using System.Net.Http.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data;
using Stardust.Server;
using Stardust.Server.Models;
using Xunit;

namespace ServerTest.Controllers;

public class OAuthControllerTests
{
    private readonly TestServer _server;

    public OAuthControllerTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    [Fact(DisplayName = "OAuth密码模式颁发令牌")]
    public async Task Token_password()
    {
        var app = App.FindByName("stone");
        if (app != null)
        {
            app.Enable = true;
            app.Update();
        }

        var model = new TokenInModel
        {
            grant_type = "password",
            UserName = "stone",
        };

        var client = _server.CreateClient();

        var rs = await client.PostAsync<TokenModel>("oauth/token", model);
        Assert.NotNull(rs);
        Assert.NotEmpty(rs.AccessToken);
        Assert.NotEmpty(rs.RefreshToken);
        Assert.Equal(7200, rs.ExpireIn);
        Assert.Equal("JWT", rs.TokenType);
    }

    [Fact(DisplayName = "OAuth刷新令牌模式续期")]
    public async Task Token_refresh_token()
    {
        var client = _server.CreateClient();

        var refresh_token = "";
        {
            var model = new TokenInModel
            {
                grant_type = "password",
                UserName = "stone",
            };

            var rs = await client.PostAsync<TokenModel>("oauth/token", model);
            Assert.NotNull(rs);
            Assert.NotEmpty(rs.RefreshToken);

            refresh_token = rs.RefreshToken;
        }

        // 刷新令牌
        {
            var model2 = new TokenInModel
            {
                grant_type = "refresh_token",
                refresh_token = refresh_token,
            };

            var rs2 = await client.PostAsync<TokenModel>("oauth/token", model2);
            Assert.NotNull(rs2);
            Assert.NotEmpty(rs2.AccessToken);
            Assert.NotEmpty(rs2.RefreshToken);
            Assert.Equal(7200, rs2.ExpireIn);
            Assert.Equal("JWT", rs2.TokenType);
        }
    }

    [Fact(DisplayName = "OAuth无效密码返回错误")]
    public async Task Token_InvalidPassword_ReturnsError()
    {
        var client = _server.CreateClient();
        var model = new { grant_type = "password", UserName = "stone", Password = "wrong-password" };
        var content = JsonContent.Create(model);

        var response = await client.PostAsync("oauth/token", content);
        var body = await response.Content.ReadAsStringAsync();

        // NewLife Remoting 协议：错误码在 body 的 code 字段
        Assert.Contains("\"code\":", body);
    }

    [Fact(DisplayName = "OAuth不支持的grant_type返回错误")]
    public async Task Token_UnsupportedGrantType_ReturnsError()
    {
        var client = _server.CreateClient();
        var model = new { grant_type = "client_credentials", UserName = "stone" };
        var content = JsonContent.Create(model);

        var response = await client.PostAsync("oauth/token", content);
        var body = await response.Content.ReadAsStringAsync();

        // 未支持的 grant_type 抛出 NotSupportedException，序列化到 body
        Assert.Contains("\"code\":", body);
    }
}