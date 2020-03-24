using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Threading;
using Stardust;

namespace StarAgent2
{
    class Program
    {
        static void Main(string[] args)
        {
            XTrace.UseConsole();

            XTrace.WriteLine("FullPath:{0}", ".".GetFullPath());
            XTrace.WriteLine("BasePath:{0}", ".".GetBasePath());
            XTrace.WriteLine("TempPath:{0}", Path.GetTempPath());

            var mi = MachineInfo.Current ?? MachineInfo.RegisterAsync().Result;

            foreach (var pi in mi.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                XTrace.WriteLine("{0}:\t{1}", pi.Name, mi.GetValue(pi));
            }

            var set = StarAgent.Setting.Current;

            StartClient();

            // 应用服务管理
            _Manager = new ServiceManager
            {
                Services = set.Services,

                Log = XTrace.Log,
            };
            _Manager.Start();

            Thread.Sleep(-1);
        }

        private static TimerX _timer;
        private static StarClient _Client;
        private static ServiceManager _Manager;
        private static void StartClient()
        {
            var set = StarAgent.Setting.Current;
            var server = set.Server;
            if (server.IsNullOrEmpty()) return;

            XTrace.WriteLine("初始化服务端地址：{0}", server);

            var client = new StarClient(server)
            {
                Code = Environment.MachineName,
                Secret = Environment.MachineName,
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

            // 可能需要多次尝试
            _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };

            _Client = client;
        }

        private static void TryConnectServer(Object state)
        {
            var client = state as StarClient;
            var set = StarAgent.Setting.Current;
            //Task.Run(client.Login).ContinueWith(t => CheckUpgrade(client, set.Channel));
            client.Login().Wait();
            CheckUpgrade(client, set.Channel);

            // 登录成功，销毁定时器
            //TimerX.Current.Period = 0;
            _timer.TryDispose();
            _timer = null;
        }

        private static void CheckUpgrade(StarClient client, String channel)
        {
            // 检查更新
            var ur = client.Upgrade(channel).Result;
            if (ur != null)
            {
                var rs = client.ProcessUpgrade(ur);

                // 强制更新时，马上重启
                if (rs && ur.Force)
                {
                    var p = Process.GetCurrentProcess();
                    p.Close();
                    p.Kill(true);
                }
            }
        }
    }
}