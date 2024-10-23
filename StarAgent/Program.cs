using System.Diagnostics;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using Stardust;
using Stardust.Deployment;
using Stardust.Managers;
using Stardust.Models;
using Stardust.Plugins;
using IHost = NewLife.Agent.IHost;

namespace StarAgent;

internal class Program
{
    private static void Main(String[] args)
    {
#if !NET40
        // 用不到的配置不要输出
        Runtime.CreateConfigOnMissing = false;
#endif
        XTrace.UseConsole();

        if ("-upgrade".EqualIgnoreCase(args))
        {
            XTrace.WriteLine("更新模式启动，等待{0}秒", 5_000);
            Thread.Sleep(5_000);
        }
        else if ("-delay".EqualIgnoreCase(args))
        {
            XTrace.WriteLine("延迟启动，等待{0}秒", 5_000);
            Thread.Sleep(5_000);
        }

        var set = StarSetting.Current;
        if (set.IsNew)
        {
#if DEBUG
            set.Server = "http://localhost:6600";
#endif

            set.Save();
        }

        // 用户模式存储配置，方便服务模式读取。因为服务模式无法读取用户和分辨率等信息
        var set2 = StarAgentSetting.Current;
        if (!"-s".EqualIgnoreCase(args)) ThreadPoolX.QueueUserWorkItem(() => LoadUser(set2));

        var svc = new MyService
        {
            StarSetting = set,
            AgentSetting = set2,
            UseAutorun = set2.UseAutorun,

            Log = XTrace.Log,
        };

        // 处理 -server 参数，建议在-start启动时添加
        svc.SetServer(args);

#if !NET40
        // 注册增强版机器信息提供者
        MachineInfo.Provider = new MachineInfoProvider();
#endif

        // Zip发布
        if (svc.RunZipDeploy(args)) return;

        // 修复
        if ("-repair".EqualIgnoreCase(args))
        {
            svc.Repair();
            return;
        }

        svc.Main(args);
    }

    /// <summary>用户模式存储配置，方便服务模式读取。因为服务模式无法读取用户和分辨率等信息</summary>
    /// <param name="set"></param>
    private static void LoadUser(StarAgentSetting set)
    {
        set.UserName = Environment.UserName;

        var info = new NodeInfo();
        if (Runtime.Windows) StarClient.FillOnWindows(info);
        if (Runtime.Linux) StarClient.FillOnLinux(info);

        set.Dpi = info.Dpi;
        set.Resolution = info.Resolution;

        set.Save();
    }
}

/// <summary>服务类。名字可以自定义</summary>
internal class MyService : ServiceBase, IServiceProvider
{
    public StarSetting StarSetting { get; set; }

    public StarAgentSetting AgentSetting { get; set; }

    /// <summary>宿主服务提供者</summary>
    public IServiceProvider Provider { get; set; }

    private IObjectContainer _container;

    public MyService()
    {
        ServiceName = "StarAgent";

        //// 控制应用服务。有些问题，只能控制当前进程管理的服务，而不能管理后台服务管理的应用
        //AddMenu('z', "启动所有应用服务", () => _Manager?.StartAll());
        //AddMenu('x', "停止所有应用服务", () => _Manager?.StopAll("菜单控制"));

        MachineInfo.RegisterAsync();

        _container = ObjectContainer.Current;
        Provider = ObjectContainer.Provider;
    }

    #region 服务控制
    protected override void Init()
    {
        base.Init();

        // 自定义Systemd工作模式
        if (Host is Systemd sd)
        {
            var set = sd.Setting;
            set.ServiceName = ServiceName;

            // 无限重试启动
            set.StartLimitInterval = 0;

            // 只杀主进程StarAgent，避免误杀应用进程
            set.KillMode = "process";
            set.KillSignal = "SIGINT";

            // 禁止被OOM杀死
            set.OOMScoreAdjust = -1000;

            // 检查并修正旧版KillMode
            FixKillMode(set);
        }
    }

