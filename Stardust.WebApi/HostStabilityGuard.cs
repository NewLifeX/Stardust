using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace Stardust.Web;

/// <summary>宿主稳定性防护（wave4 ⑩：Kestrel Heartbeat.TimerLoop 自毙主修，进程内）。</summary>
public static class HostStabilityGuard
{
    /// <summary>应用宿主稳定性防护，须在 WebApplication.Build() 之前调用。</summary>
    /// <remarks>
    /// ① EventLog logger 全级别禁用：WebApplication.CreateBuilder 在 Windows 默认注册
    /// EventLogLoggerProvider（source '.NET Runtime'），非提权进程写事件源被拒（Win32 5）→
    /// 聚合 Logger 抛 AggregateException（An error occurred while writing to logger(s)）→
    /// Kestrel Heartbeat.OnHeartbeat 捕获后的 LogError 本身抛出异常逃逸 Heartbeat.TimerLoop
    /// →未处理异常→宿主运行时自毙（2026-11-10 20:24:09.784 实发，crash-host-err-20261110-2024.log
    /// 尾栈在档）。过滤后 Heartbeat 内 LogError 不再触达 EventLog 写路径，TimerLoop 不再因
    /// 日志异常逃逸。与 appsettings Logging:EventLog:LogLevel:Default=None 同义
    /// （6680 DP-01 同配方），代码级过滤为防部署漂移双保险。
    /// ② BackgroundService 未处理异常只记日志不停宿主（.NET 默认 StopHost=单服务异常拖垮全宿主；
    /// 现有三个 IHostedService 均不受该行为影响，此项纯防御纵深）。
    /// </remarks>
    /// <param name="builder">宿主构建器</param>
    public static void Apply(WebApplicationBuilder builder)
    {
        // ① 断根：EventLog 写路径全级别关死（Heartbeat 等框架线程内 LogError 不再触达不可写事件源）。
        //    CA1416 守卫：EventLogLoggerProvider 为 Windows 专属；非 Windows 本无此写路径，跳过注册行为等价
        if (OperatingSystem.IsWindows())
        {
            builder.Logging.AddFilter<EventLogLoggerProvider>(null, LogLevel.None);
        }

        // ② 兜底：BackgroundService 未处理异常只记日志不停宿主（Host 内部仍会 LogError，异常可见）
        builder.Services.Configure<HostOptions>(o =>
        {
            o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });
    }
}
