using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Threading;
using Stardust;
using Stardust.Monitors;

namespace StarAgent
{
    class Program
    {
        static void Main(String[] args) => new MyService().Main(args);
    }

    /// <summary>服务类。名字可以自定义</summary>
    class MyService : ServiceBase
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
        }

        TimerX _timer;
        StarClient _Client;
        ServiceManager _Manager;
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
            var tracer = new StarTracer
            {
                AppId = "StarAgent",
                AppSecret = null,
                Client = client,
                Log = XTrace.Log
            };
            DefaultTracer.Instance = tracer;
            client.Tracer = tracer;

            // 使用跟踪
            client.UseTrace();

            // 可能需要多次尝试
            _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };

            _Client = client;
        }

        private void TryConnectServer(Object state)
        {
            var client = state as StarClient;
            var set = Setting.Current;
            client.Login().Wait();
            CheckUpgrade(client, set.Channel);

            // 登录成功，销毁定时器
            //TimerX.Current.Period = 0;
            _timer.TryDispose();
            _timer = null;
        }

        /// <summary>服务启动</summary>
        /// <remarks>
        /// 安装Windows服务后，服务启动会执行一次该方法。
        /// 控制台菜单按5进入循环调试也会执行该方法。
        /// </remarks>
        protected override void StartWork(String reason)
        {
            var set = Setting.Current;

            StartClient();

            // 应用服务管理
            _Manager = new ServiceManager
            {
                Services = set.Services,

                Log = XTrace.Log,
            };
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

            _Manager.Stop(reason);
            //_Manager.TryDispose();

            _Client.TryDispose();
            _Client = null;
        }

        private void CheckUpgrade(StarClient client, String channel)
        {
            // 检查更新
            var ur = client.Upgrade(channel).Result;
            if (ur != null)
            {
                var rs = client.ProcessUpgrade(ur);

                // 强制更新时，马上重启
                if (rs && ur.Force)
                {
                    StopWork("Upgrade");

                    var p = Process.GetCurrentProcess();
                    p.Close();
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

            foreach (var pi in mi.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                XTrace.WriteLine("{0}:\t{1}", pi.Name, mi.GetValue(pi));
            }
        }
    }
}