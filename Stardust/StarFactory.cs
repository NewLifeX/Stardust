using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NewLife;
using NewLife.Caching;
using NewLife.Common;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Threading;
using Stardust.Configs;
using Stardust.Models;
using Stardust.Monitors;
using Stardust.Registry;
using Stardust.Services;

#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust;

/// <summary>星尘工厂</summary>
/// <remarks>
/// 文档 https://newlifex.com/blood/stardust
/// 星尘代理 https://newlifex.com/blood/staragent
/// 节点管理 https://newlifex.com/blood/stardust_node
/// 注册中心 https://newlifex.com/blood/stardust_registry
/// 监控中心 https://newlifex.com/blood/stardust_monitor
/// 配置中心 https://newlifex.com/blood/stardust_configcenter
/// 发布中心 https://newlifex.com/blood/stardust_deploy
/// </remarks>
public class StarFactory : DisposeBase
{
    #region 属性
    /// <summary>服务器地址</summary>
    public String? Server { get; set; }

    /// <summary>应用</summary>
    public String? AppId { get; set; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>应用密钥</summary>
    public String? Secret { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>客户端</summary>
    public IApiClient? Client => _client?.Client;

    /// <summary>应用客户端</summary>
    public AppClient? App => _client;

    /// <summary>配置信息。从配置中心返回的信息头</summary>
    public ConfigInfo? ConfigInfo => (_config as StarHttpConfigProvider)?.ConfigInfo;

    /// <summary>本地星尘代理</summary>
    public LocalStarClient? Local { get; private set; }

    private AppClient? _client;
    #endregion

    #region 构造
    /// <summary>
    /// 实例化星尘工厂，先后读取appsettings.json、本地StarAgent、star.config
    /// </summary>
    public StarFactory() => Init();

    /// <summary>实例化星尘工厂，指定地址、应用和密钥，创建工厂</summary>
    /// <param name="server">服务端地址。为空时先后读取appsettings.json、本地StarAgent、star.config，初始值为空，不连接服务端</param>
    /// <param name="appId">应用标识。为空时读取star.config，初始值为入口程序集名称</param>
    /// <param name="secret">应用密钥。为空时读取star.config，初始值为空</param>
    /// <returns></returns>
    public StarFactory(String? server, String? appId, String? secret)
    {
        Server = server;
        AppId = appId;
        Secret = secret;

        Init();
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _tracer.TryDispose();
        _config.TryDispose();
    }
    #endregion

    #region 初始化注册
    private void Init()
    {
        XTrace.WriteLine("正在初始化星尘……");

        Local = new LocalStarClient { Log = Log };

        // 从命令行读取参数
        var args = Environment.GetCommandLineArgs();
        if (args != null && args.Length > 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i].TrimStart('-');
                var p = key.IndexOf('=');
                if (p > 0)
                {
                    var value = key.Substring(p + 1);
                    key = key.Substring(0, p);
                    if (Server.IsNullOrEmpty() && key.EqualIgnoreCase("StarServer"))
                        Server = value;
                    else if (AppId.IsNullOrEmpty() && key.EqualIgnoreCase("StarAppId"))
                        AppId = value;
                    else if (Secret.IsNullOrEmpty() && key.EqualIgnoreCase("StarSecret"))
                        Secret = value;
                }
            }
        }

        // 从环境变量读取星尘地址、应用Id、密钥，方便容器化部署
        if (Server.IsNullOrEmpty()) Server = Environment.GetEnvironmentVariable("StarServer");
        if (AppId.IsNullOrEmpty()) AppId = Environment.GetEnvironmentVariable("StarAppId");
        if (Secret.IsNullOrEmpty()) Secret = Environment.GetEnvironmentVariable("StarSecret");

        // 不区分大小写识别环境变量
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = item.Key + "";
            var value = item.Value + "";
            if (Server.IsNullOrEmpty() && key.EqualIgnoreCase("StarServer"))
                Server = value;
            else if (AppId.IsNullOrEmpty() && key.EqualIgnoreCase("StarAppId"))
                AppId = value;
            else if (Secret.IsNullOrEmpty() && key.EqualIgnoreCase("StarSecret"))
                Secret = value;
        }

