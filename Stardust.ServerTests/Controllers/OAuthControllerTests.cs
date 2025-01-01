using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data;
using Stardust.Server.Models;
using Xunit;

namespace Stardust.Server.Controllers.Tests;

public class OAuthControllerTests
{
    private readonly TestServer _server;

    public OAuthControllerTests()
    {
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
    }

    [Fact]
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

    [Fact]
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
}