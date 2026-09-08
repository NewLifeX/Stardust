using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>网关配置接口鉴权安全测试（安全V-10）。验证 /Gateway/* 需应用身份，防公网拓扑泄露</summary>
public class GatewaySecurityTests
{
    private readonly TestServer _server;

    public GatewaySecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<HttpResponseMessage> GetConfigAsync(String query)
    {
        var client = _server.CreateClient();
        return await client.GetAsync($"/Gateway/config?{query}");
    }

    private App CreateApp(String name, String secret)
    {
        var app = new App { Name = name, Enable = true };
        if (!secret.IsNullOrEmpty()) app.Secret = secret;
        app.Insert();
        return app;
    }

    [Fact(DisplayName = "无凭据访问网关配置被401拒绝")]
    public async Task NoCredential_Rejected()
    {
        var resp = await GetConfigAsync("");

        Assert.Equal(401, (Int32)resp.StatusCode);
    }

    [Fact(DisplayName = "错误密钥访问被401拒绝")]
    public async Task WrongSecret_Rejected()
    {
        var app = CreateApp("gw_" + Rand.NextString(8), "gw-secret");
        try
        {
            var resp = await GetConfigAsync($"appId={app.Name}&secret=wrong");

            Assert.Equal(401, (Int32)resp.StatusCode);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "空密钥应用无法通过凭据校验")]
    public async Task EmptySecretApp_Rejected()
    {
        var app = CreateApp("gw_" + Rand.NextString(8), "");
        try
        {
            var resp = await GetConfigAsync($"appId={app.Name}&secret=");

            Assert.Equal(401, (Int32)resp.StatusCode);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "正确应用密钥可读取网关配置（StarGateway 路径回归）")]
    public async Task ValidSecret_Ok()
    {
        var app = CreateApp("gw_" + Rand.NextString(8), "gw-secret");
        try
        {
            var resp = await GetConfigAsync($"appId={app.Name}&secret=gw-secret");
            var body = await resp.Content.ReadAsStringAsync();

            Assert.NotEqual(401, (Int32)resp.StatusCode);
            Assert.False(body.IsNullOrEmpty());
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "有效应用令牌可读取网关配置")]
    public async Task ValidToken_Ok()
    {
        var app = CreateApp("gw_" + Rand.NextString(8), "gw-secret");
        try
        {
            var client = _server.CreateClient();
            var rs = await client.PostAsync<LoginResponse>("app/login", new { AppId = app.Name, Secret = "gw-secret" });
            Assert.NotNull(rs?.Token);

            var resp = await client.GetAsync($"/Gateway/config?token={rs.Token}");
            Assert.NotEqual(401, (Int32)resp.StatusCode);
        }
        finally
        {
            app.Delete();
        }
    }
}
