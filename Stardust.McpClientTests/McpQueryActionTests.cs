using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>MCP 查询类动作端到端测试。覆盖网关与监控两个模块的动作调用（MCP-10 / MCP-11）</summary>
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

    /// <summary>调用动作并取出返回内容</summary>
    private static async Task<JsonNode?> InvokeAsync(RawMcpClient client, String action, Object parameters)
    {
        var resp = await client.CallToolAsync("invoke_action", new { action_name = action, @params = parameters });
        Assert.False(RawMcpClient.HasError(resp), $"{action} 调用失败：{RawMcpClient.GetErrorMessage(resp)}");

        var content = RawMcpClient.GetToolContent(resp);
        Assert.True(content != null, $"{action} 未返回内容：{resp.ToJsonString()}");

        return content;
    }

    [Fact(DisplayName = "网关模块：查询路由与集群列表成功返回分页结构")]
    public async Task GatewayActions_ReturnPagedList()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("mcp-gateway");
        try
        {
            // 未绑定任何资源的 Token 不允许 invoke_action（-32003），这里授权全部项目
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var routes = await InvokeAsync(client, "gateway_list_routes", new { page = 1, page_size = 5 });
            Assert.NotNull(routes!["total"]);
            Assert.NotNull(routes["records"]);

            var clusters = await InvokeAsync(client, "gateway_list_clusters", new { page = 1, page_size = 5 });
            Assert.NotNull(clusters!["total"]);
            Assert.NotNull(clusters["records"]);

            _output.WriteLine($"路由 {routes["total"]} 条，集群 {clusters["total"]} 个");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    [Fact(DisplayName = "监控模块：调用链搜索与告警列表成功返回分页结构")]
    public async Task MonitorActions_ReturnPagedList()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("mcp-monitor");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 不传 app_id 时不做应用授权校验，走"最近1小时"默认时间窗
            var traces = await InvokeAsync(client, "monitor_trace_search", new { kind = "day", page = 1, page_size = 5 });
            Assert.Equal("day", traces!["kind"]?.GetValue<String>());
            Assert.NotNull(traces["total"]);
            Assert.NotNull(traces["records"]);

            var alarms = await InvokeAsync(client, "monitor_alarm_list", new { page = 1, page_size = 5 });
            Assert.NotNull(alarms!["total"]);
            Assert.NotNull(alarms["records"]);

            _output.WriteLine($"追踪 {traces["total"]} 条，告警 {alarms["total"]} 条");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }
}
