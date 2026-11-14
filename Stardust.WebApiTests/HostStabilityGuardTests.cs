using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Stardust.Web;
using Xunit;

namespace Stardust.WebApiTests;

/// <summary>wave4 ⑩ 宿主稳定性防护（Kestrel Heartbeat.TimerLoop 自毙主修）。</summary>
/// <remarks>
/// 缺陷链：非提权进程写 EventLog '.NET Runtime' 事件源被拒（Win32 5）→ 聚合 Logger 抛
/// AggregateException → Kestrel Heartbeat.OnHeartbeat 内 LogError 抛出逃逸 TimerLoop 线程
/// →未处理异常→宿主自毙（2026-11-10 20:24:09.784 实发，Bin\WebApi\crash-host-err-20261110-2024.log）。
/// </remarks>
public class HostStabilityGuardTests
{
    [Fact]
    public void Apply_EventLogFilterRuleNone()
    {
        var builder = WebApplication.CreateBuilder();
        HostStabilityGuard.Apply(builder);
        using var app = builder.Build();

        // 断根闸口=LoggerFactory filter rule：生产链里聚合 Logger.Write 按 rule 对每个 provider
        // 做 IsEnabled，rule(None) 命中 EventLog→Heartbeat 内 LogError 不再触达其写路径。
        // 注：EventLogLoggerProvider.CreateLogger 直造实例的 IsEnabled 不经过 filter（框架实现），
        // 故正确断言点是 LoggerFilterOptions.Rules 而非直造 logger。
        // 注2：代码级 AddFilter<EventLogLoggerProvider> 注册的 ProviderName=provider 全名
        // （Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider），配置别名 "EventLog"
        // 只出现在 appsettings 配置式规则里，两者勿混。
        var options = app.Services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
        Assert.Contains(options.Rules, r => r.ProviderName == "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider" && r.LogLevel == LogLevel.None);
    }

    [Fact]
    public void Apply_BackgroundServiceExceptionBehaviorIsIgnore()
    {
        var builder = WebApplication.CreateBuilder();
        HostStabilityGuard.Apply(builder);
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.Equal(BackgroundServiceExceptionBehavior.Ignore, options.BackgroundServiceExceptionBehavior);
    }

    [Fact]
    public void AppSettings_EventLogDisabled_DeployDriftAnchor()
    {
        var path = FindRepoFile(Path.Combine("Stardust.WebApi", "appsettings.json"));
        var json = File.ReadAllText(path);
        // appsettings.json 是含注释的 JSON（ASP.NET 配置惯例），解析须容忍注释与尾逗号
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var value = doc.RootElement
            .GetProperty("Logging")
            .GetProperty("EventLog")
            .GetProperty("LogLevel")
            .GetProperty("Default");
        Assert.Equal("None", value.GetString());
    }

    [Fact]
    public void Program_WiresHostStabilityGuard()
    {
        var path = FindRepoFile(Path.Combine("Stardust.WebApi", "Program.cs"));
        Assert.Contains("HostStabilityGuard.Apply", File.ReadAllText(path));
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent!;
        }

        throw new FileNotFoundException(relativePath);
    }
}
