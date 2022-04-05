using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;
using Stardust.Registry;
using Stardust.Services;

namespace Stardust
{
    /// <summary>应用客户端。每个应用有一个客户端连接星尘服务端</summary>
    public class AppClient : ApiHttpClient, ICommandClient, IRegistry
    {
        #region 属性
        /// <summary>应用</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>节点编码</summary>
        public String NodeCode { get; set; }

        private ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>命令集合</summary>
        public IDictionary<String, Delegate> Commands => _commands;

        /// <summary>收到命令时触发</summary>
        public event EventHandler<CommandEventArgs> Received;

        private AppInfo _appInfo;
        private readonly String _version;

        /// <summary>已发布服务，记录下来，定时注册刷新</summary>
        private readonly ConcurrentDictionary<String, PublishServiceInfo> _publishServices = new();

        /// <summary>已消费服务，记录下来，定时刷新获取新的地址信息</summary>
        private readonly ConcurrentDictionary<String, ConsumeServiceInfo> _consumeServices = new();
        private readonly ConcurrentDictionary<String, ServiceModel[]> _consumes = new();
        private readonly ConcurrentDictionary<String, IList<Delegate>> _consumeEvents = new();
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public AppClient()
        {
            try
            {
                var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
                var asm = AssemblyX.Entry ?? executing;
                if (asm != null)
                {
                    if (AppId == null) AppId = asm.Name;
                    AppName = asm.Title;
                    _version = asm.Version;
                }

                ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
            }
            catch { }

            Log = XTrace.Log;
        }

        /// <summary>实例化</summary>
        /// <param name="urls"></param>
        public AppClient(String urls) : this()
        {
            if (!urls.IsNullOrEmpty())
            {
                var ss = urls.Split(',', ';');
                for (var i = 0; i < ss.Length; i++)
                {
                    Add("service" + (i + 1), new Uri(ss[i]));
                }
            }
        }

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            StopTimer();

            foreach (var item in _publishServices)
            {
                //UnregisterAsync(item.Value).Wait();
                Unregister(item.Key);
            }
        }
        #endregion

