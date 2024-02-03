using System.Collections.Concurrent;
using System.Diagnostics;
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
using NewLife.Caching;
#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
using System.Net.WebSockets;
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust;

/// <summary>应用客户端。每个应用有一个客户端连接星尘服务端</summary>
public class AppClient : ApiHttpClient, ICommandClient, IRegistry, IEventProvider
{
    #region 属性
    /// <summary>应用</summary>
    public String AppId { get; set; } = null!;

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>节点编码</summary>
    public String? NodeCode { get; set; }

    /// <summary>WebSocket长连接。建立长连接后，可以实时感知配置更新和注册服务更新，默认false</summary>
    public Boolean UseWebSocket { get; set; }

    /// <summary>看门狗超时时间。默认0秒</summary>
    /// <remarks>
    /// 设置看门狗超时时间，超过该时间未收到心跳，将会重启本应用进程。
    /// 0秒表示不启用看门狗。
    /// </remarks>
    public Int32 WatchdogTimeout { get; set; }

    /// <summary>星尘工厂</summary>
    public StarFactory? Factory { get; set; }

    private ConcurrentDictionary<String, Delegate> _commands = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>命令集合</summary>
    public IDictionary<String, Delegate> Commands => _commands;

    /// <summary>收到命令时触发</summary>
    public event EventHandler<CommandEventArgs>? Received;

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
    public Int32 MaxFails { get; set; } = 120;

    private AppInfo? _appInfo;
    private readonly String? _version;

    /// <summary>已发布服务，记录下来，定时注册刷新</summary>
    private readonly ConcurrentDictionary<String, PublishServiceInfo> _publishServices = new();

    /// <summary>已消费服务，记录下来，定时刷新获取新的地址信息</summary>
    private readonly ConcurrentDictionary<String, ConsumeServiceInfo> _consumeServices = new();
    private readonly ConcurrentDictionary<String, ServiceModel[]> _consumes = new();
    private readonly ConcurrentDictionary<String, IList<Delegate>> _consumeEvents = new();
    private readonly ConcurrentQueue<AppInfo> _fails = new();
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public AppClient()
    {
        // 加载已保存数据
        var dic = LoadConsumeServicese();
        if (dic != null && dic.Count > 0)
        {
            foreach (var item in dic)
            {
                _consumes[item.Key] = item.Value;
            }
        }

        try
        {
            var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
            var asm = AssemblyX.Entry ?? executing;
            if (asm != null)
            {
                AppId ??= asm.Name;
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
                var task = TaskEx.Run(Register);
                task.Wait(1_000);
            }
        }
        catch (Exception ex)
        {
            Log?.Error("注册失败：{0}", ex.GetTrue().Message);
        }

        StartTimer();
    }

