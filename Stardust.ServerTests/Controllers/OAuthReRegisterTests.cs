using System.Net.Http.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server;
using Stardust.Server.Models;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>OAuth令牌接口重注册模式测试。验证收敛窗口/开放模式放行存量错密钥应用，并在客户端携带正确密钥后收敛关窗</summary>
public class OAuthReRegisterTests
{
    private readonly TestServer _server;

    public OAuthReRegisterTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private static TokenInModel BuildModel(String name, String password) => new()
    {
        grant_type = "password",
        UserName = name,
        Password = password,
        ClientId = "oar@1",
    };

    private async Task<String> PostAsync(String name, String password)
    {
        var client = _server.CreateClient();
        var response = await client.PostAsync("oauth/token", JsonContent.Create(BuildModel(name, password)));

        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "关闭模式错密钥被拒绝")]
    public async Task Off_WrongPassword_Rejected()
    {
        var name = "oar_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "oar-off-1", Enable = true, ReRegister = ReRegisterModes.Off };
        app.Insert();
        try
        {
            var body = await PostAsync(name, "wrong-password");

            Assert.Contains("非法访问应用", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "开放模式错密钥放行并下发密钥")]
    public async Task Open_WrongPassword_Allowed()
    {
        var name = "oar_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "oar-open-1", Enable = true, ReRegister = ReRegisterModes.Open };
        app.Insert();
        try
        {
            var body = await PostAsync(name, null);

            // 标准令牌字段保持，并额外携带密钥供客户端保存后收敛
            Assert.Contains("\"access_token\"", body);
            Assert.Contains("\"refresh_token\"", body);
            Assert.Contains("\"token_type\":\"JWT\"", body);
            Assert.Contains("\"secret\":\"oar-open-1\"", body);
            Assert.Equal(ReRegisterModes.Open, App.FindByName(name)?.ReRegister);

            // 旧客户端仍可正常反序列化标准字段，忽略额外的secret字段
            var client = _server.CreateClient();
            var model = await client.PostAsync<TokenModel>("oauth/token", BuildModel(name, null));
            Assert.NotEmpty(model.AccessToken);
            Assert.NotEmpty(model.RefreshToken);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口错密钥放行，携带正确密钥后自动关窗")]
    public async Task Auto_AllowThenConverge_Closed()
    {
        var name = "oar_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "oar-auto-1", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            // 第一次：错误密钥按收敛窗口放行，随令牌下发密钥
            var body1 = await PostAsync(name, "wrong-password");
            Assert.Contains("\"secret\":\"oar-auto-1\"", body1);
            Assert.Equal(ReRegisterModes.Auto, App.FindByName(name)?.ReRegister);

            // 第二次：携带正确密钥，收敛窗口自动关闭，不再下发密钥
            var body2 = await PostAsync(name, "oar-auto-1");
            Assert.Contains("\"access_token\"", body2);
            Assert.DoesNotContain("\"secret\":", body2);
            Assert.Equal(ReRegisterModes.Off, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式错密钥被拒绝并提示")]
    public async Task Trusted_WrongPassword_Rejected()
    {
        var name = "oar_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "oar-trust-1", Enable = true, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        try
        {
            var body = await PostAsync(name, "wrong-password");

            Assert.Contains("可信接入", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "空密钥应用在开放模式下放行")]
    public async Task Open_EmptySecret_Allowed()
    {
        var name = "oar_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "oar-empty-1", Enable = true, ReRegister = ReRegisterModes.Open };
        app.Insert();
        try
        {
            // 模拟存量脏数据：绕过 Valid 直接置空密钥
            App.Update("Secret=null", $"Name='{name}'");

            var body = await PostAsync(name, null);

            Assert.Contains("\"access_token\"", body);
        }
        finally
        {
            app.Delete();
        }
    }
}
