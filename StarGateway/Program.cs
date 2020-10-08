using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using Stardust.Data;

namespace StarGateway
{
    class Program
    {
        public static void Main(String[] args)
        {
            XTrace.UseConsole();

            // 异步初始化
            Task.Run(InitAsync);

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(String[] args)
        {
            var builder = Host.CreateDefaultBuilder(args);
            //builder.ConfigureWebHostDefaults(webBuilder =>
            //{
            //    var set = Setting.Current;
            //    if (set.Port > 0) webBuilder.UseUrls($"http://*:{set.Port}");
            //    webBuilder.UseStartup<Startup>();
            //});

            return builder;
        }

        private static void InitAsync()
        {
            // 配置
            var set = NewLife.Setting.Current;
            if (set.IsNew)
            {
                set.DataPath = "../Data";
                set.Save();
            }

            // 初始化数据库
            var n = App.Meta.Count;
            AppStat.Meta.Session.Dal.Db.ShowSQL = false;
        }
    }
}