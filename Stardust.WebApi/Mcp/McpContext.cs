namespace Stardust.Web.Mcp;

/// <summary>MCP调用上下文。每次tools/call请求时构建，传递给动作实现</summary>
public sealed class McpContext
{
    /// <summary>令牌ID</summary>
    public Int32 TokenId { get; init; }

    /// <summary>令牌名称（快照）</summary>
    public String TokenName { get; init; }

    /// <summary>调用方IP</summary>
    public String CallerIp { get; init; }

    /// <summary>客户端User-Agent</summary>
    public String UserAgent { get; init; }

    /// <summary>链路追踪ID</summary>
    public String TraceId { get; init; }

    /// <summary>服务提供者，用于动作实现解析依赖</summary>
    public IServiceProvider ServiceProvider { get; init; }
}
