using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Monitors;

/// <summary>星尘性能追踪器，追踪数据提交到星尘平台</summary>
/// <remarks>其它项目有可能直接使用这个类代码，用于提交监控数据</remarks>
public class StarTracer : DefaultTracer
{
    #region 属性
    /// <summary>应用标识</summary>
    public String? AppId { get; set; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    private String? _clientId;
    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    /// <remarks>首次联网之前IP可能为空，此时为“@进程号”；获得IP后自动补全为完整标识</remarks>
    public String? ClientId { get => _clientId ?? Runtime.ClientId; set => _clientId = value; }

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认2 * 24 * 60</summary>
    public Int32 MaxFails { get; set; } = 2 * 24 * 60;

    /// <summary>要排除的操作名</summary>
    public String[]? Excludes { get; set; }

    /// <summary>Api客户端</summary>
    public IApiClient? Client { get; set; }

    /// <summary>星尘工厂</summary>
    public StarFactory? Factory { get; set; }

    /// <summary>剔除埋点调用自己。默认true</summary>
    public Boolean TrimSelf { get; set; } = true;

    /// <summary>性能收集。收集应用性能信息，数量较大的客户端可以不必收集应用性能信息</summary>
    public Boolean EnableMeter { get; set; } = true;

    /// <summary>采集 OpenTelemetry 埋点。把进程内第三方组件的活动转成埋点上报，默认true</summary>
    public Boolean EnableOpenTelemetry { get; set; } = true;

#if NET6_0_OR_GREATER
    /// <summary>OpenTelemetry 采集器。为空时按 <see cref="EnableOpenTelemetry"/> 自动创建</summary>
    /// <remarks>可预先配置源名白名单或黑名单，例如只采集 Redis 与 Npgsql</remarks>
    public OpenTelemetryCollector? OpenTelemetry { get; set; }
#endif

    private readonly String? _version;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly ConcurrentQueue<TraceModel> _fails = new();
    private AppInfo? _appInfo;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public StarTracer()
    {
        var set = StarSetting.Current;
        AppId = set.AppKey;
        //Secret = set.Secret;
        Period = set.TracerPeriod;
        MaxSamples = set.MaxSamples;
        MaxErrors = set.MaxErrors;
        EnableOpenTelemetry = set.EnableOpenTelemetry;

        if (set.Debug) Log = XTrace.Log;

        Resolver = new StarTracerResolver();

        try
        {
            var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
            var asm = AssemblyX.Entry ?? executing;
            if (asm != null)
            {
                AppId ??= asm.Name;
                AppName = asm.Title;
                _version = asm.FileVersion;
            }
        }
        catch { }
    }

    /// <summary>指定服务端地址来实例化追踪器</summary>
    /// <param name="server"></param>
    public StarTracer(String server) : this()
    {
        if (server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(server));

        var http = new ApiHttpClient(server)
        {
            Tracer = this
        };
        Client = http;

        var set = StarSetting.Current;
        if (!AppId.IsNullOrEmpty() && !set.Secret.IsNullOrEmpty())
            http.Filter = new TokenHttpFilter { UserName = AppId, Password = set.Secret };
    }
    #endregion

    #region OpenTelemetry
#if NET6_0_OR_GREATER
    /// <summary>挂载 OpenTelemetry 采集器。把进程内第三方组件的活动转成埋点，与自研埋点统一上报</summary>
    /// <returns>采集器实例，未启用时返回空。低版本目标框架不支持 OpenTelemetry，方法不提供</returns>
    public OpenTelemetryCollector? AttachOpenTelemetry()
    {
        if (!EnableOpenTelemetry) return null;

        var collector = OpenTelemetry;
        if (collector == null)
        {
            collector = new OpenTelemetryCollector { Tracer = this };
            OpenTelemetry = collector;
        }
        collector.Attach();

        return collector;
    }
#endif
    #endregion

    #region 销毁
    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
#if NET6_0_OR_GREATER
            OpenTelemetry?.Dispose();
            OpenTelemetry = null;
#endif
        }
    }
    #endregion

    #region 核心业务
    private Int32 _inited;
    private readonly Object _initLock = new();
    private void Init()
    {
        if (_inited != 0) return;

        // 双检锁，防止并发重复初始化
        lock (_initLock)
        {
            if (_inited != 0) return;

            // 自动从本地星尘代理获取地址
            var client = Client;
            if (client == null) throw new ArgumentNullException(nameof(Client));

            var server = client is ClientBase cbase ? cbase.Server : (client + "");
            WriteLog("Stardust monitoring center Server={0} AppId={1} ClientId={2}", server, AppId, ClientId);

            _inited = 1;
        }
    }

    /// <summary>处理Span集合。默认输出日志，可重定义输出控制台</summary>
    protected override void ProcessSpans(ISpanBuilder[] builders)
    {
        if (builders == null) return;

        // 剔除项
        if (Excludes != null) builders = builders.Where(e => !Excludes.Any(y => y.IsMatch(e.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (TrimSelf) builders = builders.Where(e => !e.Name.EndsWithIgnoreCase("/Trace/Report", "/Trace/ReportRaw")).ToArray();
        if (builders.Length == 0) return;

        // 初始化
        Init();

        var client = Client;
        if (client == null || client is IDisposable2 ds && ds.Disposed) return;

        // 构建应用信息。如果应用心跳已存在，则监控上报时不需要携带应用性能信息
        if (EnableMeter && client is not ClientBase)
        {
            if (_appInfo == null)
                _appInfo = new AppInfo(_process) { Version = _version };
            else
                _appInfo.Refresh();
        }

        // 发送，失败后进入队列
        var model = new TraceModel
        {
            AppId = AppId,
            AppName = AppName,
            ClientId = ClientId,
            Version = _version,
            Info = _appInfo,

            Builders = builders
        };

        // 如果网络不可用，直接保存到队列
        if (!NetworkInterface.GetIsNetworkAvailable() || client is not ClientBase cb || !cb.Logined)
        {
            SaveFails(model);
            return;
        }

        try
        {
            // 数据过大时，以压缩格式上传
            var body = model.ToJson();
            var rs = body.Length > 1024 ?
                 client.Invoke<TraceResponse>("Trace/ReportRaw", body.GetBytes()) :
                 client.Invoke<TraceResponse>("Trace/Report", model);
            // 处理响应参数
            if (rs != null)
            {
                if (rs.Period > 0) Period = rs.Period;
                if (rs.MaxSamples > 0) MaxSamples = rs.MaxSamples;
                if (rs.MaxErrors > 0) MaxErrors = rs.MaxErrors;
                if (rs.Timeout > 0) Timeout = rs.Timeout;
                if (rs.MaxTagLength > 0) MaxTagLength = rs.MaxTagLength;
                if (rs.EnableMeter != null) EnableMeter = rs.EnableMeter.Value;
                Excludes = rs.Excludes;

                if (Resolver is StarTracerResolver resolver && rs.RequestTagLength != 0)
                {
                    resolver.RequestContentAsTag = rs.RequestTagLength >= 0;
                    resolver.RequestTagLength = rs.RequestTagLength;
                }

                // 保存到配置文件
                if (rs.Period > 0 || rs.MaxSamples > 0 || rs.MaxErrors > 0)
                {
                    var set = StarSetting.Current;
                    set.TracerPeriod = Period;
                    set.MaxSamples = MaxSamples;
                    set.MaxErrors = MaxErrors;
                    set.Save();
                }
            }
            else
            {
                XTrace.WriteLine("ProcessSpans rs=null");
            }
        }
        catch (ApiException ex)
        {
            Log?.Error(ex + "");
        }
        catch (Exception ex)
        {
            var source = (client as ApiHttpClient)?.Source;
            var ex2 = ex is AggregateException aex ? aex.InnerException : ex;
            var detail = ex2 switch
            {
                TaskCanceledException tce => $"TaskId={tce.Task?.Id}",
                HttpRequestException hre => hre.Message,
                SocketException se => $"SocketErrorCode={se.SocketErrorCode}",
                _ => ex + "",
            };
            Log?.Debug("Monitoring center [{0}] error: {1}: {2}", source, ex2.GetType().Name, detail);

            //if (ex2 is not HttpRequestException)
            //    Log?.Error(ex + "");

            SaveFails(model);

            return;
        }

        // 如果发送成功，则继续发送以前失败的数据
        ProcessFails();
    }

    void SaveFails(TraceModel model)
    {
        if (model.Builders != null && _fails.Count < MaxFails)
        {
            // 失败时清空采样数据，避免内存暴涨
            foreach (var item in model.Builders)
            {
                if (item is DefaultSpanBuilder builder)
                {
                    builder.Samples = null;
                    builder.ErrorSamples = null;
                }
            }
            model.Info = model.Info?.Clone();
            _fails.Enqueue(model);
        }
    }

    void ProcessFails()
    {
        var client = Client;
        if (client == null || client is IDisposable2 ds && ds.Disposed) return;

        while (_fails.TryDequeue(out var model))
        {
            //model = _fails.Dequeue();
            try
            {
                client.Invoke<Object>("Trace/Report", model);
            }
            catch (ApiException ex)
            {
                Log?.Error(ex + "");
            }
            catch (Exception ex)
            {
                Log?.Info("Failed to report the sample data twice; discarding this batch: {0}", model.Builders?.FirstOrDefault()?.StartTime.ToDateTime());
                //if (Log != null && Log.Level <= LogLevel.Debug) Log?.Error(ex + "");
                Log?.Debug("{0}", ex);

                // 星尘收集器上报，二次失败后放弃该批次数据，因其很可能是错误数据
                //_fails.Enqueue(model);
                break;
            }
        }
    }
    #endregion

    #region 方法
    /// <summary>全局注入</summary>
    public void AttachGlobal()
    {
        Instance = this;
        ApiHelper.Tracer = this;

#if NET6_0_OR_GREATER
        // 采集第三方组件的 OpenTelemetry 埋点，与自研埋点统一上报星尘
        AttachOpenTelemetry();
#endif

#if NET5_0_OR_GREATER
        // 订阅Http事件
        var observer = new DiagnosticListenerObserver { Tracer = this };
        observer.Subscribe(new HttpDiagnosticListener());
        observer.Subscribe(new EfCoreDiagnosticListener());
        observer.Subscribe(new SqlClientDiagnosticListener());
        observer.Subscribe(new MongoDbDiagnosticListener());
        observer.Subscribe(new GrpcDiagnosticListener());
#endif
#if !NET40 && !NET45
        new DnsEventListener { Tracer = this };
        new SocketEventListener { Tracer = this };
#endif

        // 反射处理XCode追踪
        {
            var type = "XCode.DataAccessLayer.DAL".GetTypeEx();
            var pi = type?.GetPropertyEx("GlobalTracer");
            if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
        }

        // 反射处理Cube追踪
        {
            var type = "NewLife.Cube.WebMiddleware.TracerMiddleware".GetTypeEx();
            var pi = type?.GetPropertyEx("Tracer");
            if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
        }

        // 反射处理Star追踪
        {
            var type = "Stardust.Extensions.TracerMiddleware".GetTypeEx();
            var pi = type?.GetPropertyEx("Tracer");
            if (pi != null && pi.PropertyType == typeof(ITracer)) pi.SetValue(null, this, null);
        }
    }
    #endregion

    #region 全局注册
    /// <summary>全局注册星尘性能追踪器</summary>
    /// <param name="server">星尘监控中心地址，为空时自动从本地探测</param>
    /// <returns></returns>
    public static StarTracer? Register(String? server = null)
    {
        if (server.IsNullOrEmpty())
        {
            var set = StarSetting.Current;
            server = set.Server;
        }
        if (server.IsNullOrEmpty())
        {
            var client = new LocalStarClient();
            var inf = client.GetInfo();
            server = inf?.Server;

            if (!server.IsNullOrEmpty()) XTrace.WriteLine("Stardust probe: {0}", server);
        }
        if (server.IsNullOrEmpty()) return null;

        if (Instance is StarTracer tracer && tracer.Client is ApiHttpClient) return tracer;

        tracer = new StarTracer(server);
        tracer.AttachGlobal();

        return tracer;
    }
    #endregion
}