    private String? _appName;
    /// <summary>注册</summary>
    /// <returns></returns>
    public async Task<Object?> Register()
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
                IP = AgentInfo.GetIps()
            };

            var rs = await PostAsync<String>("App/Register", inf);
            WriteLog("接入星尘服务端：{0}", rs);
            _appName = rs + "";

            //if (Filter is NewLife.Http.TokenHttpFilter thf) Token = thf.Token?.AccessToken;

            return rs;
        }
        catch (Exception ex)
        {
            if (ex is HttpRequestException)
                Log?.Info("注册异常[{0}] {1}", Source, ex.GetTrue()?.Message);
            else
                Log?.Info(ex.ToString());

            //throw;
            return null;
        }
    }

    /// <summary>心跳</summary>
    /// <returns></returns>
    public async Task<Object?> Ping()
    {
        try
        {
            if (_appInfo == null)
                _appInfo = new AppInfo(Process.GetCurrentProcess()) { Version = _version };
            else
                _appInfo.Refresh();

            PingResponse? rs = null;
            try
            {
                rs = await PostAsync<PingResponse>("App/Ping", _appInfo);
                if (rs != null)
                {
                    // 由服务器改变采样频率
                    if (rs.Period > 0 && _timer != null) _timer.Period = rs.Period * 1000;

                    var delay = 0;
                    var dt = rs.Time.ToDateTime();
                    if (dt.Year > 2000)
                    {
                        // 计算延迟
                        var ts = DateTime.UtcNow - dt;
                        var ms = (Int32)Math.Round(ts.TotalMilliseconds);
                        delay = delay > 0 ? (delay + ms) / 2 : ms;
                    }

                    // 时间偏移，用于修正本地时间
                    dt = rs.ServerTime.ToDateTime();
                    if (dt.Year > 2000) _span = dt.AddMilliseconds(delay / 2) - DateTime.UtcNow;

                    // 推队列
                    if (rs.Commands != null && rs.Commands.Length > 0)
                    {
                        foreach (var model in rs.Commands)
                        {
                            await ReceiveCommand(model);
                        }
                    }
                }
            }
            catch
            {
                if (_fails.Count < MaxFails) _fails.Enqueue(_appInfo.Clone());

                throw;
            }

            // 上报正常，处理历史，失败则丢弃
            while (_fails.TryDequeue(out var info))
            {
                await PostAsync<PingResponse>("App/Ping", info);
            }

            return rs;
        }
        catch (Exception ex)
        {
            var ex2 = ex.GetTrue();
            if (ex2 is ApiException aex && (aex.Code == 401 || aex.Code == 403))
            {
                XTrace.WriteLine("重新登录");
                return await Register();
            }

            Log?.Debug("心跳异常 {0}", ex.GetTrue().Message);

            throw;
        }
    }

    /// <summary>向本地StarAgent发送心跳</summary>
    /// <returns></returns>
    public async Task<Object?> PingLocal()
    {
        if (WatchdogTimeout <= 0) return null;

        var local = Factory?.Local;
        if (local == null || local.Info == null) return null;

        if (_appInfo == null) return null;

        return await local.PingAsync(_appInfo, WatchdogTimeout);
    }

    private TimeSpan _span;
    /// <summary>获取相对于服务器的当前时间，避免两端时间差</summary>
    /// <returns></returns>
    public DateTime GetNow() => DateTime.Now.Add(_span);
    #endregion

    #region 上报
    private readonly ConcurrentQueue<EventModel> _events = new();
    private readonly ConcurrentQueue<EventModel> _failEvents = new();
    private TimerX? _eventTimer;
    private String? _eventTraceId;

    /// <summary>批量上报事件</summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public async Task<Int32> PostEvents(params EventModel[] events) => await PostAsync<Int32>("App/PostEvents", events);

    async Task DoPostEvent(Object state)
    {
        DefaultSpan.Current = null;
        var tid = _eventTraceId;
        _eventTraceId = null;

        // 正常队列为空，异常队列有数据，给它一次机会
        if (_events.IsEmpty && !_failEvents.IsEmpty)
        {
            while (_failEvents.TryDequeue(out var ev))
            {
                _events.Enqueue(ev);
            }
        }

        while (!_events.IsEmpty)
        {
            var max = 100;
            var list = new List<EventModel>();
            while (_events.TryDequeue(out var model) && max-- > 0) list.Add(model);

            using var span = Tracer?.NewSpan("PostEvent", list.Count);
            if (!tid.IsNullOrEmpty()) span?.Detach(tid);
            try
            {
                if (list.Count > 0) await PostEvents(list.ToArray());

                // 成功后读取本地缓存
                while (_failEvents.TryDequeue(out var ev))
                {
                    _events.Enqueue(ev);
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                // 失败后进入本地缓存
                foreach (var item in list)
                {
                    _failEvents.Enqueue(item);
                }
            }
        }
    }

    /// <summary>写事件</summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    public virtual Boolean WriteEvent(String type, String name, String? remark)
    {
        // 记录追踪标识，上报的时候带上，尽可能让源头和下游串联起来
        _eventTraceId = DefaultSpan.Current?.ToString();

        var now = GetNow().ToUniversalTime();
        var ev = new EventModel { Time = now.ToLong(), Type = type, Name = name, Remark = remark };
        _events.Enqueue(ev);

        _eventTimer?.SetNext(1000);

        return true;
    }
    #endregion

    #region 长连接
    private TimerX? _timer;
    private void StartTimer()
    {
        if (_timer == null)
        {
            lock (this)
            {
                if (_timer == null)
                {
                    _timer = new TimerX(DoPing, null, 5_000, 60_000) { Async = true };
                    _eventTimer = new TimerX(DoPostEvent, null, 3_000, 60_000, "Device") { Async = true };

                    Attach(this);
                }
            }
        }
    }

    private void StopTimer()
    {
        _timer.TryDispose();
        _timer = null;

        _eventTimer.TryDispose();
        _eventTimer = null;

#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
        _source?.Cancel();
        try
        {
            if (_websocket != null && _websocket.State == WebSocketState.Open)
                _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
        }
        catch { }

        //_websocket.TryDispose();
        _websocket = null;
#endif
    }

    private async Task DoPing(Object state)
    {
        DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan("AppPing");
        try
        {
            if (_appName == null)
            {
                var rs = await Register();
                if (rs == null) return;
            }

            // 向服务端发送心跳后，再向本地发送心跳
            await Ping();
            await PingLocal();

            await RefreshPublish();
            await RefreshConsume();

#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
            var svc = _currentService;
            if (svc == null || !UseWebSocket) return;

            // 使用过滤器内部token，因为它有过期刷新机制
            var token = Token;
            if (Filter is NewLife.Http.TokenHttpFilter thf) token = thf.Token?.AccessToken;
            span?.AppendTag($"svc={svc.Address} Token=[{token?.Length}]");

            if (token.IsNullOrEmpty()) return;

            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                try
                {
                    // 在websocket链路上定时发送心跳，避免长连接被断开
                    var str = "Ping";
                    await _websocket.SendAsync(new ArraySegment<Byte>(str.GetBytes()), WebSocketMessageType.Text, true, default);
                }
                catch (Exception ex)
                {
                    span?.SetError(ex, null);
                    WriteLog("{0}", ex);
                }
            }

            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                var url = svc.Address.ToString().Replace("http://", "ws://").Replace("https://", "wss://");
                var uri = new Uri(new Uri(url), "/app/notify");

                using var span2 = Tracer?.NewSpan("WebSocketConnect", uri + "");

                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", "Bearer " + token);

                span?.AppendTag($"WebSocket.Connect {uri}");
                await client.ConnectAsync(uri, default);

                _websocket = client;

                _source = new CancellationTokenSource();
                _ = Task.Run(() => DoPull(client, _source.Token));
            }
#endif
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Debug("{0}", ex);
        }
    }