    private void FixKillMode(SystemdSetting set)
    {
        var servicePath = typeof(Systemd).GetValue("ServicePath") as String;
        if (!servicePath.IsNullOrEmpty())
        {
            var file = servicePath.CombinePath($"{set.ServiceName}.service");
            if (File.Exists(file) && !File.ReadAllText(file).Contains("KillMode"))
            {
                WriteLog("旧版service文件，修正KillMode");

                var exe = Process.GetCurrentProcess().MainModule.FileName;

                // 兼容dotnet
                var args = Environment.GetCommandLineArgs();
                if (args.Length >= 1)
                {
                    var fileName = Path.GetFileName(exe);
                    if (exe.Contains(' ')) exe = $"\"{exe}\"";

                    var dll = args[0].GetFullPath();
                    if (dll.Contains(' ')) dll = $"\"{dll}\"";

                    if (fileName.EqualIgnoreCase("dotnet", "dotnet.exe"))
                        exe += " " + dll;
                    else if (fileName.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
                        exe = dll;
                }

                Host.Install(ServiceName, DisplayName, exe, "-s", Description);
            }
        }
    }
    #endregion

    private ApiServer _server;
    private TimerX _timer;
    internal StarClient _Client;
    internal StarFactory _factory;
    private ServiceManager _Manager;
    private PluginManager _PluginManager;
    private String _lastVersion;

    #region 调度核心
    /// <summary>服务启动</summary>
    /// <remarks>
    /// 安装Windows服务后，服务启动会执行一次该方法。
    /// 控制台菜单按5进入循环调试也会执行该方法。
    /// </remarks>
    public override void StartWork(String reason)
    {
        var set = AgentSetting;

        StartFactory();

        // 应用服务管理
        var manager = new ServiceManager
        {
            Delay = set.Delay,

            Tracer = _factory?.Tracer,
            Log = XTrace.Log,
        };
        manager.SetServices(set.Services);
        manager.ServiceChanged += OnServiceChanged;

        _Manager = manager;
        _container.AddSingleton(manager);

        // 插件管理器
        var pm = _PluginManager = new PluginManager
        {
            Identity = "StarAgent",
            Provider = this,

            Log = XTrace.Log,
        };
        _container.AddSingleton(pm);

        // 监听端口，用于本地通信
        if (set.LocalPort > 0) StartLocalServer(set.LocalPort);

        // 启动星尘客户端，连接服务端
        StartClient();

        _Manager.Start();
        StarAgentSetting.Provider.Changed += OnSettingChanged;

        // 启动插件
        WriteLog("启动插件[{0}]", pm.Identity);
        pm.Load();
        pm.Init();
        foreach (var item in pm.Plugins)
        {
            if (item is IAgentPlugin plugin)
            {
                try
                {
                    plugin.Start();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }
        }

        if (_Client != null) _Client.Plugins = pm.Plugins.Select(e => e.GetType().Name.TrimEnd("Plugin")).ToArray();

        // 辅助任务清理数据
        ThreadPoolX.QueueUserWorkItem(Fix);

        base.StartWork(reason);
    }

    private void OnSettingChanged(Object sender, EventArgs eventArgs)
    {
        WriteLog("重新加载应用服务");

        // 支持动态更新
        //_Manager.Services = AgentSetting.Services;
        _Manager.SetServices(AgentSetting.Services);
    }

    private void OnServiceChanged(Object sender, EventArgs eventArgs)
    {
        // 服务改变时，保存到配置文件
        var set = AgentSetting;
        set.Services = _Manager.Services.Select(e => e.Clone()).ToArray();
        set.Save();
    }

    /// <summary>服务停止</summary>
    /// <remarks>
    /// 安装Windows服务后，服务停止会执行该方法。
    /// 控制台菜单按5进入循环调试，任意键结束时也会执行该方法。
    /// </remarks>
    public override void StopWork(String reason)
    {
        base.StopWork(reason);

        // 停止插件
        WriteLog("停止插件[{0}]", _PluginManager.Identity);
        foreach (var item in _PluginManager.Plugins)
        {
            if (item is IAgentPlugin plugin)
            {
                try
                {
                    plugin.Stop(reason);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }
        }

        _timer.TryDispose();
        _timer = null;

        StarAgentSetting.Provider.Changed -= OnSettingChanged;
        _Manager.Stop(reason);
        //_Manager.TryDispose();

        _Client?.Logout(reason);
        //_Client.TryDispose();
        _Client = null;

        _factory = null;

        _server.TryDispose();
        _server = null;
    }
    #endregion

    #region 客户端启动
    public void StartClient()
    {
        var server = StarSetting.Server;
        if (server.IsNullOrEmpty()) return;

        WriteLog("初始化服务端地址：{0}", server);

        var set = AgentSetting;
        var client = new MyStarClient
        {
            Name = "Node",
            Server = server,
            Code = set.Code,
            Secret = set.Secret,
            ProductCode = "StarAgent",

            Log = XTrace.Log,

            //Manager = _Manager,
            //Host = Host,
            Service = this,
            AgentSetting = AgentSetting,
        };

        // 登录后保存证书
        client.OnLogined += (s, e) =>
        {
            var inf = e.Response;
            if (inf != null && !inf.Code.IsNullOrEmpty())
            {
                set.Code = inf.Code;
                set.Secret = inf.Secret;
                set.Save();
            }
        };

        // 服务迁移
        client.OnMigration += (s, e) =>
        {
            var setStar = StarSetting;
            var svr = e.NewServer;
            if (!svr.IsNullOrEmpty() && !svr.EqualIgnoreCase(setStar.Server))
            {
                setStar.Server = svr;
                setStar.Save();

                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
            }
        };

        // APM埋点。独立应用名
        client.Tracer = _factory?.Tracer;

        _Manager.Attach(client);

        //// 使用跟踪
        //client.UseTrace();

        _Client = client;
        _container.AddSingleton(client);
        _container.AddSingleton<ICommandClient>(client);
        _container.AddSingleton<IEventProvider>(client);

        // 可能需要多次尝试
        client.Open();
    }

    public void StartFactory()
    {
        if (_factory == null)
        {
            var server = StarSetting.Server;
            if (!server.IsNullOrEmpty())
            {
                _factory = new StarFactory(server, "StarAgent", null);
                _factory.Register(_container);

                // 激活配置中心，获取PluginServer
                var config = _factory.GetConfig();
                if (config != null) ThreadPoolX.QueueUserWorkItem(() => config.LoadAll());
            }
        }
    }

    public void StartLocalServer(Int32 port)
    {
        try
        {
            // 必须支持Udp，因为需要支持局域网广播搜索功能
            var svr = new ApiServer(port)
            {
                ReuseAddress = true,
                Tracer = _factory?.Tracer,
                Log = XTrace.Log,
                EncoderLog = XTrace.Log,
            };
            svr.Register(new StarService
            {
                Provider = this,
                //Host = Host,
                Manager = _Manager,
                //PluginManager = _PluginManager,
                StarSetting = StarSetting,
                AgentSetting = AgentSetting,
                Log = XTrace.Log
            }, null);

            _server = svr;
            _container.AddSingleton(svr);

            svr.Start();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }
    #endregion

    #region 自动更新
    /// <summary>修复模式启动StarAgent，以修复正式的StarAgent</summary>
    public void Repair()
    {
        WriteLog("修复模式启动StarAgent，以修复正式的StarAgent");

        // 校验当前程序的MD5，避免重复执行
        var mf = "data/repair.md5".GetBasePath();
        if (File.Exists(mf))
        {
            var old = File.ReadAllText(mf);
            var md5 = Assembly.GetExecutingAssembly().Location.AsFile().MD5().ToHex();
            if (old == md5) return;

            File.WriteAllText(mf, md5);
        }

        // 查找正式目录
        DirectoryInfo di = null;

        // 初始化Host
        Init();

        var cfg = Host?.QueryConfig(ServiceName);
        if (cfg != null && !cfg.FilePath.IsNullOrEmpty())
        {
            var str = cfg.FilePath;
            var p = str.IndexOf(' ');
            if (p > 0) str = str.Substring(0, p);

            di = str.AsFile().Directory;
        }

        if (di == null || !di.Exists) di = "../agent".GetBasePath().AsDirectory();
        if (!di.Exists) di = "../Agent".GetBasePath().AsDirectory();
        if (!di.Exists) di = "../staragent".GetBasePath().AsDirectory();
        if (!di.Exists) di = "../StarAgent".GetBasePath().AsDirectory();
        if (!di.Exists)
        {
            WriteLog("目标不存在 {0}", di.FullName);

            var cur = "./".GetBasePath().TrimEnd('/', '\\');
            WriteLog("当前目录 {0}", cur);

            // 遍历所有子目录，但跳过当前
            foreach (var item in "../".GetBasePath().AsDirectory().GetDirectories())
            {
                WriteLog("检查 {0}", item.FullName);
                if (item.FullName.TrimEnd('/', '\\').EqualIgnoreCase(cur)) continue;

                var fi = item.GetFiles("StarAgent.dll").FirstOrDefault();
                if (fi != null && fi.Exists)
                {
                    di = fi.Directory;
                    break;
                }
            }
        }

        if (!di.Exists)
        {
            WriteLog("未能找到正式StarAgent所在，修复失败！");
            Thread.Sleep(1_000);
            return;
        }

        WriteLog("正式StarAgent所在目录：{0}", di.FullName);

        // 等待一会，拉起修复进程的进程，可能还有别的善后工作
        Thread.Sleep(5_000);

        WriteLog("停止服务……");
        //Init();
        Host.Stop(ServiceName);
        //Process.Start("net", $"stop {ServiceName}");
        Thread.Sleep(1_000);

        // 拷贝当前目录所有dll/exe/runtime.json到正式目录
        foreach (var fi in "./".GetBasePath().AsDirectory().GetAllFiles("*.dll;*.exe;*.runtimeconfig.json"))
        {
            try
            {
                WriteLog("复制 {0}", fi.Name);

                var dst = di.FullName.CombinePath(fi.Name);
                if (File.Exists(dst)) File.Move(dst, Path.GetTempFileName());

                fi.CopyTo(dst, true);
            }
            catch (Exception ex)
            {
                Log?.Error(ex.Message);
            }
        }

        WriteLog("启动服务……");
        Host.Start(ServiceName);
        //Process.Start("net", $"start {ServiceName}");
        Thread.Sleep(1_000);
    }
    #endregion

    #region 扩展功能
    protected override void ShowMenu()
    {
        base.ShowMenu();

        var set = StarSetting;
        if (!set.Server.IsNullOrEmpty()) Console.WriteLine("服务端：{0}", set.Server);
        Console.WriteLine();
    }

    public void SetServer(String[] args)
    {
        var set = StarSetting;
        var set2 = AgentSetting;

        // 处理 -server 参数，建议在-start启动时添加
        if (args != null && args.Length > 0)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i].EqualIgnoreCase("-server"))
                {
                    set.Server = args[i + 1];
                    set.Save();

                    XTrace.WriteLine("服务端修改为：{0}", set.Server);

                    break;
                }
                else if (args[i].EqualIgnoreCase("-project"))
                {
                    set2.Project = args[i + 1];
                    set2.Save();

                    XTrace.WriteLine("项目修改为：{0}", set2.Project);

                    break;
                }
            }
        }
    }

