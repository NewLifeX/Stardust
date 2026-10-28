using NewLife.Log;
using Stardust;
using Stardust.Web;

// 先按宿主语言初始化日志语言，保证首次写出的配置文件注释即为本地语言，避免稍后改写
StarLocalization.InitLogCulture();

XTrace.UseConsole();

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.UseStartup<Startup>();
});

builder.Build().Run();