using System;
using NewLife.Log;
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

            Test3();

            Console.WriteLine("OK!");
            Console.ReadKey();
        }

        static void Test1()
        {
            var sc = new RpcServer()
            {
                Port = 1234,
                Log = XTrace.Log,
                EncoderLog = XTrace.Log,

                NameSpace = "NewLife.Test",
            };

            var star = new StarClient("tcp://127.0.0.1:6666")
            {
                Code = "test",
                Secret = "pass"
            };

            sc.Star = star;

            sc.Start();

            _Server = sc;
        }

        static void Test3()
        {
            //foreach (Environment.SpecialFolder item in Enum.GetValues(typeof(Environment.SpecialFolder)))
            //{
            //    var v = Environment.GetFolderPath(item);
            //    Console.WriteLine("{0}:\t{1}", item, v);
            //}

            var client = new LocalStarClient();
            client.ProbeAndInstall(null, "1.1");
        }
    }
}