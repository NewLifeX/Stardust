using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust;
using Stardust.Models;

namespace StarAgent
{
    internal class Program
    {
        private static void Main(String[] args)
        {
            if ("-upgrade".EqualIgnoreCase(args)) Thread.Sleep(5_000);

            new MyService().Main(args);
        }
    }

    /// <summary>服务类。名字可以自定义</summary>
    internal class MyService : ServiceBase
    {
        public MyService()
        {
            ServiceName = "StarAgent";

            var set = Stardust.Setting.Current;
            if (set.IsNew)
            {
#if DEBUG
                set.Server = "http://localhost:6600";
#endif

                set.Save();
            }

            // 注册菜单，在控制台菜单中按 t 可以执行Test函数，主要用于临时处理数据
            AddMenu('s', "使用星尘", UseStarServer);
            AddMenu('t', "服务器信息", ShowMachineInfo);
            AddMenu('w', "测试微服务", UseMicroService);

            MachineInfo.RegisterAsync();

            // 处理 -server 参数
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 0)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].EqualIgnoreCase("-server") && i + 1 < args.Length)
                    {
                        var addr = args[i + 1];

                        set.Server = addr;
                        set.Save();

                        WriteLog("服务端修改为：{0}", addr);
                    }
                }
            }
        }

        private ApiServer _server;
        private TimerX _timer;
        private StarClient _Client;
        private StarFactory _factory;
        private ServiceManager _Manager;

        private void StartClient()
        {
            var set = Setting.Current;
            var server = Stardust.Setting.Current.Server;
            if (server.IsNullOrEmpty()) return;

            WriteLog("初始化服务端地址：{0}", server);

            var client = new StarClient(server)
            {
                Code = set.Code,
                Secret = set.Secret,
                Log = XTrace.Log,
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
            client.Tracer = _factory.Tracer;

            // 使用跟踪
            client.UseTrace();

            _Client = client;

            // 可能需要多次尝试
            _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };
        }

        private void StartFactory()
        {
            if (_factory == null)
            {
                var server = Stardust.Setting.Current.Server;
                _factory = new StarFactory(server, "StarAgent", null);
            }
        }

        private void TryConnectServer(Object state)
        {
            var client = state as StarClient;
            client.Login().Wait();
            CheckUpgrade(client);

            // 登录成功，销毁定时器
            //TimerX.Current.Period = 0;
            //_timer.TryDispose();
            //_timer = null;

            _timer.TryDispose();
            _timer = new TimerX(CheckUpgrade, null, 600_000, 600_000) { Async = true };
        }

        /// <summary>服务启动</summary>
        /// <remarks>
        /// 安装Windows服务后，服务启动会执行一次该方法。
        /// 控制台菜单按5进入循环调试也会执行该方法。
        /// </remarks>
        protected override void StartWork(String reason)
        {
            var set = Setting.Current;

            // 应用服务管理
            _Manager = new ServiceManager
            {
                Services = set.Services,

                Log = XTrace.Log,
            };

            // 监听端口，用于本地通信
            if (!set.LocalServer.IsNullOrEmpty())
            {
                try
                {
                    var svr = new ApiServer(new NetUri(set.LocalServer))
                    {
                        Log = XTrace.Log
                    };
                    svr.Register(new StarService
                    {
                        Service = this,
                        Host = Host,
                        Manager = _Manager,
                        Log = XTrace.Log
                    }, null);
                    svr.Start();

                    _server = svr;
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }

            StartFactory();

            // 启动星尘客户端，连接服务端
            StartClient();

            _Manager.Start();

            base.StartWork(reason);
        }

        /// <summary>服务管理线程</summary>
        /// <param name="data"></param>
        protected override void DoCheck(Object data)
        {
            // 支持动态更新
            _Manager.Services = Setting.Current.Services;

            base.DoCheck(data);
        }

        /// <summary>服务停止</summary>
        /// <remarks>
        /// 安装Windows服务后，服务停止会执行该方法。
        /// 控制台菜单按5进入循环调试，任意键结束时也会执行该方法。
        /// </remarks>
        protected override void StopWork(String reason)
        {
            base.StopWork(reason);

            _timer.TryDispose();
            _timer = null;

            _Manager.Stop(reason);
            //_Manager.TryDispose();

            _Client.TryDispose();
            _Client = null;

            _factory = null;

            _server.TryDispose();
            _server = null;
        }

        private void CheckUpgrade(Object data)
        {
            var client = _Client;

            // 运行过程中可能改变配置文件的通道
            var set = Setting.Current;
            var channel = set.Channel;

            // 检查更新
            var ur = client.Upgrade(channel).Result;
            if (ur != null)
            {
                var rs = client.ProcessUpgrade(ur);

                // 强制更新时，马上重启
                if (rs && ur.Force)
                {
                    StopWork("Upgrade");

                    // 重新拉起进程
                    var star = "";
                    if (Runtime.Windows)
                        star = "StarAgent.exe";
                    else if (Runtime.Linux)
                        star = "StarAgent";
                    if (!star.IsNullOrEmpty())
                    {
                        XTrace.WriteLine("强制升级，拉起进程 {0} -run -upgrade", star.GetFullPath());
                        Process.Start(star.GetFullPath(), "-run -upgrade");
                    }

                    Environment.Exit(0);
                    var p = Process.GetCurrentProcess();
                    p.Kill();
                }
            }
        }

        protected override void ShowMenu()
        {
            base.ShowMenu();

            var set = Stardust.Setting.Current;
            if (!set.Server.IsNullOrEmpty()) Console.WriteLine("服务端：{0}", set.Server);
            Console.WriteLine();
        }

        public void UseStarServer()
        {
            var set = Stardust.Setting.Current;

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

            var models = _factory.Dust.Resolve(serviceName);
            if (models == null) models = _factory.Dust.ResolveAsync(new ConsumeServiceInfo { ServiceName = serviceName }).Result;

            Console.WriteLine(models.ToJson(true));
        }
    }
}