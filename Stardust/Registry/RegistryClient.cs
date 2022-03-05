using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;

namespace Stardust.Registry
{
    /// <summary>尘埃客户端。每个应用有一个客户端连接星尘服务端</summary>
    public class RegistryClient : DisposeBase, IRegistry
    {
        #region 属性
        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>客户端</summary>
        public IApiClient Client { get; set; }

        private readonly ConcurrentDictionary<String, PublishServiceInfo> _publishServices = new();
        private readonly ConcurrentDictionary<String, ConsumeServiceInfo> _consumeServices = new();
        private readonly ConcurrentDictionary<String, ServiceModel[]> _consumes = new();
        private readonly ConcurrentDictionary<String, IList<Delegate>> _consumeEvents = new();
        #endregion

        #region 构造
        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            _timer.TryDispose();
            _timer = null;

            foreach (var item in _publishServices)
            {
                //UnregisterAsync(item.Value).Wait();
                Unregister(item.Key);
            }
        }
        #endregion

        #region 心跳
        private AppInfo _appInfo;
        private TimerX _timer;
        private void InitTimer()
        {
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null)
                        _timer = new TimerX(DoWork, null, 3_000, 60_000) { Async = true };
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

                var rs = await Client.InvokeAsync<PingResponse>("Dust/Ping", _appInfo);
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

        private async Task DoWork(Object state)
        {
            //Ping().Wait();

            // 刷新已发布服务
            foreach (var item in _publishServices)
            {
                var svc = item.Value;
                if (svc.Address.IsNullOrEmpty() && svc.AddressCallback != null)
                {
                    var address = svc.AddressCallback();
                    if (address.IsNullOrEmpty()) continue;

                    svc.Address = address;
                }

                await RegisterAsync(svc);
            }

            // 刷新已消费服务
            foreach (var item in _consumeServices)
            {
                var svc = item.Value;
                var ms = await ResolveAsync(svc);
                if (ms != null && ms.Length > 0)
                {
                    //_consumes.TryAdd(svc.ServiceName, ms);
                    _consumes[svc.ServiceName] = ms;

                    //todo 需要判断，只有服务改变才调用相应事件
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
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        public async Task<Object> RegisterAsync(PublishServiceInfo service) => await Client.InvokeAsync<Object>("RegisterService", service);

        /// <summary>取消服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        public async Task<Object> UnregisterAsync(PublishServiceInfo service) => await Client.InvokeAsync<Object>("UnregisterService", service);

        private void AddService(PublishServiceInfo service)
        {
            var asmx = AssemblyX.Entry;
            var ip = NetHelper.MyIP();

            service.ClientId = ClientId;
            service.IP = ip + "";
            service.Version = asmx?.Version;

            XTrace.WriteLine("注册服务 {0}", service.ToJson());

            _publishServices[service.ServiceName] = service;

            InitTimer();
        }

        /// <summary>发布服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="address">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public async Task<Object> RegisterAsync(String serviceName, String address, String tag = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            var service = new PublishServiceInfo
            {
                ServiceName = serviceName,
                Address = address,
                Tag = tag,
            };

            AddService(service);

            var rs = await RegisterAsync(service);
            XTrace.WriteLine("注册完成 {0}", rs.ToJson());

            return rs;
        }

        /// <summary>发布服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="addressCallback">服务地址回调</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public void Register(String serviceName, Func<String> addressCallback, String tag = null)
        {
            if (addressCallback == null) throw new ArgumentNullException(nameof(addressCallback));

            var service = new PublishServiceInfo
            {
                ServiceName = serviceName,
                AddressCallback = addressCallback,
                Tag = tag,
            };

            AddService(service);
        }

        /// <summary>取消服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public Boolean Unregister(String serviceName)
        {
            if (!_publishServices.TryGetValue(serviceName, out var service)) return false;
            if (service == null) return false;

            XTrace.WriteLine("取消注册 {0}", service.ToJson());
            UnregisterAsync(service).Wait();

            return true;
        }

        /// <summary>消费服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        public async Task<ServiceModel[]> ResolveAsync(ConsumeServiceInfo service) => await Client.InvokeAsync<ServiceModel[]>("ResolveService", service);

        /// <summary>消费得到服务地址信息</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="minVersion">最小版本</param>
        /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
        /// <returns></returns>
        public async Task<ServiceModel[]> ResolveAsync(String serviceName, String minVersion = null, String tag = null)
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

                    ClientId = $"{ip}@{p.Id}",
                };

                XTrace.WriteLine("消费服务 {0}", service.ToJson());

                //_consumeServices.TryAdd(service.ServiceName, service);
                _consumeServices[service.ServiceName] = service;

                InitTimer();

                return await ResolveAsync(service);
            }

            if (_consumes.TryGetValue(serviceName, out var models)) return models;

            return null;
        }

        /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="callback">回调方法</param>
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