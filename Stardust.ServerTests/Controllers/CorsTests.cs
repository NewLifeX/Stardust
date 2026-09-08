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
public class CorsTests
{
    private readonly TestServer _server;

    public CorsTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

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
}
