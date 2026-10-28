using System;
using NewLife;
using NewLife.Caching;
using NewLife.Remoting;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>浏览器埋点短期令牌服务单元测试。覆盖令牌识别、应用绑定与签发限流</summary>
public class BrowserTokenServiceTests
{
    private readonly BrowserTokenService _service = new(new CacheProvider());

    private static App CreateApp()
    {
        var app = new App { Name = "bws_" + Rand.NextString(8), Enable = true };
        app.Insert();
        return app;
    }

    [Fact(DisplayName = "签发的令牌以bw_开头且可校验到对应应用")]
    public void Issue_Validate_Ok()
    {
        var app = CreateApp();
        try
        {
            var token = _service.Issue(app, "127.0.0.1");
            Assert.StartsWith(BrowserTokenService.Prefix, token);

            var found = _service.Validate(token);
            Assert.NotNull(found);
            Assert.Equal(app.Name, found.Name);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "非浏览器令牌格式一律校验失败")]
    public void Validate_NotBrowserToken_Fail()
    {
        Assert.Null(_service.Validate(""));
        Assert.Null(_service.Validate(null));
        Assert.Null(_service.Validate("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhIn0.sig"));
        Assert.Null(_service.Validate(BrowserTokenService.Prefix + "not-issued"));
    }

    [Fact(DisplayName = "超过签发上限时拒绝继续签发")]
    public void Issue_OverLimit_Rejected()
    {
        var service = new BrowserTokenService(new CacheProvider()) { MaxIssue = 3 };
        var app = CreateApp();
        try
        {
            for (var i = 0; i < 3; i++) service.Issue(app, "10.0.0.1");

            var ex = Assert.Throws<ApiException>(() => service.Issue(app, "10.0.0.1"));
            Assert.Equal(ApiCode.Forbidden, ex.Code);

            // 其它来源IP不受影响
            Assert.False(service.Issue(app, "10.0.0.2").IsNullOrEmpty());
        }
        finally
        {
            app.Delete();
        }
    }
}
