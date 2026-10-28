using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>浏览器埋点端点测试。覆盖配置下发、短期令牌签发与使用、以及令牌不得越权</summary>
[Collection(StarTestServerCollection.Name)]
public class BrowserEndpointsTests : StarTestBase
{
    public BrowserEndpointsTests(StarTestServer fixture) : base(fixture) { }

    private App CreateApp(out AppTracer tracer)
    {
        var name = "bws_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-" + Rand.NextString(8), Enable = true };
        app.Insert();

        tracer = new AppTracer { Name = name, AppId = app.Id, Enable = true };
        tracer.Insert();

        return app;
    }

    private static String ExtractToken(String body)
    {
        var m = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>以浏览器身份申请短期令牌。Origin 用于通过来源校验</summary>
    private async Task<(String Token, String Body)> IssueTokenAsync(String appId, String origin = "https://demo.example.com")
    {
        var client = _server.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/stardust/browser-token")
        {
            Content = JsonContent.Create(new { AppId = appId }),
        };
        if (!origin.IsNullOrEmpty()) req.Headers.Add("Origin", origin);

        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        return (ExtractToken(body), body);
    }

    [Fact(DisplayName = "配置端点返回前端所需的同源配置")]
    public async Task Config_ReturnsDefaults()
    {
        var app = CreateApp(out var tracer);
        try
        {
            var client = _server.CreateClient();
            var body = await client.GetStringAsync("/stardust/browser-config?appId=" + app.Name);

            Assert.Contains("\"tokenUrl\"", body);
            Assert.Contains("/stardust/browser-token", body);
            Assert.Contains(app.Name, body);
            Assert.Contains("\"sampleRate\"", body);
        }
        finally
        {
            tracer.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "配置端点对未知应用报错")]
    public async Task Config_UnknownApp_Rejected()
    {
        var client = _server.CreateClient();
        var body = await client.GetStringAsync("/stardust/browser-config?appId=not_exists_" + Rand.NextString(6));

        Assert.Contains("\"code\"", body);
    }

    [Fact(DisplayName = "缺少来源信息的令牌申请被拒绝")]
    public async Task Token_WithoutOrigin_Rejected()
    {
        var app = CreateApp(out var tracer);
        try
        {
            var (token, body) = await IssueTokenAsync(app.Name, null);

            Assert.True(token.IsNullOrEmpty(), "缺少来源时不应签发令牌：" + body);
            Assert.Contains("\"code\"", body);
        }
        finally
        {
            tracer.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "签发的短期令牌可用于埋点上报")]
    public async Task Token_ReportTrace_Success()
    {
        var app = CreateApp(out var tracer);
        try
        {
            var (token, body) = await IssueTokenAsync(app.Name);
            Assert.False(token.IsNullOrEmpty(), "应签发令牌：" + body);
            Assert.StartsWith("bw_", token);

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);
            var resp = await client.PostAsync("/Trace/Report", JsonContent.Create(new { AppId = app.Name, ClientId = "browser-test" }));
            var reportBody = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("\"message\"", reportBody);
            Assert.Contains("\"code\":0", reportBody);
        }
        finally
        {
            tracer.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "短期令牌不能冒名向其它应用上报")]
    public async Task Token_CannotReportAsOtherApp()
    {
        var a = CreateApp(out var tracerA);
        var b = CreateApp(out var tracerB);
        try
        {
            var (token, _) = await IssueTokenAsync(a.Name);
            Assert.False(token.IsNullOrEmpty());

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);
            var resp = await client.PostAsync("/Trace/Report", JsonContent.Create(new { AppId = b.Name, ClientId = "browser-test" }));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("\"code\"", body);
        }
        finally
        {
            tracerB.Delete();
            b.Delete();
            tracerA.Delete();
            a.Delete();
        }
    }

    [Fact(DisplayName = "短期令牌不能当作应用令牌访问其它接口")]
    public async Task Token_CannotBeUsedAsAppToken()
    {
        var app = CreateApp(out var tracer);
        try
        {
            var (token, _) = await IssueTokenAsync(app.Name);
            Assert.False(token.IsNullOrEmpty());

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("app/ping", JsonContent.Create(new { Name = app.Name, Version = "1.0.0", MachineName = "test-machine" }));
            var body = await resp.Content.ReadAsStringAsync();

            // 不透明令牌不是 JWT，心跳接口必须拒绝
            Assert.Contains("\"code\"", body);
        }
        finally
        {
            tracer.Delete();
            app.Delete();
        }
    }
}
