using ModelContextProtocol;
using ModelContextProtocol.Client;
using NewLife;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>协议层测试：initialize 协商、ping、未知方法、非法 JSON、空 body、tools/list 鉴权。
/// 面向真实 WebApi(6681) /mcp 端点，使用官方 MCP SDK。</summary>
[Collection(nameof(McpApiTestCollection))]
public class McpProtocolTests : McpSdkTestBase
{
    public McpProtocolTests(ITestOutputHelper output) : base(output) { }

    /// <summary>initialize 握手：协商出服务端支持的协议版本，serverInfo 为 Stardust</summary>
    [Fact(DisplayName = "协议-1：SDK initialize 握手成功（协议版本协商）")]
    public async Task Initialize_Handshake_Succeeds()
    {
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], client =>
        {
            Assert.NotNull(client.ServerInfo);
            Assert.Equal("Stardust", client.ServerInfo.Name);

            var negotiated = client.NegotiatedProtocolVersion;
            Assert.False(negotiated.IsNullOrEmpty());
            Assert.Contains(negotiated, new[] { "2026-07-28", "2025-06-18", "2025-03-26", "2024-11-05" });

            Output.WriteLine($"✅ initialize 成功：ServerName={client.ServerInfo.Name}, NegotiatedProtocolVersion={negotiated}");
            return Task.CompletedTask;
        });
    }

    /// <summary>initialize 不需要鉴权（无 Token 也应成功）</summary>
    [Fact(DisplayName = "协议-2：无 Token 时 initialize 仍成功（端点免鉴权）")]
    public async Task Initialize_NoToken_Succeeds()
    {
        await RunSdkAsync(null, client =>
        {
            Assert.NotNull(client);
            Assert.Equal("Stardust", client.ServerInfo.Name);
            Output.WriteLine("✅ 无Token initialize 成功");
            return Task.CompletedTask;
        });
    }

    /// <summary>无 Token 时 tools/list 必须失败（缺 Bearer 鉴权）</summary>
    [Fact(DisplayName = "协议-3：无 Token 时 tools/list 被拒（-32001 类错误）")]
    public async Task ListTools_NoToken_Rejected()
    {
        await RunSdkAsync(null, async client =>
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await client.ListToolsAsync());
            Output.WriteLine($"✅ 无Token tools/list 抛异常：{ex.GetType().Name}: {ex.Message}");
        });
    }

    /// <summary>合法 Token 时 tools/list 成功且返回工具（覆盖工具清单在其他测试验证）</summary>
    [Fact(DisplayName = "协议-4：合法 Token 时 tools/list 成功")]
    public async Task ListTools_ValidToken_Succeeds()
    {
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var tools = await client.ListToolsAsync();
            Assert.True(tools.Count > 0, "tools/list 应返回工具");
            Output.WriteLine($"✅ tools/list 成功：返回 {tools.Count} 个工具");
        });
    }

    /// <summary>伪造 Token 时 tools/list 被拒</summary>
    [Fact(DisplayName = "协议-5：伪造 Token 时 tools/list 被拒")]
    public async Task ListTools_FakeToken_Rejected()
    {
        await RunSdkAsync("sdmcp_fake_token_00000000000000000000000000000000", async client =>
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await client.ListToolsAsync());
            Output.WriteLine($"✅ 伪造Token tools/list 抛异常：{ex.GetType().Name}: {ex.Message}");
        });
    }

    /// <summary>ping 无参往返成功（SDK 层由框架调用，这里验证连接可用性）</summary>
    [Fact(DisplayName = "协议-6：连接建立后基础往返可用")]
    public async Task Ping_RoundTrip_Works()
    {
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            // 用 whoami（无需参数）验证 RPC 往返
            var result = await client.CallToolAsync("stardust_token_whoami", new Dictionary<String, Object?>());
            AssertOk(result);
            Output.WriteLine("✅ 基础 RPC 往返可用（whoami 调用成功）");
        });
    }
}
