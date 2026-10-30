extern alias web;

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using web::Stardust.Web.Filters;
using web::Stardust.Web.Services;
using StarServerSetting = web::Stardust.Server.StarServerSetting;

namespace ServerTest.Web;

/// <summary>调用链访问授权过滤器测试</summary>
public class TraceAuthorizeFilterTests
{
    private const String TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const String Secret = "unit-test-secret";

    private static DefaultHttpContext CreateContext(String query)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/trace";
        ctx.Request.QueryString = new QueryString(query);

        var services = new ServiceCollection();
        services.AddSingleton<TraceShareTokenService>();
        ctx.RequestServices = services.BuildServiceProvider();

        return ctx;
    }

    /// <summary>固定密钥与匿名开关执行用例，结束后还原，避免用例相互干扰</summary>
    private static void WithSetting(Boolean anonymous, Action action)
    {
        var set = StarServerSetting.Current;
        var oldAnonymous = set.TraceAnonymous;
        var oldSecret = set.TraceShareSecret;
        try
        {
            set.TraceAnonymous = anonymous;
            set.TraceShareSecret = Secret;

            action();
        }
        finally
        {
            set.TraceAnonymous = oldAnonymous;
            set.TraceShareSecret = oldSecret;
        }
    }

    [Fact(DisplayName = "未登录且无令牌且未开匿名时拒绝访问")]
    public void AnonymousWithoutToken_Denied()
    {
        WithSetting(false, () =>
        {
            var ctx = CreateContext($"?id={TraceId}");

            var mode = TraceAuthorizeAttribute.Resolve(ctx, false);

            Assert.Equal(TraceAccessModes.None, mode);
            Assert.False(ctx.Items.ContainsKey("TraceId"));
            Assert.False(ctx.Response.Headers.ContainsKey("X-Robots-Tag"));
        });
    }

    [Fact(DisplayName = "有效分享令牌允许只读访问")]
    public void ValidToken_Allowed()
    {
        WithSetting(false, () =>
        {
            var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, DateTime.UtcNow);
            var ctx = CreateContext($"?id={TraceId}&token={token}");

            var mode = TraceAuthorizeAttribute.Resolve(ctx, false);

            Assert.Equal(TraceAccessModes.Token, mode);
            Assert.Equal(TraceId, ctx.Items["TraceId"]);
            Assert.True((Boolean)ctx.Items["TraceReadOnly"]!);
            Assert.IsType<DateTime>(ctx.Items["TraceExpire"]);
            Assert.Contains("noindex", ctx.Response.Headers["X-Robots-Tag"].ToString());
            Assert.Equal("no-store", ctx.Response.Headers["Cache-Control"].ToString());
            Assert.Equal("no-referrer", ctx.Response.Headers["Referrer-Policy"].ToString());
        });
    }

    [Fact(DisplayName = "登录用户打开分享链接同样带出有效期")]
    public void SignedInWithToken_ShowsExpire()
    {
        WithSetting(false, () =>
        {
            var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, DateTime.UtcNow);
            var ctx = CreateContext($"?id={TraceId}&token={token}&share=1");

            var mode = TraceAuthorizeAttribute.Resolve(ctx, true);

            Assert.Equal(TraceAccessModes.User, mode);
            Assert.IsType<DateTime>(ctx.Items["TraceExpire"]);
        });
    }

    [Fact(DisplayName = "过期或伪造的分享令牌被拒绝")]
    public void InvalidToken_Denied()
    {
        WithSetting(false, () =>
        {
            var expired = TraceShareTokenService.CreateToken(TraceId, Secret, 60, DateTime.UtcNow.AddMinutes(-120));
            Assert.Equal(TraceAccessModes.None, TraceAuthorizeAttribute.Resolve(CreateContext($"?id={TraceId}&token={expired}"), false));

            var forged = TraceShareTokenService.CreateToken(TraceId, "other-secret", 60, DateTime.UtcNow);
            Assert.Equal(TraceAccessModes.None, TraceAuthorizeAttribute.Resolve(CreateContext($"?id={TraceId}&token={forged}"), false));
        });
    }

    [Fact(DisplayName = "分享令牌不能用于多个追踪编号")]
    public void MultiTraceIds_TokenRejected()
    {
        WithSetting(false, () =>
        {
            var token = TraceShareTokenService.CreateToken(TraceId, Secret, 60, DateTime.UtcNow);
            var ctx = CreateContext($"?id={TraceId},ffffffffffffffffffffffffffffffff&token={token}");

            Assert.Equal(TraceAccessModes.None, TraceAuthorizeAttribute.Resolve(ctx, false));
        });
    }

    [Fact(DisplayName = "登录用户直接放行并可写")]
    public void SignedIn_Allowed()
    {
        WithSetting(false, () =>
        {
            var ctx = CreateContext($"?id={TraceId}");

            var mode = TraceAuthorizeAttribute.Resolve(ctx, true);

            Assert.Equal(TraceAccessModes.User, mode);
            Assert.False((Boolean)ctx.Items["TraceReadOnly"]!);
        });
    }

    [Fact(DisplayName = "开启匿名开关后未登录只读访问")]
    public void AnonymousSwitch_Allowed()
    {
        WithSetting(true, () =>
        {
            var ctx = CreateContext($"?id={TraceId}");

            var mode = TraceAuthorizeAttribute.Resolve(ctx, false);

            Assert.Equal(TraceAccessModes.Anonymous, mode);
            Assert.True((Boolean)ctx.Items["TraceReadOnly"]!);
        });
    }

    [Fact(DisplayName = "traceparent 输入被归一化为追踪编号")]
    public void TraceParentNormalized()
    {
        WithSetting(false, () =>
        {
            var ctx = CreateContext("?id=00-" + TraceId + "-00f067aa0ba902b7-01");

            TraceAuthorizeAttribute.Resolve(ctx, true);

            Assert.Equal(TraceId, ctx.Items["TraceId"]);
        });
    }
}
