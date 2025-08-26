using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using Stardust.Data;
using Xunit;

namespace Stardust.Server.Services.Tests;

public class AppServiceTests
{
    [Fact]
    public void AuthorizeTest()
    {
        var app = App.FindByName("test");
        if (app != null) app.Delete();

        var service = new AppTokenService();

        // 没有自动注册
        var ex = Assert.Throws<ApiException>(() => service.Authorize("test", "xxx", false));
        Assert.NotNull(ex);

        // 启用
        app = App.FindByName("test");
        app.Enable = true;
        app.Update();

        // 自动注册
        var rs = service.Authorize("test", "xxx", true);
        Assert.NotNull(rs);

        Assert.NotNull(app);
        Assert.Equal(app.Id, rs.Id);

        // 再次验证
        var rs2 = service.Authorize("test", "xxx", false);
        Assert.NotNull(rs2);
        Assert.Equal(app.Id, rs.Id);

        // 错误验证
        Assert.Throws<ApiException>(() => service.Authorize("test", "yyy", true));
    }

    [Fact]
    public void IssueTokenTest()
    {
        var app = new App { Name = "test" };

        var set = StarServerSetting.Current;
        var service = new TokenService(set);

        var model = service.IssueToken(app.Name, null);
        Assert.NotNull(model);

        Assert.Equal(3, model.AccessToken.Split('.').Length);
        Assert.Equal(3, model.RefreshToken.Split('.').Length);
        Assert.Equal(set.TokenExpire, model.ExpireIn);
        Assert.Equal("JWT", model.TokenType);
    }

    [Fact]
    public void DecodeTokenTest()
    {
        var app = App.FindByName("test");
        if (app == null)
        {
            app = new App { Name = "test", Enable = true };
            app.Insert();
        }

        var set = StarServerSetting.Current;
        var service = new TokenService(set);

        var model = service.IssueToken(app.Name, null);
        Assert.NotNull(model);

        //// 马上解码
        //var (jwt, app2) = service.DecodeToken(model.AccessToken, set.TokenSecret);
        //Assert.NotNull(jwt);
        //Assert.NotNull(app2);

        //(jwt, app2) = service.DecodeToken(model.RefreshToken, set.TokenSecret);
        //Assert.NotNull(jwt);
        //Assert.NotNull(app2);
    }
}