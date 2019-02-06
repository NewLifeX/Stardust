using System;
using System.Net;
using System.Threading;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Stardust;
using Setting = Stardust.Setting;

namespace Test
{
    class Program
    {
        static ApiServer _Server;
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            var set = Setting.Current;

            var sc = new ApiServer(set.Port)
            {
                Log = XTrace.Log
            };
            if (set.Debug)
            {
                var ns = sc.EnsureCreate() as NetServer;
                ns.Log = XTrace.Log;
#if DEBUG
                ns.LogSend = true;
                ns.LogReceive = true;
                sc.EncoderLog = XTrace.Log;
#endif
            }

            // 注册服务
            sc.Register<StarService>();

            StarService.Log = XTrace.Log;
            StarService.Local = new IPEndPoint(NetHelper.MyIP(), set.Port);

            sc.Start();

            _Server = sc;

            Thread.Sleep(-1);
        }
    }
}
