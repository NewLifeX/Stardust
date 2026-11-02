using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Stardust.Mcp;

/// <summary>MCP配置。宿主通过委托绑定自身配置源（如 StarServerSetting.EnableMcp/McpActionSet），实现运行时实时开关</summary>
public sealed class StardustMcpOptions
{
    /// <summary>服务总开关。默认关闭，关闭时 /mcp 返回404</summary>
    public Func<Boolean> Enabled { get; set; } = () => false;

    /// <summary>域白名单。逗号分隔（platform/apps/nodes/deploy/config/monitor/now），* 或空表示全部启用</summary>
    public Func<String> Domains { get; set; } = () => "*";

    /// <summary>敏感配置 key 关键词列表（运行时委托读取，宿主从 StarServerSetting 注入）。
    /// config_get 返回时 key 名包含任一关键词（大小写不敏感）→ 值一律返回 ***。</summary>
    public Func<String[]> SensitiveKeyKeywords { get; set; } = () => DefaultSensitiveKeyKeywords;

    /// <summary>默认敏感关键词列表。conn 覆盖 Conn/Connection 变体，authorization 覆盖鉴权头类 key，
    /// 与 McpAuditWriter 写路径脱敏词表保持一致。</summary>
    public static readonly String[] DefaultSensitiveKeyKeywords =
    {
        "db", "database", "conn", "redis", "mysql", "mssql",
        "connectionstring", "password", "passwd",
        "secret", "token", "apikey", "access_key", "authorization",
    };
}

/// <summary>宿主挂载扩展。两宿主各自两行完成接入</summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>注册MCP引擎并扫描 Stardust.Mcp 程序集中的工具</summary>
    public static IServiceCollection AddStardustMcp(this IServiceCollection services, Action<StardustMcpOptions>? configure = null)
    {
        var options = new StardustMcpOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton(sp => new McpEngine(options));
        return services;
    }

    /// <summary>挂载 /mcp 端点中间件（应在宿主自身中间件之前短路）</summary>
    public static IApplicationBuilder UseStardustMcp(this IApplicationBuilder app)
        => app.UseMiddleware<McpMiddleware>();
}
