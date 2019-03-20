using System;
using System.Collections.Generic;
using System.Threading;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Stardust;

namespace Test
{
    class Program
    {
        static ApiServer _Server;
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            var sc = new RpcServer()
            {
                Port = 1234,
                Log = XTrace.Log,
                EncoderLog = XTrace.Log,

                NameSpace = "NewLife.Test",
            };

            var star = new StarClient("tcp://127.0.0.1:6666")
            {
                UserName = "test",
                Password = "pass"
            };

            sc.Star = star;

            var rs = star.InvokeAsync<List<String>>("GetAll").Result;

            //if (set.Debug)
            {
                var ns = sc.EnsureCreate() as NetServer;
                ns.Log = XTrace.Log;
#if DEBUG
                ns.LogSend = true;
                ns.LogReceive = true;
                sc.EncoderLog = XTrace.Log;
#endif
            }

            sc.Start();

            _Server = sc;

            Thread.Sleep(-1);
        }
    }
}