        #region 方法
        /// <summary>开始客户端</summary>
        public void Start()
        {
            try
            {
                if (AppId != "StarServer")
                {
                    // 等待注册到平台
                    var task = Task.Run(Register);
                    task.Wait(1_000);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("注册失败：{0}", ex.GetTrue().Message);
            }

            StartTimer();
        }

        private String _appName;
        /// <summary>注册</summary>
        /// <returns></returns>
        public async Task<Object> Register()
        {
            try
            {
                var inf = new AppModel
                {
                    AppId = AppId,
                    AppName = AppName,
                    ClientId = ClientId,
                    Version = _version,
                    NodeCode = NodeCode,
                };
                try
                {
                    inf.IP = NetHelper.MyIP() + "";
                }
                catch { }

                var rs = await PostAsync<String>("App/Register", inf);
                WriteLog("接入星尘服务端：{0}", rs);
                _appName = rs + "";

                //if (Filter is NewLife.Http.TokenHttpFilter thf) Token = thf.Token?.AccessToken;

                return rs;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("注册异常 {0}", ex.GetTrue().Message);

                throw;
            }
        }

        /// <summary>心跳</summary>
        /// <returns></returns>
        public async Task<Object> Ping()
        {
            try
            {
                if (_appInfo == null)
                    _appInfo = new AppInfo(Process.GetCurrentProcess()) { Version = _version };
                else
                    _appInfo.Refresh();

                var rs = await PostAsync<PingResponse>("App/Ping", _appInfo);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0) _timer.Period = rs.Period * 1000;
                }

                return rs;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("心跳异常 {0}", ex.GetTrue().Message);

                throw;
            }
        }
        #endregion

        #region 长连接
        private TimerX _timer;
        private void StartTimer()
        {
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null)
                    {
                        _timer = new TimerX(DoPing, null, 5_000, 60_000) { Async = true };

                        Attach(this);
                    }
                }
            }
        }

        private void StopTimer()
        {
            _timer.TryDispose();
            _timer = null;

            if (_websocket != null && _websocket.State == WebSocketState.Open) _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default).Wait();
            _source?.Cancel();

            //_websocket.TryDispose();
            _websocket = null;
        }

        private WebSocket _websocket;
        private CancellationTokenSource _source;
        private async Task DoPing(Object state)
        {
            DefaultSpan.Current = null;

            if (_appName == null) await Register();
            await Ping();

            var svc = _currentService;
            if (svc == null) return;

            // 使用过滤器内部token，因为它有过期刷新机制
            var token = Token;
            if (Filter is NewLife.Http.TokenHttpFilter thf) token = thf.Token?.AccessToken;
            if (token.IsNullOrEmpty()) return;

            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
                var uri = new Uri(new Uri(url), "/app/notify");
                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", "Bearer " + token);
                await client.ConnectAsync(uri, default);

                _websocket = client;

                _source = new CancellationTokenSource();
                _ = Task.Run(() => DoPull(client, _source.Token));
            }

            await OnWorkService();
        }

        private async Task DoPull(WebSocket socket, CancellationToken cancellationToken)
        {
            DefaultSpan.Current = null;
            try
            {
                var buf = new Byte[4 * 1024];
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken);
                    var model = buf.ToStr(null, 0, data.Count).ToJsonEntity<CommandModel>();
                    if (model != null)
                    {
                        // 建立追踪链路
                        using var span = Tracer?.NewSpan("cmd:" + model.Command, model);
                        if (span != null && !model.TraceId.IsNullOrEmpty()) span.TraceId = model.TraceId;
                        try
                        {
                            XTrace.WriteLine("Got Command: {0}", model.ToJson());
                            if (model.Expire.Year < 2000 || model.Expire > DateTime.Now)
                            {
                                await OnReceiveCommand(model);
                            }
                        }
                        catch (Exception ex)
                        {
                            span?.SetError(ex, null);
                        }
                    }
                }
            }
            catch (WebSocketException) { }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        #endregion

        #region 命令调度
        /// <summary>
        /// 触发收到命令的动作
        /// </summary>
        /// <param name="model"></param>
        protected virtual async Task OnReceiveCommand(CommandModel model)
        {
            var e = new CommandEventArgs { Model = model };
            Received?.Invoke(this, e);

            var rs = await this.ExecuteCommand(model);
            if (e.Reply == null) e.Reply = rs;

            if (e.Reply != null) await CommandReply(e.Reply);
        }

        /// <summary>上报服务调用结果</summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public virtual async Task<Object> CommandReply(CommandReplyModel model) => await PostAsync<Object>("App/CommandReply", model);
        #endregion

        #region 发布、消费
        /// <summary>发布服务。定时反复执行，让服务端更新注册信息</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        public async Task<Object> RegisterAsync(PublishServiceInfo service)
        {
            AddService(service);

            return await PostAsync<Object>("App/RegisterService", service);
        }

        /// <summary>取消服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        public async Task<Object> UnregisterAsync(PublishServiceInfo service)
        {
            _publishServices.TryRemove(service.ServiceName, out _);

            return await PostAsync<Object>("App/UnregisterService", service);
        }

        private void AddService(PublishServiceInfo service)
        {
            if (_publishServices.TryAdd(service.ServiceName, service))
            {
                XTrace.WriteLine("注册服务 {0}", service.ToJson());

                StartTimer();
            }
        }

        /// <summary>创建服务对象，用于自定义需要，再通过RegisterAsync发布到注册中心</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        public PublishServiceInfo CreatePublishService(String serviceName)
        {
            var asmx = AssemblyX.Entry;
            var ip = NetHelper.MyIP();

            var service = new PublishServiceInfo
            {
                ServiceName = serviceName,

                ClientId = ClientId,
                IP = ip + "",
                Version = asmx?.Version,
            };

            return service;
        }

        /// <summary>发布服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="address">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public async Task<Object> RegisterAsync(String serviceName, String address, String tag = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            var service = CreatePublishService(serviceName);
            service.Address = address;
            service.Tag = tag;

            var rs = await RegisterAsync(service);
            XTrace.WriteLine("注册完成 {0}", rs.ToJson());

            return rs;
        }

        /// <summary>发布服务（延迟），直到回调函数返回地址信息才做真正发布</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="addressCallback">服务地址回调</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        public void Register(String serviceName, Func<String> addressCallback, String tag = null)
        {
            if (addressCallback == null) throw new ArgumentNullException(nameof(addressCallback));

            var service = CreatePublishService(serviceName);
            service.AddressCallback = addressCallback;
            service.Tag = tag;

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
        public async Task<ServiceModel[]> ResolveAsync(ConsumeServiceInfo service) => await PostAsync<ServiceModel[]>("App/ResolveService", service);

        /// <summary>消费得到服务地址信息</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="minVersion">最小版本</param>
        /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
        /// <returns></returns>
        public async Task<ServiceModel[]> ResolveAsync(String serviceName, String minVersion = null, String tag = null)
        {
            var service = new ConsumeServiceInfo
            {
                ServiceName = serviceName,
                MinVersion = minVersion,
                Tag = tag,

                ClientId = ClientId,
            };

            if (_consumeServices.TryAdd(serviceName, service))
            {
                XTrace.WriteLine("消费服务 {0}", service.ToJson());

                StartTimer();

                var models = await ResolveAsync(service);
                _consumes[serviceName] = models;

                return models;
            }
            else
            {
                if (_consumes.TryGetValue(serviceName, out var models)) return models;

                return null;
            }
        }

        /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="callback">回调方法</param>
        public void Bind(String serviceName, Action<String, ServiceModel[]> callback)
        {
            var list = _consumeEvents.GetOrAdd(serviceName, k => new List<Delegate>());
            list.Add(callback);

            StartTimer();
        }

        private async Task OnWorkService()
        {
            // 刷新已发布服务
            foreach (var item in _publishServices)
            {
                var svc = item.Value;
                if (svc.Address.IsNullOrEmpty() && svc.AddressCallback != null)
                {
                    var address = svc.AddressCallback();
                    if (!address.IsNullOrEmpty()) svc.Address = address;
                }

                if (!svc.Address.IsNullOrEmpty()) await RegisterAsync(svc);
            }

            // 刷新已消费服务
            foreach (var item in _consumeServices)
            {
                var svc = item.Value;
                var ms = await ResolveAsync(svc);
                if (ms != null && ms.Length > 0)
                {
                    _consumes[svc.ServiceName] = ms;

                    // 需要判断，只有服务改变才调用相应事件
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

        #region 刷新
        /// <summary>附加刷新命令</summary>
        /// <param name="client"></param>
        public void Attach(ICommandClient client)
        {
            client.RegisterCommand("registry/register", DoRefresh);
            client.RegisterCommand("registry/unregister", DoRefresh);
        }

        private async Task<String> DoRefresh(String argument)
        {
            await OnWorkService();

            return "刷新服务成功";
        }
        #endregion

        #region 辅助
        #endregion
    }
}