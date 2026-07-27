namespace Stardust.Web.Mcp;

/// <summary>MCP业务异常。携带JSON-RPC错误码，由McpService统一捕获并包装为错误响应</summary>
public class McpException : Exception
{
    /// <summary>JSON-RPC错误码（如-32601方法不存在、-32602参数错误、-32003资源未授权等）</summary>
    public Int32 Code { get; }

    public McpException(Int32 code, String message) : base(message) => Code = code;
}
