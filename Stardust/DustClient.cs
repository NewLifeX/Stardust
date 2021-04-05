using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using NewLife.Web;
using Stardust.Models;

namespace Stardust
{
    /// <summary>尘埃客户端。每个应用有一个客户端连接星尘服务端</summary>
    public class DustClient : ApiHttpClient
    {
        #region 属性
        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>密钥</summary>
        public String Secret { get; set; }

        /// <summary>是否已登录</summary>
        public Boolean Logined { get; set; }

        /// <summary>登录完成后触发</summary>
        public event EventHandler OnLogined;

        /// <summary>最后一次登录成功后的消息</summary>
        public IDictionary<String, Object> Info { get; private set; }

        private readonly ConcurrentDictionary<String, PublishServiceInfo> _publishServices = new();
        private readonly ConcurrentDictionary<String, ConsumeServiceInfo> _consumeServices = new();
        private readonly ConcurrentDictionary<String, ServiceModel[]> _consumes = new();
        private readonly ConcurrentDictionary<String, IList<Delegate>> _consumeEvents = new();
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public DustClient() { }

        /// <summary>实例化</summary>
        /// <param name="uris"></param>
        public DustClient(String uris) : base(uris) { }
        #endregion

        #region 方法
        /// <summary>远程调用拦截，支持重新登录</summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="method"></param>
        /// <param name="action"></param>
        /// <param name="args"></param>
        /// <param name="onRequest"></param>
        /// <returns></returns>
        public override async Task<TResult> InvokeAsync<TResult>(HttpMethod method, String action, Object args = null, Action<HttpRequestMessage> onRequest = null)
        {
            try
            {
                return await base.InvokeAsync<TResult>(method, action, args, onRequest);
            }
            catch (Exception ex)
            {
                var ex2 = ex.GetTrue();
                if (ex2 is ApiException aex && (aex.Code == 402 || aex.Code == 403) && !action.EqualIgnoreCase("OAuth/Token"))
                {
                    XTrace.WriteException(ex);
                    XTrace.WriteLine("重新登录！");
                    await Login();

                    return await base.InvokeAsync<TResult>(method, action, args, onRequest);
                }

                throw;
            }
        }
        #endregion

        #region 登录
        /// <summary>登录</summary>
        /// <returns></returns>
        public async Task<Object> Login()
        {
            XTrace.WriteLine("登录：{0}", AppId);

            // 登录前清空令牌，避免服务端使用上一次信息
            Token = null;
            Logined = false;
            Info = null;

            var rs = await PostAsync<TokenModel>("OAuth/Token", new
            {
                grant_type = "password",
                username = AppId,
                password = Secret,
            });

            // 登录后设置用于用户认证的token
            Token = rs.AccessToken;
            Logined = true;

            OnLogined?.Invoke(this, EventArgs.Empty);

            if (Logined) InitTimer();

            return rs;
        }
        #endregion

