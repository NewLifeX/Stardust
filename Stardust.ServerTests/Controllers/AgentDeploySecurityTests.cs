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

/// <summary>AgentDeploy 远程部署鉴权安全测试（安全扫描 V-3）</summary>
public class AgentDeploySecurityTests
{
    private readonly TestServer _server;

    public AgentDeploySecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<LoginResponse> AppLoginAsync(String appId, String secret)
    {
        var client = _server.CreateClient();
        return await client.PostAsync<LoginResponse>("app/login", new { AppId = appId, Secret = secret });
    }

    private async Task<String> TestConnectionAsync(String token)
    {
        var client = _server.CreateClient();
        if (!token.IsNullOrEmpty())
            client.DefaultRequestHeaders.Add("X-Token", token);

        var query = "host=127.0.0.1&port=22&userName=root&password=test-pass";
        var response = await client.PostAsync($"/AgentDeploy/TestConnection?{query}", null);
        return await response.Content.ReadAsStringAsync();
    }

    private App CreateApp(String name, String secret, String allowControl)
    {
        var app = new App { Name = name, Enable = true };
        if (!secret.IsNullOrEmpty()) app.Secret = secret;
        if (!allowControl.IsNullOrEmpty()) app.AllowControlNodes = allowControl;
        app.Insert();
        return app;
    }

    [Fact(DisplayName = "无令牌调用远程部署被拒绝")]
    public async Task NoToken_Rejected()
    {
        var body = await TestConnectionAsync("");

        Assert.Contains("\"code\"", body);
        Assert.Contains("令牌", body);
    }

    [Fact(DisplayName = "空密钥应用令牌调用远程部署被拒绝")]
    public async Task EmptySecretApp_Rejected()
    {
        var app = CreateApp("v3e_" + Rand.NextString(8), "", "*");
        try
        {
            var rs = await AppLoginAsync(app.Name, "");
            Assert.NotNull(rs?.Token);

            var body = await TestConnectionAsync(rs.Token);

            Assert.Contains("\"code\"", body);
            Assert.Contains("密钥", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "有密钥但无控制权的应用调用远程部署被拒绝")]
    public async Task NoControlRight_Rejected()
    {
        var app = CreateApp("v3c_" + Rand.NextString(8), "v3-secret", "");
        try
        {
            var rs = await AppLoginAsync(app.Name, "v3-secret");
            Assert.NotNull(rs?.Token);

            var body = await TestConnectionAsync(rs.Token);

            Assert.Contains("\"code\"", body);
            Assert.Contains("控制", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "有密钥且具控制权的管理应用可通过鉴权（回归保护）")]
    public async Task PrivilegedApp_Passes()
    {
        var app = CreateApp("v3p_" + Rand.NextString(8), "v3-secret", "*");
        try
        {
            var rs = await AppLoginAsync(app.Name, "v3-secret");
            Assert.NotNull(rs?.Token);

            var body = await TestConnectionAsync(rs.Token);

            // 已通过鉴权进入执行逻辑：不应再出现鉴权类错误
            // （测试环境无 /bin/bash，通常返回 success:false 或 500，均非鉴权拒绝）
            Assert.DoesNotContain("未提供令牌", body);
            Assert.DoesNotContain("禁止远程部署", body);
            Assert.DoesNotContain("无效应用", body);
        }
        finally
        {
            app.Delete();
        }
    }
}
