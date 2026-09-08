using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Stardust.Server;
using Xunit;

namespace ServerTest.Services;

/// <summary>限流中间件单元测试（安全扫描 V-8）。使用独立来源IP避免共享计数串扰</summary>
public class RateLimitMiddlewareTests
{
    private static Int32 _seed = 0;

    private static String NewIP()
    {
        _seed++;
        return $"10.{(Int32)(_seed / 250)}.{_seed % 250}.{new Random().Next(2, 254)}";
    }

    private static (RateLimitMiddleware mw, DefaultHttpContext ctx) Build(String paths, Int32 times, String path, String ip)
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        ctx.Request.Path = path;
        return (new RateLimitMiddleware(next, paths, times), ctx);
    }

    [Fact(DisplayName = "同一IP同一路径超过阈值被429拒绝")]
    public async Task OverThreshold_Rejected()
    {
        var ip = NewIP();
        var (mw, ctx) = Build("/App/Login,/Node/Login", 3, "/App/Login", ip);

        for (var i = 0; i < 3; i++)
        {
            await mw.Invoke(ctx);
            Assert.Equal(200, ctx.Response.StatusCode);
        }

        await mw.Invoke(ctx);
        Assert.Equal(429, ctx.Response.StatusCode);
    }

    [Fact(DisplayName = "非限流路径不受限制")]
    public async Task UnlistedPath_Allowed()
    {
        var ip = NewIP();
        var (mw, ctx) = Build("/App/Login,/Node/Login", 3, "/api", ip);

        for (var i = 0; i < 10; i++)
        {
            await mw.Invoke(ctx);
            Assert.Equal(200, ctx.Response.StatusCode);
        }
    }

    [Fact(DisplayName = "不同来源IP独立计数")]
    public async Task DifferentIP_Independent()
    {
        // 第一个IP已触发429，第二个IP仍应放行
        var ip1 = NewIP();
        var (mw1, ctx1) = Build("/App/Login", 2, "/App/Login", ip1);
        for (var i = 0; i < 3; i++) await mw1.Invoke(ctx1);
        Assert.Equal(429, ctx1.Response.StatusCode);

        var ip2 = NewIP();
        var (mw2, ctx2) = Build("/App/Login", 2, "/App/Login", ip2);
        // 第二个IP打满自身阈值(2次)仍应全部放行，证明未继承第一个IP的计数
        for (var i = 0; i < 2; i++)
        {
            await mw2.Invoke(ctx2);
            Assert.Equal(200, ctx2.Response.StatusCode);
        }
    }

    [Fact(DisplayName = "路径前缀匹配不区分大小写")]
    public async Task Path_CaseInsensitive()
    {
        var ip = NewIP();
        var (mw, ctx) = Build("/App/Login", 2, "/app/login", ip);

        await mw.Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);
        await mw.Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);
        await mw.Invoke(ctx);
        Assert.Equal(429, ctx.Response.StatusCode);
    }

    [Fact(DisplayName = "优先取X-Forwarded-For真实客户端IP")]
    public async Task XForwardedFor_Used()
    {
        // 反代场景：直连地址相同，但 XFF 不同 → 独立计数
        var mwPaths = "/App/Login";

        var ctx1 = new DefaultHttpContext();
        ctx1.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        ctx1.Request.Headers["X-Forwarded-For"] = "8.8.8.8";
        ctx1.Request.Path = "/App/Login";
        var mw1 = new RateLimitMiddleware(_ => Task.CompletedTask, mwPaths, 2);

        var ctx2 = new DefaultHttpContext();
        ctx2.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        ctx2.Request.Headers["X-Forwarded-For"] = "8.8.4.4";
        ctx2.Request.Path = "/App/Login";
        var mw2 = new RateLimitMiddleware(_ => Task.CompletedTask, mwPaths, 2);

        for (var i = 0; i < 3; i++) await mw1.Invoke(ctx1);
        Assert.Equal(429, ctx1.Response.StatusCode);

        // 不同XFF的真实客户端IP独立计数，打满自身阈值(2次)仍放行
        for (var i = 0; i < 2; i++)
        {
            await mw2.Invoke(ctx2);
            Assert.Equal(200, ctx2.Response.StatusCode);
        }
    }
}
