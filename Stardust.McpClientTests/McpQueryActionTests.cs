using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Stardust.Data.Platform;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>MCP 查询类工具端到端测试</summary>
/// <remarks>
/// v1 直出工具集：旧版二段式 <c>invoke_action</c> 已废弃，网关域工具按 MCP-4 设计推迟到 v2。
/// 这里覆盖仍保留的查询域（告警、调用链）与「不再注册的工具」契约。
/// </remarks>
[Collection(nameof(McpTestCollection))]
public class McpQueryActionTests
{
    private readonly McpTestServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private const String Endpoint = "http://localhost/mcp";

    public McpQueryActionTests(McpTestServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>调用工具并取出返回内容</summary>
    private static async Task<JsonNode?> InvokeAsync(RawMcpClient client, String tool, Object parameters)
    {
        var resp = await client.CallToolAsync(tool, parameters);
        Assert.False(RawMcpClient.HasError(resp), $"{tool} 调用失败：{RawMcpClient.GetErrorMessage(resp)}");

        var content = RawMcpClient.GetToolContent(resp);
        Assert.True(content != null, $"{tool} 未返回内容：{resp.ToJsonString()}");

        return content;
    }

    /// <summary>创建已绑定项目并授予监控读取的令牌。查询类工具严格要求单项目边界与域授权</summary>
    private static (McpToken token, String tokenString) CreateProjectToken(String name, Int32 projectId)
    {
        var (token, tokenString) = McpTestHelper.CreateTestToken(name);
        token.ProjectId = projectId;
        token.Scopes = "monitor:read,apps:read";
        token.Update();

        return (token, tokenString);
    }

    [Fact(DisplayName = "告警列表：按令牌绑定项目返回分页结构")]
    public async Task AlarmList_ReturnsPagedEnvelope()
    {
        var (token, tokenStr) = CreateProjectToken("mcp-alarm", 900001);
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var content = await InvokeAsync(client, "stardust_alarm_list", new { page_size = 5 });

            var pagination = content!["pagination"];
            Assert.NotNull(pagination);
            Assert.NotNull(pagination!["total"]);
            Assert.NotNull(content["data"]?["records"]);
            _output.WriteLine($"告警 {pagination["total"]} 条");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    [Fact(DisplayName = "调用链搜索：缺少应用编号时拒绝访问")]
    public async Task TraceSearch_WithoutApp_ReturnsAccessDenied()
    {
        var (token, tokenStr) = CreateProjectToken("mcp-trace", 900001);
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var resp = await client.CallToolAsync("stardust_trace_search", new { });

            Assert.Equal(-32003, RawMcpClient.GetErrorCode(resp));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    [Fact(DisplayName = "网关域工具推迟到 v2，v1 不注册 invoke_action 与 gateway 工具")]
    public async Task GatewayTools_DeferredToV2()
    {
        var (token, tokenStr) = CreateProjectToken("mcp-gateway", 900001);
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var list = await client.ListToolsAsync();
            var names = list["result"]?["tools"]?.AsArray()?.Select(t => t!["name"]?.GetValue<String>()).Where(e => e != null).ToArray()
                ?? [];

            Assert.DoesNotContain("invoke_action", names);
            Assert.DoesNotContain(names, e => e!.StartsWith("stardust_gateway_", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("stardust_alarm_list", names);
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }
}
