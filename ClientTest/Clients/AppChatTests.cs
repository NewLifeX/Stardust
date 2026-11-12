using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Security;
using Stardust;
using Xunit;

namespace ClientTest.Clients;

/// <summary>AppClient AI对话测试。验证对话与流式对话按 OpenAI 协议复用登录令牌</summary>
public class AppChatTests
{
    private static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private static async Task WriteAsync(HttpListenerContext context, String body, String contentType)
    {
        var buffer = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    private static Task WriteAsync(HttpListenerContext context, String body) => WriteAsync(context, body, "application/json");

    private static async Task<String> ReadBodyAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);

        return await reader.ReadToEndAsync();
    }

    private const String ChatResponse = """{"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"pong"},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":1,"total_tokens":4}}""";

    private const String StreamResponse = "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"好\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"test-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
        "data: [DONE]\n\n";

    [Fact(DisplayName = "AI对话复用登录令牌并按OpenAI协议解析回复")]
    public async Task ChatAsync_SharesLoginToken()
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var token = "token-" + Rand.NextString(8);
        var aiAuthorization = "";
        var aiBody = "";
        var gotChat = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch { break; }

                var path = context.Request.Url?.AbsolutePath ?? "";
                if (path.EqualIgnoreCase("/api"))
                {
                    await WriteAsync(context, """{"serverTime":0}""");
                }
                else if (path.EndsWithIgnoreCase("/Login"))
                {
                    await WriteAsync(context, "{\"code\":0,\"data\":{\"name\":\"app\",\"token\":\"" + token + "\",\"expire\":7200}}");
                }
                else if (path.EqualIgnoreCase("/v1/chat/completions"))
                {
                    aiBody = await ReadBodyAsync(context);
                    if (aiBody.Contains("\"stream\":true"))
                    {
                        await WriteAsync(context, StreamResponse, "text/event-stream");
                    }
                    else
                    {
                        aiAuthorization = context.Request.Headers["Authorization"] ?? "";
                        await WriteAsync(context, ChatResponse);
                        gotChat.TrySetResult(true);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }, stop.Token);

        try
        {
            using var client = new AppClient(prefix) { AppId = "chat-test", Secret = "secret" };

            var rs = await client.ChatAsync("ping", "system-prompt");

            Assert.Equal("pong", rs?.Content);
            Assert.Equal("test-model", rs?.Model);
            Assert.Equal("stop", rs?.FinishReason);
            Assert.Equal(4, rs?.Usage?.TotalTokens);

            Assert.True(await gotChat.Task, "应发起AI对话请求");
            Assert.Equal("Bearer " + token, aiAuthorization);
            Assert.Contains("ping", aiBody);
            Assert.Contains("system-prompt", aiBody);
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            try { await server; } catch { }
        }
    }

    [Fact(DisplayName = "AI流式对话复用登录令牌并逐段返回")]
    public async Task ChatStreamAsync_SharesLoginToken()
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var token = "token-" + Rand.NextString(8);
        var streamAuthorization = "";
        var gotStream = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch { break; }

                var path = context.Request.Url?.AbsolutePath ?? "";
                if (path.EqualIgnoreCase("/api"))
                {
                    await WriteAsync(context, """{"serverTime":0}""");
                }
                else if (path.EndsWithIgnoreCase("/Login"))
                {
                    await WriteAsync(context, "{\"code\":0,\"data\":{\"name\":\"app\",\"token\":\"" + token + "\",\"expire\":7200}}");
                }
                else if (path.EqualIgnoreCase("/v1/chat/completions"))
                {
                    streamAuthorization = context.Request.Headers["Authorization"] ?? "";

                    await WriteAsync(context, StreamResponse, "text/event-stream");
                    gotStream.TrySetResult(true);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }, stop.Token);

        try
        {
            using var client = new AppClient(prefix) { AppId = "chat-test", Secret = "secret" };

            var sb = new StringBuilder();
            var finishReason = "";
            await foreach (var delta in client.ChatStreamAsync("ping"))
            {
                sb.Append(delta.Content);
                if (delta.FinishReason != null) finishReason = delta.FinishReason;
            }

            Assert.Equal("你好", sb.ToString());
            Assert.Equal("stop", finishReason);
            Assert.True(await gotStream.Task, "应发起AI流式对话请求");
            Assert.Equal("Bearer " + token, streamAuthorization);
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            try { await server; } catch { }
        }
    }
}
