using System.Net.Http;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewLife;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>面向真实 WebApi(6681) 的官方 MCP SDK 客户端基类。
/// 提供 Streamable HTTP 传输（Bearer Token）与有限重试封装，供各测试类复用。
/// 参考 Stardust.McpClientTests.SdkMcpClientTests（2.0.0 已验证写法）。</summary>
public abstract class McpSdkTestBase
{
    private readonly ITestOutputHelper _output;

    protected McpSdkTestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    protected ITestOutputHelper Output => _output;

    /// <summary>构造 SDK 的 Streamable HTTP 传输层（真实 6681，携带 Bearer Token）
    /// 构造签名 (options, HttpClient) 与既有 SdkMcpClientTests 一致（SDK 2.0.0 无单参构造）。</summary>
    private HttpClientTransport CreateTransport(String? tokenStr)
    {
        var headers = new Dictionary<String, String>();
        if (!tokenStr.IsNullOrEmpty())
            headers["Authorization"] = $"Bearer {tokenStr}";

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(McpTestDataInitializer.McpEndpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        };

        return new HttpClientTransport(transportOptions, new HttpClient());
    }

    /// <summary>创建 SDK 客户端并执行动作。对官方 SDK 握手/请求阶段的瞬时传输错误做有限重试；
    /// 断言失败与真实错误（非瞬时）立即抛出，不掩盖缺陷。</summary>
    protected async Task RunSdkAsync(String? tokenStr, Func<McpClient, Task> action)
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

    /// <summary>解析工具返回的 content[0].text 为 JsonElement</summary>
    protected static JsonElement ParseText(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(text);
        return JsonSerializer.Deserialize<JsonElement>(text!.Text);
    }

    /// <summary>断言调用成功并返回 Ok 结构（content 含 summary）</summary>
    protected static JsonElement AssertOk(CallToolResult result)
    {
        var content = ParseText(result);
        Assert.True(content.TryGetProperty("summary", out _), $"应返回 summary：{content}");
        return content;
    }

    /// <summary>断言远程调用被拒（SDK 抛 McpProtocolException），并校验服务端 JSON-RPC 错误码。
    /// SDK 2.0.0 将远程错误码原样保留在 McpProtocolException.ErrorCode（McpErrorCode 枚举可承载自定义负值）。
    /// 返回异常实例供进一步断言消息内容。</summary>
    protected static async Task<McpProtocolException> AssertRejectedAsync(Func<Task> action, Int32 expectedErrorCode)
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(action);
        var mpe = Assert.IsType<McpProtocolException>(ex);
        Assert.Equal(expectedErrorCode, (Int32)mpe.ErrorCode);
        return mpe;
    }
}
