using System.Net.NetworkInformation;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust;
using Stardust.Deployment;
using Stardust.Managers;
using Stardust.Models;
using Stardust.Plugins;
using Stardust.Services;
using Host = NewLife.Agent.Host;
using IHost = NewLife.Agent.IHost;
using Upgrade = Stardust.Web.Upgrade;

namespace StarAgent;

internal class Program
{
    private static void Main(String[] args)
    {
        if ("-upgrade".EqualIgnoreCase(args)) Thread.Sleep(5_000);

        var set = StarSetting.Current;
        if (set.IsNew)
        {
#if DEBUG
            set.Server = "http://localhost:6600";
#endif

            set.Save();
        }

        var svc = new MyService
        {
            StarSetting = set,
            AgentSetting = Setting.Current,
            Log = XTrace.Log,
        };

        // 处理 -server 参数，建议在-start启动时添加
        svc.SetServer(args);

        // Zip发布
        if (svc.RunZipDeploy(args)) return;

        svc.Main(args);
    }
}

/// <summary>服务类。名字可以自定义</summary>
internal class MyService : ServiceBase, IServiceProvider
{
    public StarSetting StarSetting { get; set; }

    public Setting AgentSetting { get; set; }

    /// <summary>宿主服务提供者</summary>
    public IServiceProvider Provider { get; set; }

    public MyService()
    {
        ServiceName = "StarAgent";

        // 注册菜单，在控制台菜单中按 t 可以执行Test函数，主要用于临时处理数据
        AddMenu('s', "使用星尘", UseStarServer);
        AddMenu('t', "服务器信息", ShowMachineInfo);
        AddMenu('w', "测试微服务", UseMicroService);

        MachineInfo.RegisterAsync();

        //// 定时重启
        //var set2 = NewLife.Agent.Setting.Current;
        //if (set2.AutoRestart == 0)
        //{
        //    set2.AutoRestart = 24 * 60;
        //    set2.Save();
        //}
    }

    private ApiServer _server;
    private TimerX _timer;
    private StarClient _Client;
    private StarFactory _factory;
    private ServiceManager _Manager;
    private PluginManager _PluginManager;
    private String _lastVersion;

