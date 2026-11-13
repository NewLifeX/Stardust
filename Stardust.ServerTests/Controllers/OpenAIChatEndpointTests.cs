using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Security;
using ServerTest.Services;
using Stardust.Data;
using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>OpenAI 兼容对话接口端到端测试。用本地上游桩验证 snake_case 绑定、令牌鉴权与响应结构</summary>
[Collection(StarTestServerCollection.Name)]
public class OpenAIChatEndpointTests : StarTestBase
{
    public OpenAIChatEndpointTests(StarTestServer fixture) : base(fixture) { }

    private static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private static String ExtractToken(String body)
    {
        var m = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private const String ChatResponse = """{"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"stub-model","choices":[{"index":0,"message":{"role":"assistant","content":"pong"},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":1,"total_tokens":4}}""";

    private const String StreamResponse = "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"stub-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"stub-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你\"}}]}\n\n" +
        "data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"stub-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
        "data: [DONE]\n\n";

    /// <summary>启动本地上游桩，返回接收到的请求体</summary>
    private static async Task<(String prefix, Func<String> body, Action stop)> StartUpstream(String response, String contentType)
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}";

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix + "/");
        listener.Start();

        var received = "";
        var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var task = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch { break; }

                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) received = await reader.ReadToEndAsync();

                var buffer = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
            }
        }, stop.Token);

        return (prefix, () => received, () =>
        {
            stop.Cancel();
            listener.Stop();
            try { task.Wait(1000); } catch { }
            stop.Dispose();
        });
    }

    private static async Task<(HttpClient client, App app)> LoginAsync(TestServer server, String name, Int32 projectId = 0)
    {
        var app = new App
        {
            Name = name,
            Secret = "secret-" + Rand.NextString(8),
            Enable = true,
            ProjectId = projectId,
            DisplayName = "码神",
            Category = "Web",
        };
        app.Insert();

        var client = server.CreateClient();
        var resp = await client.PostAsync("app/login", new StringContent($"{{\"AppId\":\"{name}\",\"Secret\":\"{app.Secret}\"}}", Encoding.UTF8, "application/json"));
        var token = ExtractToken(await resp.Content.ReadAsStringAsync());
        Assert.False(token.IsNullOrEmpty(), "登录应下发令牌");

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (client, app);
    }

    [Fact(DisplayName = "OpenAI接口按snake_case绑定请求并返回chat.completion")]
    public async Task ChatCompletions_NonStreaming_ReturnsCompletion()
    {
        var upstream = await StartUpstream(ChatResponse, "application/json");
        var name = "ai_" + Rand.NextString(8);
        var project = new GalaxyProject { Name = "team_" + Rand.NextString(8), Enable = true };
        project.Insert();
        App app = null;
        try
        {
            using var scope = new AISettingScope(s =>
            {
                s.EnableAI = true;
                s.AIUrl = upstream.prefix;
                s.AIKey = "sk-test";
                s.AIModels = "stub-model";
                s.AISystemPrompt = "";
            });

            var (client, entity) = await LoginAsync(_server, name, project.Id);
            app = entity;

            var payload = """{"model":"stub-model","messages":[{"role":"user","content":"hi"}],"temperature":0.2,"max_tokens":64}""";
            var resp = await client.PostAsync("v1/chat/completions", new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "调用应成功：" + body);
            Assert.StartsWith("{\"id\":\"chatcmpl-", body);
            Assert.Contains("\"object\":\"chat.completion\"", body);
            Assert.Contains("\"content\":\"pong\"", body);
            Assert.Contains("\"finish_reason\":\"stop\"", body);
            Assert.Contains("\"total_tokens\":4", body);

            // 请求体按 OpenAI 协议原样转发，且透传调用方应用标识
            var sent = upstream.body();
            Assert.Contains("\"model\":\"stub-model\"", sent);
            Assert.Contains("\"max_tokens\":64", sent);
            Assert.Contains("\"temperature\":0.2", sent);
            Assert.Contains("\"content\":\"hi\"", sent);
            Assert.Contains("\"user\":\"" + name + "\"", sent);
            Assert.Contains("\"stream\":false", sent);

            // 注入的平台上下文系统消息带上应用与团队信息
            Assert.Contains("\"role\":\"system\"", sent);
            Assert.Contains("- Application: 码神 (" + name + "), category Web", sent);
            Assert.Contains("- Team: " + project.Name, sent);
        }
        finally
        {
            upstream.stop();
            app?.Delete();
            project.Delete();
        }
    }

    [Fact(DisplayName = "OpenAI接口在stream=true时返回chat.completion.chunk流")]
    public async Task ChatCompletions_Streaming_ReturnsChunks()
    {
        var upstream = await StartUpstream(StreamResponse, "text/event-stream");
        var name = "ai_" + Rand.NextString(8);
        App app = null;
        try
        {
            using var scope = new AISettingScope(s =>
            {
                s.EnableAI = true;
                s.AIUrl = upstream.prefix;
                s.AIKey = "sk-test";
                s.AIModels = "stub-model";
                s.AISystemPrompt = "";
            });

            var (client, entity) = await LoginAsync(_server, name);
            app = entity;

            var payload = """{"model":"stub-model","messages":[{"role":"user","content":"hi"}],"stream":true}""";
            var resp = await client.PostAsync("v1/chat/completions", new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "调用应成功：" + body);
            Assert.Contains("text/event-stream", resp.Content.Headers.ContentType + "");
            Assert.Contains("chat.completion.chunk", body);
            Assert.Contains("\"delta\":{\"role\":\"assistant\"}", body);
            Assert.Contains("\"content\":\"你\"", body);
            Assert.Contains("\"finish_reason\":\"stop\"", body);
            Assert.Contains("data: [DONE]", body);

            Assert.Contains("\"stream\":true", upstream.body());
        }
        finally
        {
            upstream.stop();
            app?.Delete();
        }
    }
}
