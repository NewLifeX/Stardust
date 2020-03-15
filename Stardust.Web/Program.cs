using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NewLife.Log;
using Stardust.Data;

namespace Stardust.Web
{
    public class Program
    {
        public static void Main(String[] args)
        {
            XTrace.UseConsole();

            // 异步初始化
            Task.Run(InitAsync);

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(String[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();

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