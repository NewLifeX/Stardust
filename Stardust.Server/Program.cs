using NewLife.Log;
using Stardust.Server;

XTrace.UseConsole();

// 加大最小线程数，避免启动时线程饥饿
ThreadPool.SetMinThreads(1024, 1000);

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureWebHostDefaults(webBuilder =>
{
    var set = StarServerSetting.Current;
    if (set.Port > 0) webBuilder.UseUrls($"http://*:{set.Port}");
    webBuilder.UseStartup<Startup>();
});

builder.Build().Run();