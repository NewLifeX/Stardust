using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Threading;
using Stardust;

namespace StarAgent
{
    class Program
    {
        static void Main(String[] args) => new MyService().Main();
    }

    /// <summary>服务类。名字可以自定义</summary>
    class MyService : AgentServiceBase<MyService>
    {
        public MyService()
        {
            ServiceName = "StarAgent";

            var set = Setting.Current;
            if (set.IsNew)
            {
#if DEBUG
                set.Server = "http://localhost:6600";
#endif

                set.Save();
            }

            // 注册菜单，在控制台菜单中按 t 可以执行Test函数，主要用于临时处理数据
            AddMenu('t', "测试", Test);
        }

        TimerX _timer;
        StarClient _Client;
        ServiceManager _Manager;
        private void StartClient()
        {
            var set = Setting.Current;
            var server = set.Server;
            if (server.IsNullOrEmpty()) return;

            WriteLog("初始化服务端地址：{0}", server);

            var client = new StarClient(server)
            {
                Code = Environment.MachineName,
                Secret = Environment.MachineName,
                Log = XTrace.Log,
            };

            // 可能需要多次尝试
            _timer = new TimerX(TryConnectServer, client, 0, 5_000) { Async = true };

            _Client = client;
        }

        private void TryConnectServer(Object state)
        {
            var client = state as StarClient;
            var set = Setting.Current;
            //Task.Run(client.Login).ContinueWith(t => CheckUpgrade(client, set.Channel));
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
                }
            }
        }

        #region 自动发现服务端
        //private ApiClient _udp;
        //private TimerX _udp_timer;
        //private void StartDiscover()
        //{
        //    var tc = new ApiClient("udp://255.255.255.255:6666")
        //    {
        //        UsePool = false,
        //        Log = XTrace.Log,
        //        EncoderLog = XTrace.Log,
        //        Timeout = 1_000
        //    };

        //    tc.Open();

        //    // 定时广播
        //    _udp_timer = new TimerX(OnDiscover, tc, 0, 5_000) { Async = true };

        //    _udp = tc;
        //}

        //private void OnDiscover(Object state)
        //{
        //    //var udp = new UdpServer();
        //    //udp.Log = XTrace.Log;

        //    //var ep = new IPEndPoint(IPAddress.Broadcast, 6666);
        //    //var session = udp.CreateSession(ep);
        //    //session.Send("Hello");

        //    var tc = state as ApiClient;

        //    var dic = tc.Invoke<IDictionary<String, Object>>("Discover", new { state = DateTime.Now.ToFullString() });
        //    if (dic == null || dic.Count == 0) return;

        //    var str = dic["Server"] + "";
        //    if (str.IsNullOrEmpty()) return;

        //    //WriteLog("收到[{0}]：{1}", tc, str);

        //    if (!str.IsNullOrEmpty())
        //    {
        //        var uri = new NetUri(str);
        //        if (!uri.Host.IsNullOrEmpty() && uri.Port > 0)
        //        {
        //            WriteLog("发现服务器：{0}", uri);

        //            // 停止广播
        //            _udp_timer.TryDispose();
        //            _udp_timer = null;

        //            _udp.TryDispose();
        //            _udp = null;

        //            InitClient(str);
        //        }
        //    }
        //}
        #endregion

        /// <summary>数据测试，菜单t</summary>
        public void Test()
        {
        }
    }
}