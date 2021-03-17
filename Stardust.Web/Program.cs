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

        public static IWebHostBuilder CreateWebHostBuilder(String[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();
    }
}