    #region 调度核心
    /// <summary>服务启动</summary>
    /// <remarks>
    /// 安装Windows服务后，服务启动会执行一次该方法。
    /// 控制台菜单按5进入循环调试也会执行该方法。
    /// </remarks>
    protected override void StartWork(String reason)
    {
        var set = AgentSetting;

        StartFactory();

        // 应用服务管理
        _Manager = new ServiceManager
        {
            //Services = set.Services,
            Delay = set.Delay,

            Tracer = _factory?.Tracer,
            Log = XTrace.Log,
        };
        _Manager.SetServices(set.Services);
        _Manager.ServiceChanged += OnServiceChanged;

        // 插件管理器
        var pm = _PluginManager = new PluginManager
        {
            Identity = "StarAgent",
            Provider = this,

            Log = XTrace.Log,
        };

        // 监听端口，用于本地通信
        if (set.LocalPort > 0)
        {
            //var uri = new NetUri(set.LocalServer);
            try
            {
                // 必须支持Udp，因为需要支持局域网广播搜索功能
                var svr = new ApiServer(set.LocalPort)
                {
                    Tracer = _factory?.Tracer,
                    Log = XTrace.Log
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
                svr.Start();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        // 启动星尘客户端，连接服务端
        StartClient();

        _Manager.Start();
        Setting.Provider.Changed += OnSettingChanged;

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

        base.StartWork(reason);
    }

    /// <summary>服务管理线程</summary>
    /// <param name="data"></param>
    protected override void DoCheck(Object data)
    {
        OnSettingChanged(null, null);

        base.DoCheck(data);
    }

    private void OnSettingChanged(Object sender, EventArgs eventArgs)
    {
        // 支持动态更新
        //_Manager.Services = AgentSetting.Services;
        _Manager.SetServices(AgentSetting.Services);
    }

    private void OnServiceChanged(Object sender, EventArgs eventArgs)
    {
        // 服务改变时，保存到配置文件
        var set = AgentSetting;
        set.Services = _Manager.Services;
        set.Save();
    }

    /// <summary>服务停止</summary>
    /// <remarks>
    /// 安装Windows服务后，服务停止会执行该方法。
    /// 控制台菜单按5进入循环调试，任意键结束时也会执行该方法。
    /// </remarks>
    protected override void StopWork(String reason)
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

        Setting.Provider.Changed -= OnSettingChanged;
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
        var client = new StarClient(server)
        {
            Code = set.Code,
            Secret = set.Secret,
            ProductCode = "StarAgent",
            Log = XTrace.Log,

            //Manager = _Manager,
        };

        // 登录后保存证书
        client.OnLogined += (s, e) =>
        {
            var inf = client.Info;
            if (inf != null && !inf.Code.IsNullOrEmpty())
            {
                set.Code = inf.Code;
                set.Secret = inf.Secret;
                set.Save();
            }
        };

        // APM埋点。独立应用名
        client.Tracer = _factory?.Tracer;

        _Manager.Attach(client);

        // 使用跟踪
        client.UseTrace();

        _Client = client;

        // 可能需要多次尝试
        _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };
    }

    public void StartFactory()
    {
        if (_factory == null)
        {
            var server = StarSetting.Server;
            if (!server.IsNullOrEmpty()) _factory = new StarFactory(server, "StarAgent", null);
        }
    }

    private async Task TryConnectServer(Object state)
    {
        if (!NetworkInterface.GetIsNetworkAvailable() || AgentInfo.GetIps().IsNullOrEmpty())
        {
            WriteLog("网络不可以，延迟连接服务器");
            return;
        }

        var client = state as StarClient;

        try
        {
            await client.Login();
            //await CheckUpgrade(client);
        }
        catch (Exception ex)
        {
            // 登录报错后，加大定时间隔，输出简单日志
            _timer.Period = 30_000;

            Log?.Error(ex.Message);
        }

        _timer.TryDispose();
        _timer = new TimerX(CheckUpgrade, null, 5_000, 600_000) { Async = true };

        client.RegisterCommand("node/upgrade", s => _timer.SetNext(-1));
    }

    private async Task CheckUpgrade(Object data)
    {
        var client = _Client;

        // 运行过程中可能改变配置文件的通道
        var channel = AgentSetting.Channel;
        var ug = new Upgrade { Log = XTrace.Log };

        // 去除多余入口文件
        ug.Trim("StarAgent");

        // 检查更新
        var ur = await client.Upgrade(channel);
        if (ur != null && ur.Version != _lastVersion)
        {
            client.WriteInfoEvent("Upgrade", $"准备从[{_lastVersion}]更新到[{ur.Version}]，开始下载 {ur.Source}");
            try
            {
                ug.Url = client.BuildUrl(ur.Source);
                await ug.Download();

                // 检查文件完整性
                var checkHash = ug.CheckFileHash(ur.FileHash);
                if (!ur.FileHash.IsNullOrEmpty() && !checkHash)
                {
                    client.WriteInfoEvent("Upgrade", "下载完成，哈希校验失败");
                }
                else
                {
                    client.WriteInfoEvent("Upgrade", "下载完成，准备覆盖文件");

                    // 执行更新，解压缩覆盖文件
                    var rs = ug.Update();
                    if (rs && !ur.Executor.IsNullOrEmpty()) ug.Run(ur.Executor);
                    _lastVersion = ur.Version;

                    // 去除多余入口文件
                    ug.Trim("StarAgent");

                    // 强制更新时，马上重启
                    if (rs && ur.Force)
                    {
                        // 以服务方式运行时，重启服务，否则采取拉起进程的方式
                        if (Host is Host host && host.InService)
                        {
                            //rs = Host.Restart("StarAgent");
                            // 使用外部命令重启服务
                            rs = ug.Run("StarAgent", "-restart -upgrade");

                            client.WriteInfoEvent("Upgrade", "强制更新完成，准备重启后台服务");

                            //!! 这里不需要自杀，外部命令重启服务会结束当前进程
                        }
                        else
                        {
                            // 重新拉起进程
                            rs = ug.Run("StarAgent", "-run -upgrade");

                            if (rs)
                            {
                                StopWork("Upgrade");

                                client.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，准备退出当前进程");

                                ug.KillSelf();
                            }
                            else
                            {
                                client.WriteInfoEvent("Upgrade", "强制更新完成，但拉起新进程失败");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                client.WriteErrorEvent("Upgrade", ex.Message);
            }
        }
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

    public void UseStarServer()
    {
        var set = StarSetting;
        if (!set.Server.IsNullOrEmpty()) Console.WriteLine("服务端：{0}", set.Server);

        Console.WriteLine("请输入新的服务端：");

        var addr = Console.ReadLine();
        if (addr.IsNullOrEmpty()) addr = "http://star.newlifex.com:6600";

        set.Server = addr;
        set.Save();

        WriteLog("服务端修改为：{0}", addr);
    }

    public void ShowMachineInfo()
    {
        XTrace.WriteLine("FullPath:{0}", ".".GetFullPath());
        XTrace.WriteLine("BasePath:{0}", ".".GetBasePath());
        XTrace.WriteLine("TempPath:{0}", Path.GetTempPath());

        var mi = MachineInfo.Current ?? MachineInfo.RegisterAsync().Result;
        mi.Refresh();

        foreach (var pi in mi.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = mi.GetValue(pi);
            if (pi.Name.EndsWithIgnoreCase("Memory"))
                val = val.ToLong().ToGMK();
            else if (pi.Name.EndsWithIgnoreCase("Rate", "Battery"))
                val = val.ToDouble().ToString("p2");

            XTrace.WriteLine("{0}:\t{1}", pi.Name, val);
        }

        // 网络信息
        XTrace.WriteLine("NetworkAvailable:{0}", NetworkInterface.GetIsNetworkAvailable());
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            //if (item.OperationalStatus != OperationalStatus.Up) continue;
            if (item.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            XTrace.WriteLine("{0} {1} {2}", item.NetworkInterfaceType, item.OperationalStatus, item.Name);
            XTrace.WriteLine("\tDescription:\t{0}", item.Description);
            XTrace.WriteLine("\tMac:\t{0}", item.GetPhysicalAddress().GetAddressBytes().ToHex("-"));
            var ipp = item.GetIPProperties();
            if (ipp != null && ipp.UnicastAddresses.Any(e => e.Address.IsIPv4()))
            {
                XTrace.WriteLine("\tIP:\t{0}", ipp.UnicastAddresses.Where(e => e.Address.IsIPv4()).Join(",", e => e.Address));
                if (ipp.GatewayAddresses.Any(e => e.Address.IsIPv4()))
                    XTrace.WriteLine("\tGateway:{0}", ipp.GatewayAddresses.Where(e => e.Address.IsIPv4()).Join(",", e => e.Address));
                if (ipp.DnsAddresses.Any(e => e.IsIPv4()))
                    XTrace.WriteLine("\tDns:\t{0}", ipp.DnsAddresses.Where(e => e.IsIPv4()).Join());
            }
        }
    }

    private String _lastService;
    public void UseMicroService()
    {
        if (_lastService.IsNullOrEmpty())
            Console.WriteLine("请输入要测试的微服务名称：");
        else
            Console.WriteLine("请输入要测试的微服务名称（{0}）：", _lastService);

        var serviceName = Console.ReadLine();
        if (serviceName.IsNullOrEmpty()) serviceName = _lastService;
        if (serviceName.IsNullOrEmpty()) return;

        _lastService = serviceName;

        StartFactory();

        var models = _factory.Service.ResolveAsync(serviceName).Result;
        //if (models == null) models = _factory.Dust.ResolveAsync(new ConsumeServiceInfo { ServiceName = serviceName }).Result;

        Console.WriteLine(models.ToJson(true));
    }

    /// <summary>设置服务器地址</summary>
    /// <param name="args"></param>
    public void SetServer(String[] args)
    {
        var set = StarSetting;

        // 处理 -server 参数，建议在-start启动时添加
        if (args != null && args.Length > 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-server") && i + 1 < args.Length)
                {
                    var addr = args[i + 1];

                    set.Server = addr;
                    set.Save();

                    XTrace.WriteLine("服务端修改为：{0}", addr);
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

        var deploy = new ZipDeploy { Log = XTrace.Log };
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

        if (serviceType == typeof(ITracer)) return _factory.Tracer;

        return Provider?.GetService(serviceType);
    }
    #endregion
}