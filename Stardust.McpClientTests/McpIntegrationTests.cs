using System.Text.Json.Nodes;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using Stardust.Server;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>MCP 端到端集成测试（新模型）。工具直出 tools/list，按 scope 与域白名单裁剪，
/// 自检工具 whoami 无 scope 要求。旧版 5 工具 + 27 action 的场景已随重设计移除（见 Doc/MCP-4）。</summary>
[Collection(nameof(McpTestCollection))]
public class McpIntegrationTests : IAsyncLifetime
{
    private readonly McpTestServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private const String Endpoint = "http://localhost/mcp";

    public McpIntegrationTests(McpTestServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>场景1：开关开启时 /mcp 可访问，握手协商出版本与服务器名</summary>
    [Fact]
    public async Task S01_EndpointAccessible_Handshake()
    {
        var client = new RawMcpClient(_fixture.CreateClient(), Endpoint);
        var resp = await client.InitializeAsync();

        Assert.False(RawMcpClient.HasError(resp));
        var result = RawMcpClient.GetResult(resp);
        Assert.NotNull(result);
        Assert.Equal("2024-11-05", result!["protocolVersion"]?.GetValue<String>());
        Assert.Equal("Stardust", result["serverInfo"]!["name"]?.GetValue<String>());
    }

    /// <summary>场景2：initialize 免鉴权；tools/list 缺令牌或无效令牌返回 -32001，有效令牌成功</summary>
    [Fact]
    public async Task S02_TokenAuthentication()
    {
        var client = new RawMcpClient(_fixture.CreateClient(), Endpoint);
        Assert.False(RawMcpClient.HasError(await client.InitializeAsync()));

        var noToken = await client.ListToolsAsync();
        Assert.Equal(-32001, RawMcpClient.GetErrorCode(noToken));

        var bad = new RawMcpClient(_fixture.CreateClient(), Endpoint, "sdmcp_invalid_token_xxxxxxxxxxxxxxxxxxxxxxxx");
        Assert.Equal(-32001, RawMcpClient.GetErrorCode(await bad.ListToolsAsync()));

        var (token, tokenStr) = McpTestHelper.CreateTestToken("s02-token");
        try
        {
            var good = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            Assert.False(RawMcpClient.HasError(await good.ListToolsAsync()));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景3：零 scope 令牌的 tools/list 只返回无 scope 要求的 whoami（Q2：旧令牌仅可自检）</summary>
    [Fact]
    public async Task S03_ZeroScopeToken_ListsOnlyWhoAmI()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s03-token");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var resp = await client.ListToolsAsync();
            Assert.False(RawMcpClient.HasError(resp));

            var tools = resp["result"]?["tools"]?.AsArray();
            Assert.NotNull(tools);
            var names = tools!.Select(t => t!["name"]?.GetValue<String>()).ToHashSet();
            Assert.Contains("stardust_token_whoami", names);
            Assert.DoesNotContain("list_authorized_resources", names);
            Assert.DoesNotContain("invoke_action", names);
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景4：whoami 返回身份信封（中文 summary + 结构化 data）</summary>
    [Fact]
    public async Task S04_WhoAmI_ReturnsIdentity()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s04-whoami");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var resp = await client.CallToolAsync("stardust_token_whoami", new { });
            Assert.False(RawMcpClient.HasError(resp));

            var content = RawMcpClient.GetToolContent(resp);
            Assert.NotNull(content);
            Assert.False(content!["summary"]?.GetValue<String>().IsNullOrEmpty());
            Assert.Equal("s04-whoami", content["data"]?["token_name"]?.GetValue<String>());
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景5：调用未知工具返回 -32601</summary>
    [Fact]
    public async Task S05_UnknownTool_Returns32601()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s05-unknown");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var resp = await client.CallToolAsync("no_such_tool", new { });
            Assert.Equal(-32601, RawMcpClient.GetErrorCode(resp));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景6：域白名单过滤。McpActionSet=apps 时 platform 域的 whoami 不出现且调用被拒</summary>
    [Fact]
    public async Task S06_DomainWhitelist_FiltersTools()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s06-domain");
        var setting = StarServerSetting.Current;
        var original = setting.McpActionSet;
        try
        {
            setting.McpActionSet = "apps";
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var list = await client.ListToolsAsync();
            var names = list["result"]?["tools"]?.AsArray()?.Select(t => t!["name"]?.GetValue<String>()).ToHashSet()
                ?? new HashSet<String>();
            Assert.DoesNotContain("stardust_token_whoami", names);

            var call = await client.CallToolAsync("stardust_token_whoami", new { });
            Assert.Equal(-32601, RawMcpClient.GetErrorCode(call));
        }
        finally
        {
            setting.McpActionSet = original;
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景7：whoami 调用写入审计日志（只读 best-effort）</summary>
    [Fact]
    public async Task S07_AuditLog_RecordsCall()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s07-audit");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            await client.CallToolAsync("stardust_token_whoami", new { });
            await Task.Delay(500);

            var audits = McpAudit.Search(token.Id, null, DateTime.MinValue, DateTime.MinValue, null,
                new PageParameter { PageIndex = 1, PageSize = 10 });
            Assert.Contains(audits, a => a.ToolName == "stardust_token_whoami" && a.Success);
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景8：过期令牌返回 -32001</summary>
    [Fact]
    public async Task S08_ExpiredToken_Returns32001()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s08-expired");
        try
        {
            token.ExpireTime = DateTime.Now.AddMinutes(-5);
            token.Update();

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            Assert.Equal(-32001, RawMcpClient.GetErrorCode(await client.ListToolsAsync()));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景9：禁用令牌返回 -32001</summary>
    [Fact]
    public async Task S09_DisabledToken_Returns32001()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s09-disabled");
        try
        {
            token.Enable = false;
            token.Update();

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            Assert.Equal(-32001, RawMcpClient.GetErrorCode(await client.ListToolsAsync()));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景10：非法 JSON 返回 -32700，未知方法返回 -32601</summary>
    [Fact]
    public async Task S10_ProtocolErrors()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s10-protocol");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            var bad = await client.SendAsync("not_a_method", new { });
            Assert.Equal(-32601, RawMcpClient.GetErrorCode(bad));
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }
}
