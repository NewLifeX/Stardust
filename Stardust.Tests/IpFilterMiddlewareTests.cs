using Microsoft.AspNetCore.Http;
using Stardust.Extensions;
using Xunit;

namespace Stardust.Tests;

/// <summary>IP过滤中间件测试。覆盖白名单匹配、空白名单放行、不匹配拒绝</summary>
public class IpFilterMiddlewareTests
{
    private static IpFilterMiddleware Create(String? whiteIPs, RequestDelegate? next = null)
    {
        next ??= static ctx => Task.CompletedTask;
        return new IpFilterMiddleware(next, "", whiteIPs ?? "");
    }

    private static DefaultHttpContext CreateContext(String ip)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        return ctx;
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task IpFilter_EmptyWhiteList_AllowAll(String? whiteIPs)
    {
        // 空白名单表示不校验，全部放行
        var mw = Create(whiteIPs);
        var ctx = CreateContext("10.0.0.1");

        await mw.Invoke(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.0.0.*")]
    [InlineData("192.168.1.5;10.0.0.1")]
    public async Task IpFilter_MatchingWhiteList_Pass(String whiteIPs)
    {
        // 白名单匹配（精确/通配符/多地址）时放行
        var mw = Create(whiteIPs);
        var ctx = CreateContext("10.0.0.1");

        await mw.Invoke(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task IpFilter_NotInWhiteList_Forbidden()
    {
        // 白名单不匹配时返回 403 拒绝
        var mw = Create("10.0.0.2");
        var ctx = CreateContext("10.0.0.1");

        await mw.Invoke(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task IpFilter_NotTrustForwardHeaders_IgnoresForgedHeader()
    {
        // 关闭转发头信任时，伪造 X-Forwarded-For 不生效，以真实远端地址为准
        var old = IpFilterMiddleware.TrustForwardHeaders;
        IpFilterMiddleware.TrustForwardHeaders = false;
        try
        {
            var mw = Create("10.0.0.1");
            var ctx = CreateContext("203.0.113.5");
            ctx.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

            await mw.Invoke(ctx);

            Assert.Equal(403, ctx.Response.StatusCode);
        }
        finally
        {
            IpFilterMiddleware.TrustForwardHeaders = old;
        }
    }

    [Fact]
    public async Task IpFilter_TrustForwardHeaders_AcceptHeader()
    {
        // 开启转发头信任（默认）时，转发头中的地址参与白名单判断
        var old = IpFilterMiddleware.TrustForwardHeaders;
        IpFilterMiddleware.TrustForwardHeaders = true;
        try
        {
            var mw = Create("10.0.0.1");
            var ctx = CreateContext("203.0.113.5");
            ctx.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

            await mw.Invoke(ctx);

            Assert.Equal(200, ctx.Response.StatusCode);
        }
        finally
        {
            IpFilterMiddleware.TrustForwardHeaders = old;
        }
    }
}
