namespace Stardust.Mcp;

/// <summary>MCP业务异常。Code 为 JSON-RPC 错误码（沿用旧实现约定：-32602参数、-32001未认证、-32003越权、-32000业务失败）</summary>
public class McpException : Exception
{
    /// <summary>JSON-RPC错误码</summary>
    public Int32 Code { get; }

    public McpException(Int32 code, String message) : base(message) => Code = code;
}
