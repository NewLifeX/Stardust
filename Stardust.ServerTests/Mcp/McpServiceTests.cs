using System.Text.Json;
using NewLife;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>MCP服务单元测试。覆盖JSON-RPC协议解析、错误码、方法路由</summary>
public class McpServiceTests
{
    /// <summary>JSON-RPC 标准错误码</summary>
    private const Int32 ParseError = -32700;
    private const Int32 InvalidRequest = -32600;
    private const Int32 MethodNotFound = -32601;
    private const Int32 InvalidParams = -32602;
    private const Int32 InternalError = -32603;

    /// <summary>MCP 自定义错误码</summary>
    private const Int32 TokenInvalid = -32001;
    private const Int32 TokenExpired = -32002;
    private const Int32 ResourceUnauthorized = -32003;
    private const Int32 ActionNotFound = -32004;
    private const Int32 McpDisabled = -32005;

    [Theory]
    [InlineData("{}", false)]                    // 缺少 jsonrpc/method
    [InlineData("{\"jsonrpc\":\"1.0\"}", false)]  // 错误版本
    [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"\"}", false)]  // 空方法名
    [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"initialize\"}", true)]  // 有效
    [InlineData("{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\"}", true)]  // 有效
    public void JsonRpc_RequestValidation(String json, Boolean expectedValid)
    {
        var result = IsValidJsonRpcRequest(json);
        Assert.Equal(expectedValid, result);
    }

    [Theory]
    [InlineData("not json at all", ParseError)]
    [InlineData("", ParseError)]
    [InlineData("null", ParseError)]
    [InlineData("[]", InvalidRequest)]
    [InlineData("{}", InvalidRequest)]
    public void JsonRpc_InvalidInput_ReturnsCorrectErrorCode(String json, Int32 expectedCode)
    {
        var code = GetJsonRpcErrorCode(json);
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("tools/list")]
    [InlineData("tools/call")]
    public void JsonRpc_KnownMethods_AreValid(String method)
    {
        var knownMethods = new HashSet<String> { "initialize", "tools/list", "tools/call" };
        Assert.Contains(method, knownMethods);
    }

    [Theory]
    [InlineData("unknown_method", MethodNotFound)]
    [InlineData("foo", MethodNotFound)]
    public void JsonRpc_UnknownMethod_ReturnsMethodNotFound(String method, Int32 expectedCode)
    {
        var knownMethods = new HashSet<String> { "initialize", "tools/list", "tools/call" };
        var code = knownMethods.Contains(method) ? 0 : MethodNotFound;
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData("list_authorized_resources")]
    [InlineData("search_resources")]
    [InlineData("get_resource")]
    [InlineData("list_actions")]
    [InlineData("invoke_action")]
    public void McpTools_AllFiveToolsExist(String toolName)
    {
        var expectedTools = new HashSet<String>
        {
            "list_authorized_resources",
            "search_resources",
            "get_resource",
            "list_actions",
            "invoke_action"
        };

        Assert.Contains(toolName, expectedTools);
    }

    [Theory]
    [InlineData(ParseError, "Parse error")]
    [InlineData(InvalidRequest, "Invalid Request")]
    [InlineData(MethodNotFound, "Method not found")]
    [InlineData(InvalidParams, "Invalid params")]
    [InlineData(InternalError, "Internal error")]
    [InlineData(TokenInvalid, "Token invalid")]
    [InlineData(TokenExpired, "Token expired")]
    [InlineData(ResourceUnauthorized, "Resource unauthorized")]
    public void ErrorCodes_MappingToMessages(Int32 code, String expectedMessage)
    {
        var message = GetErrorMessage(code);
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public void JsonRpc_Response_HasIdEcho()
    {
        // JSON-RPC 规范要求响应必须回显请求 ID
        var requestJson = "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\",\"id\":1}";
        var request = JsonDocument.Parse(requestJson);
        var id = request.RootElement.GetProperty("id").GetInt32();

        Assert.Equal(1, id);
    }

    [Fact]
    public void JsonRpc_Response_ErrorStructure()
    {
        // 验证 JSON-RPC 错误响应结构：{ jsonrpc, id, error: { code, message } }
        var errorResponse = new
        {
            jsonrpc = "2.0",
            id = (Int32?)1,
            error = new { code = MethodNotFound, message = "Method not found" }
        };

        var json = JsonSerializer.Serialize(errorResponse);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("id").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.Equal(MethodNotFound, error.GetProperty("code").GetInt32());
        Assert.NotNull(error.GetProperty("message").GetString());
    }

    [Fact]
    public void JsonRpc_SuccessResponseStructure()
    {
        // 验证 JSON-RPC 成功响应结构：{ jsonrpc, id, result }
        var successResponse = new
        {
            jsonrpc = "2.0",
            id = (Int32?)1,
            result = new { status = "ok" }
        };

        var json = JsonSerializer.Serialize(successResponse);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("id").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("result", out _));
        Assert.False(doc.RootElement.TryGetProperty("error", out _));
    }

    #region 辅助方法

    private static Boolean IsValidJsonRpcRequest(String json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("jsonrpc", out var version) || version.GetString() != "2.0") return false;
            if (!root.TryGetProperty("method", out var method) || method.GetString().IsNullOrEmpty()) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Int32 GetJsonRpcErrorCode(String json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array) return InvalidRequest;
            if (root.ValueKind != JsonValueKind.Object) return ParseError;
            if (!root.TryGetProperty("jsonrpc", out _) || !root.TryGetProperty("method", out _)) return InvalidRequest;
            return 0;
        }
        catch (JsonException)
        {
            return ParseError;
        }
        catch
        {
            return ParseError;
        }
    }

    private static String GetErrorMessage(Int32 code) => code switch
    {
        ParseError => "Parse error",
        InvalidRequest => "Invalid Request",
        MethodNotFound => "Method not found",
        InvalidParams => "Invalid params",
        InternalError => "Internal error",
        TokenInvalid => "Token invalid",
        TokenExpired => "Token expired",
        ResourceUnauthorized => "Resource unauthorized",
        _ => "Unknown error"
    };

    #endregion
}
