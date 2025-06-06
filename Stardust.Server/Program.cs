using NewLife.Log;

namespace Stardust.Server;

public class Program
{
    public static void Main(String[] args)
    {
        XTrace.UseConsole();

        // 加大最小线程数，避免启动时线程饥饿
        ThreadPool.SetMinThreads(1024, 100);

        CreateWebHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateWebHostBuilder(String[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureWebHostDefaults(webBuilder =>
        {
            var set = StarServerSetting.Current;
            if (set.Port > 0) webBuilder.UseUrls($"http://*:{set.Port}");
            webBuilder.UseStartup<Startup>();
        });

        return builder;
    }
}