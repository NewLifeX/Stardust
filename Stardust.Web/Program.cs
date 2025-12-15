using NewLife.Log;
using Stardust.Web;

XTrace.UseConsole();

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.UseStartup<Startup>();
});

builder.Build().Run();