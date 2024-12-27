using NewLife.Log;

namespace Stardust.Server;

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
            var set = StarServerSetting.Current;
            if (set.Port > 0) webBuilder.UseUrls($"http://*:{set.Port}");
            webBuilder.UseStartup<Startup>();
        });

        return builder;
    }
}