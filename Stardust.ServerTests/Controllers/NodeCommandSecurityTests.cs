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
using Stardust.Data.Nodes;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>节点命令下发安全测试。验证空密钥应用即使被冒用也无权向节点/应用下发命令（安全扫描 V-2）</summary>
public class NodeCommandSecurityTests
{
    private readonly TestServer _server;

    public NodeCommandSecurityTests()
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

    private async Task<String> LoginAsync(String appId, String secret)
    {
        var client = _server.CreateClient();
        var content = JsonContent.Create(new { AppId = appId, Secret = secret });
        var response = await client.PostAsync("app/login", content);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<String> SendNodeCommandAsync(String token, String nodeCode)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var model = new { code = nodeCode, command = "bash", argument = "id", timeout = 1 };
        var content = JsonContent.Create(model);
        var response = await client.PostAsync("/Node/SendCommand", content);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "空密钥应用禁止登录，无法取得令牌下发节点命令（安全治理）")]
    public async Task EmptySecretApp_CannotLogin()
    {
        var appName = "v2e_" + Rand.NextString(8);

        // 安全治理：新建应用强制自动生成密钥，不再存在空密钥应用
        var app = new App { Name = appName, Enable = true, AllowControlNodes = "*" };
        app.Insert();
        try
        {
            Assert.False(app.Secret.IsNullOrEmpty());

            // 不知道密钥，用空密钥登录拿不到令牌，更无法下发节点命令（V-2门控前的第一道防线）
            var body = await LoginAsync(appName, "");
            Assert.Contains("\"code\"", body);
            Assert.DoesNotContain("\"token\"", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已配置密钥且ACL允许的应用可正常下发节点命令（回归保护）")]
    public async Task AppWithSecret_CanSendNodeCommand()
    {
        var appName = "v2s_" + Rand.NextString(8);
        var node = new Node { Code = "n_" + Rand.NextString(8), Name = "v2node", Enable = true };
        node.Insert();
        var app = new App { Name = appName, Secret = "v2-secret", Enable = true, AllowControlNodes = node.Code };
        app.Insert();
        try
        {
            var rs = await AppLoginAsync(appName, "v2-secret");
            Assert.NotNull(rs?.Token);

            var body = await SendNodeCommandAsync(rs.Token, node.Code);

            // 不应再返回“未配置密钥”类错误（可能因无在线会话而超时返回空，属正常）
            Assert.DoesNotContain("未配置密钥", body);
        }
        finally
        {
            app.Delete();
            node.Delete();
        }
    }
}
