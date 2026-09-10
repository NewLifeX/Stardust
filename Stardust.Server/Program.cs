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

    // 弱网客户端（家庭宽带/代理/休眠唤醒）可能延迟发送请求体。Kestrel 默认最低速率 240B/s、宽限 5 秒，
    // 会把请求体迟到误判为异常请求而断开，导致登录/心跳偶发失败。这里取消请求体最低速率限制，
    // 防慢速攻击由登录接口 IP 限流（RateLimitMiddleware）与请求大小限制等机制兜底
    webBuilder.ConfigureKestrel(options => options.Limits.MinRequestBodyDataRate = null);

    webBuilder.UseStartup<Startup>();
});

builder.Build().Run();