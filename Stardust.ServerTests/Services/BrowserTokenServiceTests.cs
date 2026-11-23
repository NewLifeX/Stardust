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
    private readonly BrowserTokenService _service = new(new CacheProvider()) { Secret = "unit-test-secret" };

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

    [Fact(DisplayName = "签名令牌不依赖缓存，可在另一实例校验")]
    public void SignedToken_Stateless_Validate()
    {
        var app = CreateApp();
        try
        {
            var first = new BrowserTokenService(new CacheProvider()) { Secret = "shared-secret" };
            var second = new BrowserTokenService(new CacheProvider()) { Secret = "shared-secret" };

            var token = first.Issue(app, "127.0.0.1", "https://demo.example.com");

            Assert.NotNull(second.Validate(token, "https://demo.example.com"));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "令牌绑定的来源不匹配时拒绝")]
    public void SignedToken_OriginMismatch_Rejected()
    {
        var app = CreateApp();
        try
        {
            var token = _service.Issue(app, "127.0.0.1", "https://demo.example.com");

            Assert.NotNull(_service.Validate(token, "https://demo.example.com"));
            Assert.Null(_service.Validate(token, "https://evil.example.com"));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "过期或签名被篡改的令牌被拒绝")]
    public void SignedToken_ExpiredOrTampered_Rejected()
    {
        const String secret = "unit-test-secret";
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var token = BrowserTokenService.CreateToken("app1", "https://a.com", secret, 60, now);

        Assert.True(BrowserTokenService.CheckToken(token, secret, "https://a.com", now.AddSeconds(30), out var name));
        Assert.Equal("app1", name);
        Assert.False(BrowserTokenService.CheckToken(token, secret, "https://a.com", now.AddSeconds(61), out _));

        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        Assert.False(BrowserTokenService.CheckToken(tampered, secret, "https://a.com", now.AddSeconds(1), out _));
    }

    [Fact(DisplayName = "旧版随机令牌继续通过缓存校验")]
    public void LegacyToken_CacheBacked_Ok()
    {
        var provider = new CacheProvider();
        var service = new BrowserTokenService(provider) { Secret = "unit-test-secret" };
        var app = CreateApp();
        try
        {
            var token = BrowserTokenService.Prefix + "legacy-" + Rand.NextString(8);
            provider.Cache.Set($"bw:{token}", app.Name, TimeSpan.FromMinutes(10));

            Assert.NotNull(service.Validate(token));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "上报限流按应用与来源IP计数")]
    public void CheckReportLimit_CountsPerAppAndIp()
    {
        var service = new BrowserTokenService(new CacheProvider()) { MaxReport = 2 };

        Assert.True(service.CheckReportLimit("app1", "10.0.0.1"));
        Assert.True(service.CheckReportLimit("app1", "10.0.0.1"));
        Assert.False(service.CheckReportLimit("app1", "10.0.0.1"));
        Assert.True(service.CheckReportLimit("app1", "10.0.0.2"));
        Assert.True(service.CheckReportLimit("app2", "10.0.0.1"));
    }

    [Fact(DisplayName = "超过签发上限时拒绝继续签发")]
    public void Issue_OverLimit_Rejected()
    {
        var service = new BrowserTokenService(new CacheProvider()) { MaxIssue = 3, Secret = "unit-test-secret" };
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
