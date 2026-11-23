using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>埋点中转账测试。覆盖应用令牌中转、AppId 强制归属、客户端IP保留与非法令牌拒绝</summary>
[Collection(StarTestServerCollection.Name)]
public class TraceRelayTests : StarTestBase
{
    public TraceRelayTests(StarTestServer fixture) : base(fixture) { }

    private App CreateApp(String prefix, out AppTracer tracer)
    {
        var name = prefix + "_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = prefix + "-secret", Enable = true };
        app.Insert();

        tracer = new AppTracer { Name = name, AppId = app.Id, Enable = true };
        tracer.Insert();

        return app;
    }

    private static void Cleanup(App app)
    {
        var tracer = AppTracer.FindByName(app.Name);
        if (tracer != null)
        {
            foreach (var item in TraceItem.FindAllByApp(tracer.ID))
            {
                item.Delete();
            }
            tracer.Delete();
        }
        app.Delete();
    }

    private async Task<String> LoginAsync(String name, String secret)
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = secret });
        Assert.NotNull(rs?.Token);

        return rs!.Token;
    }

    private async Task<String> IssueBrowserTokenAsync(String appId)
    {
        var client = _server.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/stardust/browser-token")
        {
            Content = JsonContent.Create(new { AppId = appId }),
        };
        req.Headers.Add("Origin", "https://demo.example.com");

        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        var match = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static Object BuildPayload(String appId, String itemName, Int64 now, Int32 builders = 1)
    {
        var list = Enumerable.Range(0, builders).Select(i => new
        {
            Name = builders == 1 ? itemName : $"{itemName}/{i}",
            StartTime = now,
            EndTime = now + 10,
            Total = 1,
            Errors = 0,
            Cost = 10,
            MaxCost = 10,
            MinCost = 10,
            Samples = new[]
            {
                new { Id = "span-" + i, ParentId = "", TraceId = "4bf92f3577b34da6a3ce929d0e0e4736", StartTime = now, EndTime = now + 10, Tag = "relay", Error = "" }
            },
        }).ToArray();

        return new
        {
            AppId = appId,
            ClientId = "browser@relay",
            Version = "3.7",
            Builders = list,
        };
    }

    [Fact(DisplayName = "应用令牌可中转浏览器报文，AppId 强制归属令牌主体并保留客户端IP")]
    public async Task Relay_ForcesAppIdAndKeepsClientIp()
    {
        var appA = CreateApp("tra", out var tracerA);
        var appB = CreateApp("trb", out var tracerB);
        var itemName = "page.load/" + Rand.NextString(6);
        try
        {
            var token = await LoginAsync(appA.Name, appA.Secret);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 报文冒充应用B，服务端必须按令牌主体归属到应用A
            var payload = BuildPayload(appB.Name, itemName, now);

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("/Trace/ReportRelay?clientIp=203.0.113.7", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, body);
            Assert.Contains("\"code\":0", body);

            var item = TraceItem.FindByAppIdAndName(tracerA.ID, itemName);
            Assert.NotNull(item);
            var datas = TraceData.Search(tracerA.ID, item!.Id, null, DateTime.Today, 10);
            Assert.NotNull(datas);
            Assert.NotEmpty(datas);
            Assert.Equal("203.0.113.7", datas[0].CreateIP);
            Assert.Equal("browser@relay", datas[0].ClientId);

            // 被冒充的应用不产生数据
            Assert.Null(TraceItem.FindByAppIdAndName(tracerB.ID, itemName));
        }
        finally
        {
            Cleanup(appB);
            Cleanup(appA);
        }
    }

    [Fact(DisplayName = "浏览器短期令牌不能用于中转")]
    public async Task Relay_BrowserToken_Rejected()
    {
        var app = CreateApp("trc", out _);
        var itemName = "page.load/" + Rand.NextString(6);
        try
        {
            var token = await IssueBrowserTokenAsync(app.Name);
            Assert.False(token.IsNullOrEmpty());

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var client = _server.CreateClient();
            var resp = await client.PostAsync("/Trace/ReportRelay?Token=" + Uri.EscapeDataString(token), new StringContent(JsonSerializer.Serialize(BuildPayload(app.Name, itemName, now)), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("\"code\":401", body);
        }
        finally
        {
            Cleanup(app);
        }
    }

    [Fact(DisplayName = "匿名请求不能使用中转接口")]
    public async Task Relay_Anonymous_Rejected()
    {
        var app = CreateApp("trd", out _);
        var itemName = "page.load/" + Rand.NextString(6);
        try
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var client = _server.CreateClient();
            var resp = await client.PostAsync("/Trace/ReportRelay", new StringContent(JsonSerializer.Serialize(BuildPayload(app.Name, itemName, now)), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("\"code\"", body);
            Assert.DoesNotContain("\"code\":0", body);
        }
        finally
        {
            Cleanup(app);
        }
    }

    [Fact(DisplayName = "中转接口支持 GZip 压缩报文")]
    public async Task Relay_GzipBody_Success()
    {
        var app = CreateApp("tre", out var tracer);
        var itemName = "page.load/" + Rand.NextString(6);
        try
        {
            var token = await LoginAsync(app.Name, app.Secret);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(BuildPayload(app.Name, itemName, now));
                gz.Write(bytes, 0, bytes.Length);
            }

            var content = new ByteArrayContent(ms.ToArray());
            content.Headers.ContentType = new("application/x-gzip");

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("/Trace/ReportRelay", content);
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, body);
            Assert.Contains("\"code\":0", body);
            Assert.NotNull(TraceItem.FindByAppIdAndName(tracer.ID, itemName));
        }
        finally
        {
            Cleanup(app);
        }
    }

    [Fact(DisplayName = "浏览器令牌单次上报条数超限被拒绝")]
    public async Task Report_BrowserTokenTooManyBuilders_Rejected()
    {
        var app = CreateApp("trf", out _);
        var itemName = "page.load/" + Rand.NextString(6);
        try
        {
            var token = await IssueBrowserTokenAsync(app.Name);
            Assert.False(token.IsNullOrEmpty());

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var payload = BuildPayload(app.Name, itemName, now, 101);

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("/Trace/Report", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("\"code\":400", body);
        }
        finally
        {
            Cleanup(app);
        }
    }
}
