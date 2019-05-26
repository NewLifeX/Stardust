using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Net;
using NewLife.Threading;
using Stardust;
using System;
using System.Net;

namespace StarAgent
{
    class Program
    {
        static void Main(String[] args) => new MyService().Main();
    }

    /// <summary>服务类。名字可以自定义</summary>
    class MyService : AgentServiceBase<MyService>
    {
        /// <summary>是否使用线程池调度。false表示禁用线程池，改用Agent线程</summary>
        public Boolean Pooling { get; set; } = true;

        public MyService()
        {
            ServiceName = "StarAgent";

            // 注册菜单，在控制台菜单中按 t 可以执行Test函数，主要用于临时处理数据
            AddMenu('t', "测试", Test);
        }

        StarClient _Client;
        private void Init()
        {
            if (_Client == null)
            {
                var set = Setting.Current;
                if (!set.Server.IsNullOrEmpty())
                    InitClient(set.Server);
                else
                {
                    WriteLog("未配置服务端地址，开始自动发现");
                    StartDiscover();
                }
            }
        }

        private void InitClient(String server)
        {
            if (server.IsNullOrEmpty()) return;

            var set = Setting.Current;

            var client = new StarClient(server)
            {
                Log = XTrace.Log
            };
            if (set.Debug) client.EncoderLog = XTrace.Log;

            client.Open();

            _Client = client;
        }

        /// <summary>服务启动</summary>
        /// <remarks>
        /// 安装Windows服务后，服务启动会执行一次该方法。
        /// 控制台菜单按5进入循环调试也会执行该方法。
        /// </remarks>
        protected override void StartWork(String reason)
        {
            Init();

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

            _Client.TryDispose();
            _Client = null;
        }

        #region 自动发现服务端
        private UdpServer _udp;
        private TimerX _udp_timer;
        private void StartDiscover()
        {
            var udp = new UdpServer();
#if DEBUG
            udp.Log = XTrace.Log;
            udp.LogSend = true;
            udp.LogReceive = true;
#endif

            var ep = new IPEndPoint(IPAddress.Broadcast, 6666);
            var session = udp.CreateSession(ep);
            session.Received += OnDiscover;

            //session.Send("Hello");
            // 定时广播
            _udp_timer = new TimerX(s => (s as ISocketSession).Send("Hello"), session, 0, 5_000);

            _udp = udp;
        }

        private void OnDiscover(Object sender, ReceivedEventArgs e)
        {
            var str = e.Packet?.ToStr();
            WriteLog("收到[{0}]：{1}", e.UserState, str);

            if (!str.IsNullOrEmpty())
            {
                var uri = new NetUri(str);
                if (!uri.Host.IsNullOrEmpty() && uri.Port > 0)
                {
                    WriteLog("发现服务器：{0}", uri);

                    // 停止广播
                    _udp_timer.TryDispose();
                    _udp_timer = null;

                    _udp.TryDispose();
                    _udp = null;
                }
            }
        }
        #endregion

        /// <summary>数据测试，菜单t</summary>
        public void Test()
        {
        }
    }
}