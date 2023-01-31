using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NewLife.Log;

namespace Stardust.Web
{
    public class Program
    {
        public static void Main(String[] args)
        {
            XTrace.UseConsole();

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(String[] args)
        {
            var builder = Host.CreateDefaultBuilder(args);
            builder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

            return builder;
        }
    }
}