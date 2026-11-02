using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewLife;
using NewLife.Log;
using Stardust.Data.Platform;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>使用官方 ModelContextProtocol SDK 2.0 的客户端集成测试。
/// 验证 Stardust MCP 服务端（满血版：协议协商 + Streamable HTTP）与标准 SDK 的兼容性。
/// 全部硬断言：任一环节失败则测试失败，真实反映 MCP 能力可用性。
///
/// 说明：官方 SDK 2.0.0 的 StreamableHttpClientSessionTransport 在握手阶段存在瞬时竞争
/// （偶发 "POST response completed without a reply to request with ID: 1"），与服务器逻辑无关
/// （原生 RawMcpClient 测试恒过，且本测试重试后必过）。此处对握手瞬时错误做有限重试，
/// 断言失败与真实错误均立即抛出，不掩盖缺陷。</summary>
[Collection(nameof(McpTestCollection))]
public class SdkMcpClientTests : IAsyncLifetime
{
    private readonly McpTestServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private const String Endpoint = "http://localhost/mcp";

    public SdkMcpClientTests(McpTestServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>构造 SDK 的 Streamable HTTP 传输层（携带 Bearer Token）</summary>
    private HttpClientTransport CreateTransport(String? tokenStr = null)
    {
        var headers = new Dictionary<String, String>();
        if (!tokenStr.IsNullOrEmpty())
            headers["Authorization"] = $"Bearer {tokenStr}";

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(Endpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        };

        return new HttpClientTransport(transportOptions, _fixture.CreateClient());
    }

    /// <summary>创建 SDK 客户端并执行动作。对官方 SDK 握手/请求阶段的瞬时传输错误做有限重试；
    /// 断言失败与真实错误（非瞬时）立即抛出，不掩盖缺陷。</summary>
    private async Task RunSdkAsync(String? tokenStr, Func<McpClient, Task> action)
    {
        const Int32 max = 4;
        Exception? lastEx = null;
        for (var attempt = 1; attempt <= max; attempt++)
        {
            McpClient? client = null;
            try
            {
                client = await McpClient.CreateAsync(CreateTransport(tokenStr));
                await action(client);
                return;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastEx = ex;
                _output.WriteLine($"⚠ SDK瞬时传输错误（第{attempt}次）：{ex.GetType().Name}: {ex.Message}，重试...");
                await Task.Delay(300 * attempt);
            }
            finally
            {
                if (client != null) await client.DisposeAsync();
            }
        }
        if (lastEx != null) throw lastEx;
    }

    /// <summary>判断是否为瞬时传输错误（可重试）：官方 SDK 握手竞争、连接重置、超时等</summary>
    private static Boolean IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException or OperationCanceledException or TimeoutException ||
        (ex is McpException m && m.Message.Contains("without a reply"));

    /// <summary>核对服务端日志：打印到测试输出，并断言 happy-path 不应出现 [McpService] 错误日志</summary>
    private void AssertAndPrintServerLog()
    {
        McpTestLog.Instance.WriteTo(_output);
        Assert.False(McpTestLog.Instance.Contains("[McpService]", LogLevel.Error),
            "SDK 流程不应出现 [McpService] 服务端错误日志");
    }

    /// <summary>SDK initialize 握手。验证 Stardust MCP 服务端与官方 SDK 2.0 的协议协商兼容</summary>
    [Fact]
    public async Task Sdk_Initialize_Handshake()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("sdk-init");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);
            McpTestLog.Instance.Reset();

            await RunSdkAsync(tokenStr, client =>
            {
                Assert.NotNull(client.ServerInfo);
                Assert.Equal("Stardust", client.ServerInfo.Name);

                // 协商出的协议版本必须是服务端支持列表之一
                var negotiated = client.NegotiatedProtocolVersion;
                Assert.False(negotiated.IsNullOrEmpty());
                Assert.Contains(negotiated, new[] { "2026-07-28", "2025-06-18", "2025-03-26", "2024-11-05" });

                _output.WriteLine($"✅ SDK initialize 成功：ServerName={client.ServerInfo.Name}, NegotiatedProtocolVersion={negotiated}");
                return Task.CompletedTask;
            });
            AssertAndPrintServerLog();
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>SDK tools/list。零 scope 令牌至少能看到无 scope 要求的 whoami</summary>
    [Fact]
    public async Task Sdk_ListTools_IncludesWhoAmI()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("sdk-tools");
        try
        {
            McpTestLog.Instance.Reset();

            await RunSdkAsync(tokenStr, async client =>
            {
                var tools = await client.ListToolsAsync();
                var toolNames = tools.Select(t => t.Name).ToHashSet();
                Assert.Contains("stardust_token_whoami", toolNames);
                _output.WriteLine($"✅ SDK ListTools 成功：返回 {tools.Count} 个工具");
            });
            AssertAndPrintServerLog();
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>SDK CallToolAsync — 调用 whoami 自检</summary>
    [Fact]
    public async Task Sdk_CallTool_WhoAmI()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("sdk-whoami");
        try
        {
            McpTestLog.Instance.Reset();

            await RunSdkAsync(tokenStr, async client =>
            {
                var result = await client.CallToolAsync("stardust_token_whoami", new Dictionary<String, Object?>());
                Assert.NotNull(result);
                Assert.True(result.Content.Count > 0);
                var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
                Assert.NotNull(text);
                var content = JsonSerializer.Deserialize<JsonElement>(text!.Text);
                Assert.True(content.TryGetProperty("summary", out _), "whoami 应返回 summary");
                _output.WriteLine("✅ SDK CallTool whoami 成功");
            });
            AssertAndPrintServerLog();
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>SDK 无 Token 时 initialize 成功（不需鉴权），但 tools/list 必须失败（缺 Token 鉴权）</summary>
    [Fact]
    public async Task Sdk_NoToken_ToolsListFails()
    {
        // initialize 不需要 Token，应当成功建立连接
        McpTestLog.Instance.Reset();
        await RunSdkAsync(null, async client =>
        {
            Assert.NotNull(client);

            // tools/list 应因缺少 Token 鉴权而抛异常
            await Assert.ThrowsAnyAsync<Exception>(async () => await client.ListToolsAsync());
            _output.WriteLine($"✅ SDK 无Token时 initialize 成功，tools/list 抛异常");
        });
        AssertAndPrintServerLog();
    }
}
