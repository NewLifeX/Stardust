using System;
using System.Collections.Generic;
using System.Threading;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Stardust;
using XCode.DataAccessLayer;

namespace Test
{
    class Program
    {
        static ApiServer _Server;
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            Test2();
            //Thread.Sleep(-1);

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

        static void Test2()
        {
            //DAL.AddConnStr("node", "Data Source=..\\Data\\Node.db", null, "sqlite");
            DAL.AddConnStr("mysql", "Server=.;Port=3306;Database=Node;Uid=root;Pwd=root;", null, "mysql");

            var dal = DAL.Create("mysql");
            var rs = dal.RestoreAll("../data/Node_20200903215342.zip", null);
            //Assert.NotNull(rs);
        }
    }
}