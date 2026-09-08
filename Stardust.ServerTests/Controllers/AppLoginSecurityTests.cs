using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>应用登录安全测试。验证已存在应用无法通过错误/空密钥触发动态注册冒用身份（安全扫描 V-1）</summary>
public class AppLoginSecurityTests
{
    private readonly TestServer _server;

    public AppLoginSecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private async Task<String> Login(String appId, String secret)
    {
        var client = _server.CreateClient();
        var model = new { AppId = appId, Secret = secret };
        var content = JsonContent.Create(model);
        var response = await client.PostAsync("app/login", content);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "已存在应用错误密钥登录被拒绝，不泄露令牌与真实密钥")]
    public async Task Login_ExistingApp_WrongSecret_Rejected()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-abc-123", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "wrong-secret");

            // 不得泄露真实密钥
            Assert.DoesNotContain("secret-abc-123", body, StringComparison.OrdinalIgnoreCase);
            // 不得签发令牌，应返回业务错误
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已存在应用空密钥登录被拒绝")]
    public async Task Login_ExistingApp_EmptySecret_Rejected()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-abc-456", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "");

            // 不得泄露真实密钥，应返回业务错误
            Assert.DoesNotContain("secret-abc-456", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已存在应用正确密钥登录成功（回归保护）")]
    public async Task Login_ExistingApp_CorrectSecret_Ok()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-ok-789", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "secret-ok-789");

            // 正确密钥应正常签发令牌
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "全新应用自动注册仍可用（回归保护）")]
    public async Task Login_NewApp_AutoRegister_Ok()
    {
        var name = "sec_" + Rand.NextString(8);
        try
        {
            var body = await Login(name, "");

            // 新应用自动注册仍应签发令牌并下发新密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"secret\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            App.FindByName(name)?.Delete();
        }
    }
}