        #region 心跳报告
        private AppInfo _appInfo;
        private TimerX _timer;
        private void InitTimer()
        {
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null)
                    {
                        XTrace.WriteLine("星尘分注册中心 Server={0} AppId={1}", Services.Join(",", e => e.Address), AppId);

                        _timer = new TimerX(DoWork, null, 3_000, 60_000) { Async = true };
                    }
                }
            }
        }

        /// <summary>心跳</summary>
        /// <returns></returns>
        public async Task<Object> Ping()
        {
            try
            {
                if (_appInfo == null) _appInfo = new AppInfo(Process.GetCurrentProcess());

                var rs = await PostAsync<PingResponse>("Dust/Ping", _appInfo);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0) _timer.Period = rs.Period * 1000;
                }

                return rs;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("心跳异常 {0}", (String)ex.GetTrue().Message);

                throw;
            }
        }

        private void DoWork(Object state)
        {
            //Ping().Wait();

            foreach (var item in _publishServices)
            {
                var svc = item.Value;
                if (svc.Address.IsNullOrEmpty() && svc.AddressCallback != null)
                {
                    var address = svc.AddressCallback();
                    if (address.IsNullOrEmpty()) continue;

                    var ps = address.Split(",");
                    var uri = new NetUri(ps[0]);
                    var ip = NetHelper.MyIP();
                    svc.Client = $"{ip}:{uri.Port}";
                    svc.Address = address;
                }

                RegisterAsync(svc).Wait();
            }

            foreach (var item in _consumeServices)
            {
                var svc = item.Value;
                var ms = ResolveAsync(svc).Result;
                if (ms != null && ms.Length > 0)
                {
                    //_consumes.TryAdd(svc.ServiceName, ms);
                    _consumes[svc.ServiceName] = ms;

                    if (_consumeEvents.TryGetValue(svc.ServiceName, out var list))
                    {
                        foreach (var action in list)
                        {
                            (action as Action<String, ServiceModel[]>)?.Invoke(svc.ServiceName, ms);
                        }
                    }
                }
            }
        }
        #endregion

        #region 发布、消费
        /// <summary>发布服务</summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public async Task<Object> RegisterAsync(PublishServiceInfo service) => await PostAsync<Object>("RegisterService", service);

        /// <summary>取消服务</summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public async Task<Object> UnregisterAsync(PublishServiceInfo service) => await PostAsync<Object>("UnregisterService", service);

        /// <summary>发布</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="address">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public void Register(String serviceName, String address, String tag = null)
        {
            // 解析端口
            var ps = address.Split(",");
            if (ps == null || ps.Length == 0) throw new ArgumentNullException(nameof(address));

            var uri = new NetUri(ps[0]);

            var ip = NetHelper.MyIP();
            //var p = Process.GetCurrentProcess();
            var asmx = AssemblyX.Entry;

            var service = new PublishServiceInfo
            {
                ServiceName = serviceName,
                Address = address,
                Tag = tag,

                Client = $"{ip}:{uri.Port}",
                Version = asmx.Version,
            };

            XTrace.WriteLine("注册服务 {0}", service.ToJson());

            //_publishServices.TryAdd(service.ServiceName, service);
            _publishServices[service.ServiceName] = service;

            InitTimer();
        }

        /// <summary>发布</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="addressCallback">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public void Register(String serviceName, Func<String> addressCallback, String tag = null)
        {
            if (addressCallback == null) throw new ArgumentNullException(nameof(addressCallback));

            var asmx = AssemblyX.Entry;

            var service = new PublishServiceInfo
            {
                ServiceName = serviceName,
                AddressCallback = addressCallback,
                Tag = tag,

                Version = asmx.Version,
            };

            XTrace.WriteLine("注册服务 {0}", service.ToJson());

            //_publishServices.TryAdd(service.ServiceName, service);
            _publishServices[service.ServiceName] = service;

            InitTimer();
        }

        /// <summary>取消服务</summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public Boolean Unregister(String serviceName)
        {
            if (!_publishServices.TryGetValue(serviceName, out var service)) return false;
            if (service == null) return false;

            UnregisterAsync(service).Wait();

            return true;
        }

        /// <summary>消费</summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public async Task<ServiceModel[]> ResolveAsync(ConsumeServiceInfo service) => await PostAsync<ServiceModel[]>("ResolveService", service);

        /// <summary>消费得到服务地址信息</summary>
        /// <param name="serviceName"></param>
        /// <param name="minVersion"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public ServiceModel[] Resolve(String serviceName, String minVersion = null, String tag = null)
        {
            if (!_consumeServices.ContainsKey(serviceName))
            {
                var ip = NetHelper.MyIP();
                var p = Process.GetCurrentProcess();

                var service = new ConsumeServiceInfo
                {
                    ServiceName = serviceName,
                    MinVersion = minVersion,
                    Tag = tag,

                    Client = $"{ip}@{p.Id}",
                };

                XTrace.WriteLine("消费服务 {0}", service.ToJson());

                //_consumeServices.TryAdd(service.ServiceName, service);
                _consumeServices[service.ServiceName] = service;

                InitTimer();

                //// 首次同步调用
                //return ConsumeAsync(service).Result;
            }

            if (_consumes.TryGetValue(serviceName, out var models)) return models;

            return null;
        }

        /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
        /// <param name="serviceName"></param>
        /// <param name="callback"></param>
        public void Bind(String serviceName, Action<String, ServiceModel[]> callback)
        {
            var list = _consumeEvents.GetOrAdd(serviceName, k => new List<Delegate>());
            list.Add(callback);

            InitTimer();
        }
        #endregion

        #region 辅助
        #endregion
    }
}