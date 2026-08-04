using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;

namespace Stardust.McpClientTests;

/// <summary>轻量MCP客户端。直接发送JSON-RPC 2.0 over HTTP，不依赖MCP SDK</summary>
public class RawMcpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly String _endpoint;
    private readonly String? _token;

    /// <summary>构造</summary>
    /// <param name="httpClient">HTTP客户端（通常来自TestServer）</param>
    /// <param name="endpoint">MCP端点地址（如 http://localhost/mcp）</param>
    /// <param name="token">Bearer Token（sdmcp_xxx），initialize不需要</param>
    public RawMcpClient(HttpClient httpClient, String endpoint, String? token = null)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
        _token = token;
    }

    /// <summary>发送JSON-RPC请求</summary>
    public async Task<JsonObject> SendAsync(String method, Object? @params = null, Object? id = null)
    {
        var request = new Dictionary<String, Object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["id"] = id ?? 1,
        };
        if (@params != null) request["params"] = @params;

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };

        if (!_token.IsNullOrEmpty())
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(httpRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        try
        {
            return JsonSerializer.Deserialize<JsonObject>(responseBody) ?? new JsonObject();
        }
        catch (JsonException)
        {
            // 服务端返回非JSON响应（如HTML错误页），包装为JSON-RPC错误
            var snippet = responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody;
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new JsonObject
                {
                    ["code"] = -32603,
                    ["message"] = $"Non-JSON response (HTTP {(Int32)response.StatusCode}): {snippet}",
                },
                ["id"] = JsonValue.Create(id ?? 1),
            };
        }
    }

    /// <summary>initialize握手（默认使用1.0协议版本2024-11-05）</summary>
    public async Task<JsonObject> InitializeAsync(String protocolVersion = "2024-11-05")
    {
        return await SendAsync("initialize", new
        {
            protocolVersion,
            capabilities = new { },
            clientInfo = new { name = "test-client", version = "1.0.0" }
        }, id: 1);
    }

    /// <summary>获取工具列表</summary>
    public async Task<JsonObject> ListToolsAsync()
    {
        return await SendAsync("tools/list", new { }, id: 2);
    }

    /// <summary>调用工具</summary>
    public async Task<JsonObject> CallToolAsync(String toolName, Object arguments)
    {
        return await SendAsync("tools/call", new { name = toolName, arguments }, id: 3);
    }

    /// <summary>检查响应是否包含error字段</summary>
    public static Boolean HasError(JsonObject response) => response.ContainsKey("error");

    /// <summary>获取错误码</summary>
    public static Int32 GetErrorCode(JsonObject response) =>
        response["error"]?["code"]?.GetValue<Int32>() ?? 0;

    /// <summary>获取错误消息</summary>
    public static String? GetErrorMessage(JsonObject response) =>
        response["error"]?["message"]?.GetValue<String>();

    /// <summary>获取result字段</summary>
    public static JsonNode? GetResult(JsonObject response) => response["result"];

    /// <summary>获取tools/call返回的text内容（解析content[0].text为JSON）</summary>
    public static JsonNode? GetToolContent(JsonObject response)
    {
        var text = response["result"]?["content"]?[0]?["text"]?.GetValue<String>();
        if (text.IsNullOrEmpty()) return null;
        return JsonSerializer.Deserialize<JsonNode>(text!);
    }

    public void Dispose()
    {
        // 不 dispose httpClient，由 TestServer 管理
    }
}