    /// <summary>运行Zip发布文件</summary>
    /// <param name="args"></param>
    /// <returns>是否成功运行</returns>
    public Boolean RunZipDeploy(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        var file = args.FirstOrDefault(e => e.EndsWithIgnoreCase(".zip"));
        if (file.IsNullOrEmpty()) return false;

        XTrace.WriteLine("开始运行Zip发布文件 {0}", file);

        var deploy = new ZipDeploy
        {
            Tracer = _factory?.Tracer,
            Log = XTrace.Log
        };
        if (!deploy.Parse(args)) return false;

        deploy.Execute();

        return true;
    }
    #endregion

    #region IServiceProvider 成员
    Object IServiceProvider.GetService(Type serviceType)
    {
        if (serviceType == typeof(ServiceManager)) return _Manager;
        if (serviceType == typeof(StarClient)) return _Client;
        if (serviceType == typeof(StarFactory)) return _factory;
        if (serviceType == typeof(ApiServer)) return _server;
        if (serviceType == typeof(ServiceBase)) return this;
        if (serviceType == typeof(IHost)) return Host;

        if (serviceType == typeof(ITracer)) return _factory?.Tracer;

        return Provider?.GetService(serviceType);
    }
    #endregion

    #region 辅助

    /// <summary>清理历史版本文件</summary>
    private void Fix()
    {
        foreach (var fi in "./".AsDirectory().GetFiles("*", SearchOption.TopDirectoryOnly))
        {
            var flag = false;
            if (fi.Name.EndsWithIgnoreCase(".deps.json", ".runtimeconfig.json") && !fi.Name.StartsWithIgnoreCase("StarAgent"))
                flag = true;
            else if (fi.Name.EndsWithIgnoreCase(".tar.gz") && fi.LastAccessTime.AddMonths(1) < DateTime.Now)
                flag = true;
            else if (fi.Name.EndsWithIgnoreCase(".exe") && !Runtime.Windows && !Runtime.Mono)
                flag = true;

            if (flag)
            {
                try
                {
                    _Client?.WriteInfoEvent("删除", fi.FullName);
                    XTrace.WriteLine("删除：{0}", fi.FullName);
                    fi.Delete();
                }
                catch (Exception ex)
                {
                    _Client?.WriteErrorEvent("删除", ex.Message);
                    XTrace.Log.Error(ex.Message);
                }
            }
        }

        var di = "./runtimes".AsDirectory();
        if (di.Exists)
        {
            try
            {
                _Client?.WriteInfoEvent("删除", di.FullName);
                XTrace.WriteLine("删除：{0}", di.FullName);
                di.Delete(true);
            }
            catch (Exception ex)
            {
                _Client?.WriteErrorEvent("删除", ex.Message);
                XTrace.Log.Error(ex.Message);
            }
        }
    }
    #endregion
}
