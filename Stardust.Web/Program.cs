using NewLife.Log;
using Stardust;
using Stardust.Web;

// 先按宿主语言初始化日志语言，保证首次写出的配置文件注释即为本地语言，避免稍后改写
StarLocalization.InitLogCulture();

// 首次启动时先按星尘约定落盘日志目录，避免首轮日志落到 Core 默认的 ../WebLog
StarHost.InitLogPath("../LogWeb");

XTrace.UseConsole();

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.UseStartup<Startup>();
});

builder.Build().Run();