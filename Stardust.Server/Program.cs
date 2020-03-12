using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using Stardust.Data;

namespace Stardust.Server
{
    public class Program
    {
        public static void Main(String[] args)
        {
            if (Runtime.Windows)
            {
                var svc = new MyService
                {
                    Args = args
                };
                svc.Main();
            }
            else
            {
                MyService.Run(args);
            }
        }
    }

    class MyService : AgentServiceBase<MyService>
    {
        public String[] Args { get; set; }

        public MyService()
        {
            ServiceName = "";

            // 异步初始化
            Task.Run(InitAsync);
        }

        private IWebHost _host;
        protected override void StartWork(String reason)
        {
            _host = CreateWebHostBuilder(Args).Build();
            _host.RunAsync();

            base.StartWork(reason);
        }

        protected override void StopWork(String reason)
        {
            _host.StopAsync().Wait(5_000);

            base.StopWork(reason);
        }

        public static void Run(String[] args)
        {
            XTrace.UseConsole();

            // 异步初始化
            Task.Run(InitAsync);

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(String[] args)
        {
            var set = Setting.Current;

            var builder = WebHost.CreateDefaultBuilder(args);

            if (set.Port > 0) builder.UseUrls($"http://*:{set.Port}");

            builder.UseStartup<Startup>();

            return builder;
        }

        private static void InitAsync()
        {
            // 初始化数据库
            var n = App.Meta.Count;
            AppStat.Meta.Session.Dal.Db.ShowSQL = false;
        }
    }
}