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

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认2 * 24 * 60</summary>
    public Int32 MaxFails { get; set; } = 2 * 24 * 60;

    /// <summary>要排除的操作名</summary>
    public String[]? Excludes { get; set; }

    /// <summary>Api客户端</summary>
    public IApiClient? Client { get; set; }

    /// <summary>剔除埋点调用自己。默认true</summary>
    public Boolean TrimSelf { get; set; } = true;

    /// <summary>性能收集。收集应用性能信息，数量较大的客户端可以不必收集应用性能信息</summary>
    public Boolean EnableMeter { get; set; } = true;

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
                _version = asm.Version;
            }

            ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
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

    #region 核心业务
    private Boolean _inited;
    private void Init()
    {
        if (_inited) return;

        // 自动从本地星尘代理获取地址
        if (Client == null) throw new ArgumentNullException(nameof(Client));

        var server = Client is ClientBase cbase ? cbase.Server : (Client + "");
        WriteLog("星尘监控中心 Server={0} AppId={1} ClientId={2}", server, AppId, ClientId);

        _inited = true;
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
        if (client == null) return;

        // 构建应用信息。如果应用心跳已存在，则监控上报时不需要携带应用性能信息
        if (EnableMeter && Client is not ClientBase)
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
        if (!NetworkInterface.GetIsNetworkAvailable())
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
                    resolver.RequestContentAsTag = rs.RequestTagLength > 0;
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
            var source = (Client as ApiHttpClient)?.Source;
            var ex2 = ex is AggregateException aex ? aex.InnerException : ex;
            if (ex2 is TaskCanceledException tce)
                Log?.Debug("监控中心[{0}]出错 {1} TaskId={2}", source, ex2.GetType().Name, tce.Task?.Id);
            else if (ex2 is HttpRequestException hre)
                Log?.Debug("监控中心[{0}]出错 {1} {2}", source, ex2.GetType().Name, hre.Message);
            else if (ex2 is SocketException se)
                Log?.Debug("监控中心[{0}]出错 {1} SocketErrorCode={2}", source, ex2.GetType().Name, se.SocketErrorCode);
            else
                Log?.Debug("监控中心[{0}]出错 {1}", source, ex);

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
        if (client == null) return;

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
                Log?.Info("二次上报失败，放弃该批次采样数据，{0}", model.Builders?.FirstOrDefault()?.StartTime.ToDateTime());
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

#if NET5_0_OR_GREATER
        // 订阅Http事件
        var observer = new DiagnosticListenerObserver { Tracer = this };
        observer.Subscribe(new HttpDiagnosticListener());
        observer.Subscribe(new EfCoreDiagnosticListener());
        observer.Subscribe(new SqlClientDiagnosticListener());
        observer.Subscribe(new MongoDbDiagnosticListener());
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

            if (!server.IsNullOrEmpty()) XTrace.WriteLine("星尘探测：{0}", server);
        }
        if (server.IsNullOrEmpty()) return null;

        if (Instance is StarTracer tracer && tracer.Client is ApiHttpClient) return tracer;

        tracer = new StarTracer(server);
        tracer.AttachGlobal();

        return tracer;
    }
    #endregion
}