﻿using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Serialization;
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

        var set2 = StarAgentSetting.Current;
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

        //// 定时重启
        //var set2 = NewLife.Agent.Setting.Current;
        //if (set2.AutoRestart == 0)
        //{
        //    set2.AutoRestart = 24 * 60;
        //    set2.Save();
        //}

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
                WriteLog("旧版servic文件，修正KillMode");

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

    //protected override void OnShowMenu(IList<Menu> menus)
    //{
    //    var services = _Manager?.Services.Where(e => e.Enable).ToArray();
    //    if (services == null || services.Length == 0)
    //    {
    //        menus = menus.Where(e => e.Key != 'z' && e.Key != 'x').ToList();
    //    }
    //    else
    //    {
    //        var ss = services.Join(",", e => e.Name);

    //        var m = menus.FirstOrDefault(e => e.Key == 'z');
    //        if (m != null) m.Name = $"启动所有应用服务（{ss}）";

    //        m = menus.FirstOrDefault(e => e.Key == 'x');
    //        if (m != null) m.Name = $"停止所有应用服务（{ss}）";
    //    }

    //    base.OnShowMenu(menus);
    //}

    //protected override void ProcessCommand(String cmd, String[] args)
    //{
    //    // 如果是重启，则请求目标温柔退出
    //    if (cmd.EqualIgnoreCase("-restart"))
    //    {
    //        var p = StarHelper.GetProcessByName("StarAgent").FirstOrDefault();
    //        //p?.SafetyKill(0, 0);
    //        if (p != null)
    //        {
    //            p.SafetyKill();

    //            if (p.GetHasExited())
    //            {
    //                Host.Start(ServiceName);

    //                return;
    //            }
    //        }
    //    }

    //    base.ProcessCommand(cmd, args);
    //}
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
        manager.ServiceStarting += OnServiceStarting;
        manager.ServiceStoped += OnServiceStoped;
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

        base.StartWork(reason);
    }

    ///// <summary>服务管理线程</summary>
    ///// <param name="data"></param>
    //protected override void DoCheck(Object data)
    //{
    //    OnSettingChanged(null, null);

    //    base.DoCheck(data);
    //}

    private void OnSettingChanged(Object sender, EventArgs eventArgs)
    {
        WriteLog("重新加载应用服务");

        // 支持动态更新
        //_Manager.Services = AgentSetting.Services;
        _Manager.SetServices(AgentSetting.Services);
    }

    private void OnServiceStoped(Object sender, ServiceEventArgs e)
    {
        var ctrl = e.Controller;
        if (Runtime.Windows && ctrl?.Info?.UserName == "$")
        {
            // 插入桌面服务处理器
            ctrl.Handlers.Insert(0, new DesktopServiceHandler());
        }
    }

    private void OnServiceStarting(Object sender, ServiceEventArgs e) { }

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
        _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };
    }

    public void StartFactory()
    {
        if (_factory == null)
        {
            var server = StarSetting.Server;
            if (!server.IsNullOrEmpty())
            {
                _factory = new StarFactory(server, "StarAgent", null);
                _container.AddSingleton(_factory);

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

    private async Task TryConnectServer(Object state)
    {
        if (!NetworkInterface.GetIsNetworkAvailable() || AgentInfo.GetIps().IsNullOrEmpty())
        {
            WriteLog("网络不可达，延迟连接服务器");
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
            //_timer.Period = 30_000;
            if (_timer != null && _timer.Period < 30_000) _timer.Period += 5_000;

            Log?.Error(ex.Message);

            return;
        }

        _timer.TryDispose();
        _timer = new TimerX(CheckUpgrade, null, 5_000, 600_000) { Async = true };

        client.RegisterCommand("node/upgrade", s => _ = CheckUpgrade(s));
        client.RegisterCommand("node/restart", Restart);
        client.RegisterCommand("node/reboot", Reboot);
        client.RegisterCommand("node/setchannel", SetChannel);
    }

    /// <summary>重启应用服务</summary>
    private String Restart(String argument)
    {
        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            var ug = new Upgrade { Log = XTrace.Log };

            // 带有-s参数就算是服务中运行
            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());

            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
            if (inService || Host is DefaultHost host && host.InService)
            {
                // 使用外部命令重启服务
                var rs = ug.Run("StarAgent", "-restart -delay");

                //!! 这里不需要自杀，外部命令重启服务会结束当前进程
                return rs + "";
            }
            else
            {
                // 重新拉起进程
                var rs = ug.Run("StarAgent", "-run -delay");

                if (rs)
                {
                    StopWork("Upgrade");

                    ug.KillSelf();
                }

                return rs + "";
            }
        });

        return "success";
    }

    /// <summary>重启操作系统</summary>
    private String Reboot(String argument)
    {
        var dic = argument.IsNullOrEmpty() ? null : JsonParser.Decode(argument);
        var timeout = dic?["timeout"].ToInt();

        // 异步执行，让方法调用返回结果给服务端
        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            if (Runtime.Windows)
            {
                if (timeout > 0)
                    "shutdown".ShellExecute($"-r -t {timeout}");
                else
                    "shutdown".ShellExecute($"-r");

                Thread.Sleep(5000);
                "shutdown".ShellExecute($"-r -f");
            }
            else if (Runtime.Linux)
            {
                // 多种方式重启Linux，先使用温和的方式
                "systemctl".ShellExecute("reboot");

                Thread.Sleep(5000);
                "shutdown".ShellExecute("-r now");

                Thread.Sleep(5000);
                "reboot".ShellExecute();
            }
        });

        return "success";
    }

    /// <summary>设置通道</summary>
    /// <param name="argument"></param>
    /// <returns></returns>
    private String SetChannel(String argument)
    {
        if (argument.IsNullOrEmpty()) return "参数为空";

        var set = AgentSetting;
        set.Channel = argument;
        set.Save();

        return "success " + argument;
    }
    #endregion

    #region 自动更新
    private async Task CheckUpgrade(Object data)
    {
        if (!NetworkInterface.GetIsNetworkAvailable()) return;

        var client = _Client;
        using var span = client.Tracer?.NewSpan("CheckUpgrade", new { _lastVersion });

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
                    client.WriteInfoEvent("Upgrade", "下载完成，准备解压文件");
                    if (!ug.Extract())
                    {
                        client.WriteInfoEvent("Upgrade", "解压失败");
                    }
                    else
                    {
                        if (ur is UpgradeInfo ur2 && !ur2.Preinstall.IsNullOrEmpty())
                        {
                            client.WriteInfoEvent("Upgrade", "执行预安装脚本");

                            ug.Run(ur2.Preinstall);
                        }

                        client.WriteInfoEvent("Upgrade", "解压完成，准备覆盖文件");

                        // 执行更新，解压缩覆盖文件
                        var rs = ug.Update();
                        if (rs && !ur.Executor.IsNullOrEmpty()) ug.Run(ur.Executor);
                        _lastVersion = ur.Version;

                        // 去除多余入口文件
                        ug.Trim("StarAgent");

                        // 强制更新时，马上重启
                        if (rs && ur.Force)
                        {
                            // 带有-s参数就算是服务中运行
                            var inService = "-s".EqualIgnoreCase(Environment.GetCommandLineArgs());
                            var pid = Process.GetCurrentProcess().Id;

                            // 以服务方式运行时，重启服务，否则采取拉起进程的方式
                            if (inService || Host is DefaultHost host && host.InService)
                            {
                                client.WriteInfoEvent("Upgrade", "强制更新完成，准备重启后台服务！PID=" + pid);

                                //rs = Host.Restart("StarAgent");
                                // 使用外部命令重启服务
                                rs = ug.Run("StarAgent", "-restart -upgrade");

                                //!! 这里不需要自杀，外部命令重启服务会结束当前进程
                            }
                            else
                            {
                                // 重新拉起进程
                                rs = ug.Run("StarAgent", "-run -upgrade");

                                if (rs)
                                {
                                    StopWork("Upgrade");

                                    client.WriteInfoEvent("Upgrade", "强制更新完成，新进程已拉起，准备退出当前进程！PID=" + pid);

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
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                client.WriteErrorEvent("Upgrade", ex.ToString());
            }
        }
    }

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
}
