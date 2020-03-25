using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace StarAgent3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            XTrace.UseConsole();

            XTrace.WriteLine("FullPath:{0}", ".".GetFullPath());
            XTrace.WriteLine("BasePath:{0}", ".".GetBasePath());
            XTrace.WriteLine("TempPath:{0}", Path.GetTempPath());

            var mi = MachineInfo.Current ?? MachineInfo.RegisterAsync().Result;

            foreach (var pi in mi.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                XTrace.WriteLine("{0}:\t{1}", pi.Name, mi.GetValue(pi));
            }

            // 检测systemd
            if (Runtime.Linux)
            {
                var file = "/etc/systemd/system/StarAgent.service";
                if (!File.Exists(file))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("[Unit]");
                    sb.AppendLine($"Description= 星尘代理");

                    sb.AppendLine();
                    sb.AppendLine("[Service]");
                    sb.AppendLine("Type=simple");
                    sb.AppendLine($"ExecStart=/usr/bin/dotnet {typeof(Program).Assembly.Location}");
                    sb.AppendLine("Restart=on-failure");

                    sb.AppendLine();
                    sb.AppendLine("[Install]");
                    sb.AppendLine("WantedBy=multi-user.target");

                    File.WriteAllText(file, sb.ToString());
                }
            }

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                });

            if (Runtime.Windows)
                builder.UseWindowsService();
            else if (Runtime.Linux)
                builder.UseSystemd();

            return builder;
        }
    }
}