#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
    private WebSocket? _websocket;
    private CancellationTokenSource? _source;
    private async Task DoPull(WebSocket socket, CancellationToken cancellationToken)
    {
        DefaultSpan.Current = null;
        try
        {
            var buf = new Byte[4 * 1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var data = await socket.ReceiveAsync(new ArraySegment<Byte>(buf), cancellationToken);
                var txt = buf.ToStr(null, 0, data.Count);
                if (txt.StartsWithIgnoreCase("Pong"))
                {
                }
                else
                {
                    var model = txt.ToJsonEntity<CommandModel>();
                    if (model != null) await ReceiveCommand(model);
                }
            }
        }
        catch (WebSocketException) { }
        catch (Exception ex)
        {
            Log?.Debug("{0}", ex);
        }

        using var span = Tracer?.NewSpan("AppPull", socket.State + "");

        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "finish", default);
    }
#endif

    async Task ReceiveCommand(CommandModel model)
    {
        if (model == null) return;

        // 去重，避免命令被重复执行
        if (!_cache.Add($"nodecmd:{model.Id}", model, 3600)) return;

        // 埋点，建立调用链
        using var span = Tracer?.NewSpan("cmd:" + model.Command, model);
        if (!model.TraceId.IsNullOrEmpty()) span?.Detach(model.TraceId);
        try
        {
            //todo 有效期判断可能有隐患，现在只是假设服务器和客户端在同一个时区，如果不同，可能会出现问题
            var now = GetNow();
            XTrace.WriteLine("Got Command: {0}", model.ToJson());
            if (model.Expire.Year < 2000 || model.Expire > now)
            {
                // 延迟执行
                var ts = model.StartTime - now;
                if (ts.TotalMilliseconds > 0)
                {
                    TimerX.Delay(s =>
                    {
                        _ = OnReceiveCommand(model);
                    }, (Int32)ts.TotalMilliseconds);

                    var reply = new CommandReplyModel
                    {
                        Id = model.Id,
                        Status = CommandStatus.处理中,
                        Data = $"已安排计划执行 {model.StartTime.ToFullString()}"
                    };
                    await CommandReply(reply);
                }
                else
                    await OnReceiveCommand(model);
            }
            else
            {
                var reply = new CommandReplyModel { Id = model.Id, Status = CommandStatus.取消 };
                await CommandReply(reply);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
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
        e.Reply ??= rs;

        if (e.Reply != null && e.Reply.Id > 0) await CommandReply(e.Reply);
    }

    /// <summary>向命令引擎发送命令，触发指定已注册动作</summary>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <returns></returns>
    public async Task SendCommand(String command, String argument) => await OnReceiveCommand(new CommandModel { Command = command, Argument = argument });

    /// <summary>上报服务调用结果</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public virtual async Task<Object?> CommandReply(CommandReplyModel model) => await PostAsync<Object>("App/CommandReply", model);
    #endregion

    #region 发布、消费
    /// <summary>发布服务（底层）。定时反复执行，让服务端更新注册信息</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel?> RegisterAsync(PublishServiceInfo service)
    {
        AddService(service);

        // 如果没有设置地址，则不要调用接口
        if (service.Address.IsNullOrEmpty()) return null;

        return await PostAsync<ServiceModel>("App/RegisterService", service);
    }

    /// <summary>取消服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel?> UnregisterAsync(PublishServiceInfo service)
    {
        _publishServices.TryRemove(service.ServiceName, out _);

        return await PostAsync<ServiceModel>("App/UnregisterService", service);
    }

    private void AddService(PublishServiceInfo service)
    {
        if (_publishServices.TryGetValue(service.ServiceName, out var svc) && svc == service) return;

        if (_publishServices.TryAdd(service.ServiceName, service))
        {
            WriteLog("注册服务 {0}", service.ToJson());

            StartTimer();
        }
        else
        {
            // 如果服务已注册，则更新，用于更新服务地址等信息
            _publishServices[service.ServiceName] = service;
        }
    }

    /// <summary>创建服务对象，用于自定义需要，再通过RegisterAsync发布到注册中心</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public PublishServiceInfo CreatePublishService(String serviceName)
    {
        var asmx = AssemblyX.Entry;
        var ip = AgentInfo.GetIps();

        var service = new PublishServiceInfo
        {
            ServiceName = serviceName,

            ClientId = ClientId,
            IP = ip,
            Version = asmx?.Version,
        };

        return service;
    }

    /// <summary>发布服务（直达）</summary>
    /// <remarks>
    /// 可以多次调用注册，用于更新服务地址和特性标签等信息。
    /// 例如web应用，刚开始时可能并不知道自己的外部地址（域名和端口），有用户访问以后，即可得知并更新。
    /// </remarks>
    /// <param name="serviceName">服务名</param>
    /// <param name="address">服务地址</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    public async Task<PublishServiceInfo> RegisterAsync(String serviceName, String address, String? tag = null, String? health = null)
    {
        if (address == null) throw new ArgumentNullException(nameof(address));

        var service = CreatePublishService(serviceName);
        service.Address = address;
        service.ExternalAddress = NewLife.Setting.Current.ServiceAddress;
        service.Tag = tag;
        service.Health = health;

        var rs = await RegisterAsync(service);
        WriteLog("注册完成 {0}", rs?.ToJson());

        return service;
    }

    /// <summary>发布服务（延迟），直到回调函数返回地址信息才做真正发布</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="addressCallback">服务地址回调</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    public PublishServiceInfo Register(String serviceName, Func<String?> addressCallback, String? tag = null, String? health = null)
    {
        if (addressCallback == null) throw new ArgumentNullException(nameof(addressCallback));

        var service = CreatePublishService(serviceName);
        service.AddressCallback = addressCallback;
        service.ExternalAddress = NewLife.Setting.Current.ServiceAddress;
        service.Tag = tag;
        service.Health = health;

        AddService(service);

        return service;
    }

    /// <summary>取消服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public PublishServiceInfo? Unregister(String serviceName)
    {
        if (!_publishServices.TryGetValue(serviceName, out var service)) return null;
        if (service == null) return null;

        WriteLog("取消注册 {0}", service.ToJson());
        UnregisterAsync(service).Wait();

        return service;
    }

    /// <summary>消费服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel[]?> ResolveAsync(ConsumeServiceInfo service) => await PostAsync<ServiceModel[]>("App/ResolveService", service);

    /// <summary>消费得到服务地址信息</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="minVersion">最小版本</param>
    /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
    /// <returns></returns>
    public async Task<ServiceModel[]?> ResolveAsync(String serviceName, String? minVersion = null, String? tag = null)
    {
        var service = new ConsumeServiceInfo
        {
            ServiceName = serviceName,
            MinVersion = minVersion,
            Tag = tag,

            ClientId = ClientId,
        };

        // 已缓存数据的Tag可能不一致，需要重新消费
        if (!_consumeServices.TryGetValue(serviceName, out var svc) || svc.Tag + "" != tag + "")
        {
            WriteLog("消费服务 {0}", service.ToJson());

            StartTimer();

            // 消费即使报错，也要往下走，借助缓存
            try
            {
                var models = await ResolveAsync(service);
                if (models != null && models.Length > 0)
                {
                    _consumes[serviceName] = models;

                    SaveConsumeServices(_consumes);
                }

                // 缓存消费服务，避免频繁消费
                _consumeServices[serviceName] = service;

                return models;
            }
            catch (Exception ex)
            {
                WriteLog("消费服务[{0}]报错：{1}", serviceName, ex.Message);
            }
        }

        _consumeServices[serviceName] = service;

        if (_consumes.TryGetValue(serviceName, out var models2)) return models2;

        return null;
    }

    /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="callback">回调方法</param>
    public void Bind(String serviceName, ServiceChangedCallback callback)
    {
        var list = _consumeEvents.GetOrAdd(serviceName, k => new List<Delegate>());
        list.Add(callback);

        StartTimer();
    }

    private async Task RefreshPublish()
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
    }

    private async Task RefreshConsume()
    {
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
                        (action as ServiceChangedCallback)?.Invoke(svc.ServiceName, ms);
                    }
                }

                SaveConsumeServices(_consumes);
            }
        }
    }

    IDictionary<String, ServiceModel[]>? LoadConsumeServicese()
    {
        var file = NewLife.Setting.Current.DataPath.CombinePath("star_services.json").GetBasePath();
        if (!File.Exists(file)) return null;

        try
        {
            var json = File.ReadAllText(file);
            return json.ToJsonEntity<IDictionary<String, ServiceModel[]>>();
        }
        catch { return null; }
    }

    void SaveConsumeServices(IDictionary<String, ServiceModel[]> models)
    {
        try
        {
            var file = NewLife.Setting.Current.DataPath.CombinePath("star_services.json").GetBasePath();
            file.EnsureDirectory(true);

            var json = models.ToJson(true);
            File.WriteAllText(file, json);
        }
        catch { }
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

    private async Task<String?> DoRefresh(String? argument)
    {
        await RefreshConsume();

        return "刷新服务成功";
    }
    #endregion

    #region 辅助
    /// <summary>
    /// 设置服务地址
    /// </summary>
    /// <param name="serverAddress"></param>
    public void SetServerAddress(String serverAddress)
    {
        if (serverAddress == null) return;

        var set = NewLife.Setting.Current;
        if (serverAddress == set.ServiceAddress) return;

        WriteLog("设置服务地址为：{0}", serverAddress);

        var count = 0;
        foreach (var service in _publishServices.Values.ToArray())
        {
            if (service.Address.IsNullOrEmpty() || IsLocal(service.Address))
            {
                service.Address = serverAddress;
                count++;
            }
        }

        if (count > 0 && _timer != null) _timer.SetNext(-1);

        set.ServiceAddress = serverAddress;
        set.Save();
    }

    /// <summary>
    /// 是否本地地址
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static Boolean IsLocal(String? address)
    {
        if (address.IsNullOrEmpty()) return false;

        if (address.Contains("://*")) return true;
        if (address.Contains("://+")) return true;
        if (address.Contains("://0.0.0.0")) return true;
        if (address.Contains("://[::]")) return true;

        return false;
    }
    #endregion
}