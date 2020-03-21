using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using Stardust.Data;
using Stardust.Server.Services;

namespace Stardust.Server
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

        public static IWebHostBuilder CreateWebHostBuilder(String[] args)
        {
            var set = Setting.Current;

            var builder = WebHost.CreateDefaultBuilder(args);

            if (set.Port > 0) builder.UseUrls($"http://*:{set.Port}");

            builder.UseStartup<Startup>();

            return builder;
        }

        private static NodeOnlineService _online;
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

            // 在线管理服务
            _online = new NodeOnlineService();
            _online.Init();
        }
    }
}