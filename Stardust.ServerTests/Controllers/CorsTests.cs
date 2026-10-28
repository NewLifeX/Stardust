using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>CORS 默认行为回归测试。默认未配置 CorsOrigins 时保持允许任意来源，不破坏浏览器端接入（安全 V-15）</summary>
[Collection(StarTestServerCollection.Name)]
public class CorsTests : StarTestBase
{
    public CorsTests(StarTestServer fixture) : base(fixture) { }

    [Fact(DisplayName = "默认未配置CorsOrigins时允许任意来源跨域")]
    public async Task Default_AllowAnyOrigin()
    {
        var client = _server.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api");
        req.Headers.Add("Origin", "https://other.example.com");

        var resp = await client.SendAsync(req);
        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Origin", out var values), "响应缺少 Access-Control-Allow-Origin");
        Assert.Contains("*", values);
    }

    [Fact(DisplayName = "跨域预检允许浏览器令牌申请的POST请求")]
    public async Task Preflight_BrowserToken_AllowsPost()
    {
        var client = _server.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Options, "/stardust/browser-token");
        req.Headers.Add("Origin", "https://other.example.com");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");

        var resp = await client.SendAsync(req);

        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods), "预检响应缺少 Access-Control-Allow-Methods");
        Assert.Contains("POST", String.Join(",", methods));
    }

    [Fact(DisplayName = "跨域预检允许埋点上报的POST请求")]
    public async Task Preflight_TraceReport_AllowsPost()
    {
        var client = _server.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Options, "/Trace/Report");
        req.Headers.Add("Origin", "https://other.example.com");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");

        var resp = await client.SendAsync(req);

        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods), "预检响应缺少 Access-Control-Allow-Methods");
        Assert.Contains("POST", String.Join(",", methods));
    }
}