        // 读取本地appsetting
        if (Server.IsNullOrEmpty() && File.Exists("appsettings.Development.json".GetFullPath()))
        {
            using var json = new JsonConfigProvider { FileName = "appsettings.Development.json" };
            json.LoadAll();

            Server = json["StarServer"];
            AppId = json["StarAppId"];
            Secret = json["StarSecret"];
        }
        if (Server.IsNullOrEmpty() && File.Exists("appsettings.json".GetFullPath()))
        {
            using var json = new JsonConfigProvider { FileName = "appsettings.json" };
            json.LoadAll();

            Server = json["StarServer"];
            AppId = json["StarAppId"];
            Secret = json["StarSecret"];
        }

        if (!Server.IsNullOrEmpty() && Local.Server.IsNullOrEmpty()) Local.Server = Server;

        var flag = false;
        var set = StarSetting.Current;

        if (AppId != "StarAgent")
        {
            // 借助本地StarAgent获取服务器地址
            var sw = Stopwatch.StartNew();
            try
            {
                //XTrace.WriteLine("正在探测本机星尘代理……");
                var inf = Local.GetInfo();
                var server = inf?.Server;
                if (!server.IsNullOrEmpty())
                {
                    if (Server.IsNullOrEmpty()) Server = server;
                    XTrace.WriteLine("本机星尘探测：{0} Cost={1}ms", server, sw.ElapsedMilliseconds);

                    if (set.Server.IsNullOrEmpty())
                    {
                        set.Server = server;
                        flag = true;
                    }
                }
                else
                    XTrace.WriteLine("本机星尘探测：StarAgent Not Found, Cost={0}ms", sw.ElapsedMilliseconds);

                if (inf != null && !inf.PluginServer.IsNullOrEmpty() && !AppId.EqualIgnoreCase("StarWeb", "StarServer"))
                {
                    var core = NewLife.Setting.Current;
                    if (!inf.PluginServer.EqualIgnoreCase(core.PluginServer))
                    {
                        XTrace.WriteLine("据星尘代理公布，插件服务器PluginServer变更为 {0}", inf.PluginServer);
                        core.PluginServer = inf.PluginServer;
                        core.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("本机星尘探测失败！{0} Cost={1}ms", ex.Message, sw.ElapsedMilliseconds);
            }
        }

        // 如果探测不到本地应用，则使用配置
        if (Server.IsNullOrEmpty()) Server = set.Server;
        if (AppId.IsNullOrEmpty()) AppId = set.AppKey;
        if (Secret.IsNullOrEmpty()) Secret = set.Secret;

        // 重新写回配置文件
        set.Server = Server;
        set.AppKey = AppId;
        set.Secret = Secret;
        set.Save();

        // 生成ClientId，用于唯一标识当前实例，默认IP@pid
        try
        {
            // 从SysConfig读取系统名称，其受到命令行参数-Name和环境变量Name影响，方便单应用多部署（参数区分应用名）
            var sys = SysConfig.Current;
            if (AppId.IsNullOrEmpty()) AppId = sys.Name;
            if (AppName.IsNullOrEmpty()) AppName = sys.DisplayName;

            var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
            var asm = AssemblyX.Entry ?? executing;
            if (asm != null)
            {
                if (AppId.IsNullOrEmpty()) AppId = asm.Name;
                if (AppName.IsNullOrEmpty()) AppName = asm.Title;
            }

            ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
        }
        catch
        {
            ClientId = Rand.NextString(8);
        }

        XTrace.WriteLine("接入星尘平台：Server={0} AppId={1} ClientId={2}", Server, AppId, ClientId);

        Valid();
    }

    /// <summary>注册到对象容器</summary>
    /// <param name="container"></param>
    public void Register(IObjectContainer container)
    {
        container.AddSingleton(this);
        container.AddSingleton(p => Tracer ?? DefaultTracer.Instance ?? (DefaultTracer.Instance ??= new DefaultTracer()));
        container.AddSingleton(p => Service!);
        container.AddSingleton(p => (p.GetService<IRegistry>() as IEventProvider)!);
        container.AddSingleton(p => (p.GetService<IRegistry>() as ICommandClient)!);

        // 替换为混合配置提供者，优先本地配置
        container.AddSingleton(p => GetConfig()!);

        container.TryAddSingleton(XTrace.Log);
        container.TryAddSingleton(typeof(ICacheProvider), typeof(CacheProvider));
    }

    [MemberNotNullWhen(true, nameof(_client))]
    private Boolean Valid()
    {
        if (Server.IsNullOrEmpty() || AppId.IsNullOrEmpty()) return false;

        if (_client == null)
        {
            var set = StarSetting.Current;
            var client = new AppClient(Server)
            {
                Factory = this,
                AppId = AppId,
                AppName = AppName,
                Secret = Secret,
                ClientId = ClientId,
                NodeCode = Local?.Info?.Code,
                Setting = set,

                Log = Log,
            };

#if !NET40
            // 设置全局定时调度器的时间提供者，借助服务器时间差，以获得更准确的时间。避免本地时间偏差导致定时任务执行时间不准确
            TimerScheduler.GlobalTimeProvider = new StarTimeProvider { Client = client };
#endif

            var p = Process.GetCurrentProcess();
            client.WriteInfoEvent("应用启动", $"pid={p.Id}, Name={p.GetProcessName()}, FileName={p.MainModule?.FileName}");

            _client = client;

            InitTracer();

            client.Tracer = _tracer;
            client.Open();

            // 注册StarServer环境变量，子进程共享
            Environment.SetEnvironmentVariable("StarServer", Server);
        }

        return true;
    }

    /// <summary>设置服务端地址</summary>
    public void SetServer(String server)
    {
        Server = server;

        // 不能重建_client，太多对象引用它，这里无法做到逐一更新
        //_client = null;

        var client = _client;
        if (client != null)
        {
            // 先注销再登录
            try
            {
                client.Logout(nameof(SetServer)).Wait();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            client.Server = server;
            client.Open();
        }

        // 注册StarServer环境变量，子进程共享
        Environment.SetEnvironmentVariable("StarServer", Server);
    }
    #endregion

    #region 监控中心
    private StarTracer? _tracer;
    /// <summary>监控中心</summary>
    public ITracer? Tracer
    {
        get
        {
            if (_tracer == null)
            {
                if (!Valid()) return null;

                InitTracer();
            }

            return _tracer;
        }
    }

    private void InitTracer()
    {
        if (Server.IsNullOrEmpty()) return;

        XTrace.WriteLine("星尘监控中心：ITracer采集并上报应用埋点数据，自动埋点Api接口、Http请求、数据库操作、Redis操作等。可用于监控系统健康状态，分析分布式系统的调用链和性能瓶颈。");

        var tracer = new StarTracer()
        {
            AppId = AppId,
            AppName = AppName,
            ClientId = ClientId,
            Client = _client,

            Log = Log
        };

        tracer.AttachGlobal();
        _tracer = tracer;
    }
    #endregion

    #region 配置中心
    private HttpConfigProvider? _config;
    /// <summary>配置中心。务必在数据库操作和生成雪花Id之前使用激活</summary>
    /// <remarks>
    /// 文档 https://newlifex.com/blood/stardust_configcenter
    /// </remarks>
    public IConfigProvider? Config
    {
        get
        {
            if (_config == null)
            {
                if (!Valid()) return null;

                XTrace.WriteLine("星尘配置中心：IConfigProvider集中管理配置，自动从配置中心加载配置数据并支持变更实时下发，包括XCode数据库连接字符串等。配置中心同时支持分配应用实例的唯一WorkerId，确保Snowflake算法能够生成绝对唯一的雪花Id");

                var config = new StarHttpConfigProvider
                {
                    Server = Server!,
                    AppId = AppId!,
                    //Secret = Secret,
                    ClientId = ClientId,
                    Client = _client,
                };
                config.Attach(_client);

                // 为了兼容旧版本，优先给它ApiHttpClient
                var ver = typeof(HttpConfigProvider).Assembly.GetName().Version;
                if (ver <= new Version(10, 10, 2024, 0701) && _client.Client is ApiHttpClient client)
                    config.Client = client;

                //!! 不需要默认加载，直到首次使用配置数据时才加载。因为有可能应用并不使用配置中心，仅仅是获取这个对象。避免网络不通时的报错日志
                //config.LoadAll();

                _config = config;
            }

            return _config;
        }
    }

    private IConfigProvider? _configProvider;
    /// <summary>设置本地配置提供者，该提供者将跟星尘配置结合到一起，形成复合配置提供者</summary>
    /// <param name="configProvider"></param>
    public void SetLocalConfig(IConfigProvider configProvider)
    {
        if (configProvider == null) return;

        var cfg = Config;
        if (cfg != null)
            _configProvider = new CompositeConfigProvider(configProvider, cfg);
        else
            _configProvider = configProvider;
    }

    /// <summary>获取复合配置提供者</summary>
    /// <returns></returns>
    public IConfigProvider? GetConfig() => _configProvider ?? Config ?? JsonConfigProvider.LoadAppSettings();
    #endregion

    #region 注册中心
    private Boolean _initService;
    /// <summary>注册中心，服务注册与发现</summary>
    public IRegistry? Service
    {
        get
        {
            if (!_initService)
            {
                if (!Valid()) return null;

                _initService = true;

                XTrace.WriteLine("星尘注册中心：IRegistry提供服务发现能力，可根据服务名自动获取所有提供者的节点地址，并创建调用服务的IApiClient客户端，并根据服务提供者的上线与下线自动新增或减少服务地址。");
            }

            return _client;
        }
    }

    /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址。单例，应避免频繁创建客户端</summary>
    /// <param name="serviceName"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public IApiClient CreateForService(String serviceName, String? tag = null) => TaskEx.Run(() => CreateForServiceAsync(serviceName, tag)).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址。单例，应避免频繁创建客户端</summary>
    /// <param name="serviceName"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public Task<IApiClient> CreateForServiceAsync(String serviceName, String? tag = null) => Service!.CreateForServiceAsync(serviceName, tag);

    /// <summary>发布服务。异步发布，屏蔽异常</summary>
    /// <remarks>即使发布失败，也已经加入队列，后续会定时发布</remarks>
    /// <param name="serviceName">服务名</param>
    /// <param name="address">服务地址</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    public Task<PublishServiceInfo> RegisterAsync(String serviceName, String address, String? tag = null, String? health = null)
    {
        return Task.Run(() =>
        {
            try
            {
                return Service!.RegisterAsync(serviceName, address, tag, health);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                return null;
            }
        });
    }

    /// <summary>消费得到服务地址信息</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="minVersion">最小版本</param>
    /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
    /// <returns></returns>
    public Task<String[]> ResolveAddressAsync(String serviceName, String? minVersion = null, String? tag = null) => Service!.ResolveAddressAsync(serviceName, minVersion, tag);
    #endregion

    #region 其它
    /// <summary>发送节点命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="nodeCode"></param>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <param name="startTime"></param>
    /// <param name="expire"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public Task<Int32> SendNodeCommand(String nodeCode, String command, String? argument = null, Int32 startTime = 0, Int32 expire = 3600, Int32 timeout = 5)
    {
        if (!Valid()) return Task.FromResult(-1);

        return _client.InvokeAsync<Int32>("Node/SendCommand", new CommandInModel
        {
            Code = nodeCode,
            Command = command,
            Argument = argument,
            StartTime = startTime,
            Expire = expire,
            Timeout = timeout
        });
    }

    /// <summary>发送应用命令。通知应用刷新配置信息和服务信息等</summary>
    /// <param name="appId"></param>
    /// <param name="clientId"></param>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <param name="startTime"></param>
    /// <param name="expire"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public Task<Int32> SendAppCommand(String appId, String clientId, String command, String? argument, Int32 startTime = 0, Int32 expire = 3600, Int32 timeout = 5)
    {
        if (!Valid()) return Task.FromResult(-1);

        var code = appId;
        if (!clientId.IsNullOrEmpty()) code = $"{code}@{clientId}";

        return _client.InvokeAsync<Int32>("App/SendCommand", new CommandInModel
        {
            Code = code,
            Command = command,
            Argument = argument,
            StartTime = startTime,
            Expire = expire,
            Timeout = timeout
        });
    }

    /// <summary>设置看门狗超时时间</summary>
    /// <param name="timeout">超时时间，单位秒。0表示关闭看门狗</param>
    /// <returns></returns>
    public Boolean SetWatchdog(Int32 timeout)
    {
        if (!Valid()) return false;

        var client = _client;
        if (client == null) return false;

        client.WatchdogTimeout = timeout;

        return true;
    }
    #endregion

    #region 日志
    /// <summary>日志。默认 XTrace.Log</summary>
    public ILog Log { get; set; } = XTrace.Log;
    #endregion
}