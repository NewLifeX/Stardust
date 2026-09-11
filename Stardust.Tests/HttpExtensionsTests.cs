using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Stardust.Extensions;
using Xunit;

namespace Stardust.Tests;

/// <summary>Http扩展的客户端IP解析测试。覆盖单一IP折叠与可信代理链解析防伪造</summary>
public class HttpExtensionsTests : IDisposable
{
    private readonly String?[]? _oldTrusted;

    /// <summary>实例化，保存原始可信代理配置</summary>
    public HttpExtensionsTests() => _oldTrusted = HttpExtensions.TrustedProxies;

    /// <summary>恢复原始可信代理配置</summary>
    public void Dispose() => HttpExtensions.TrustedProxies = _oldTrusted;

    private static DefaultHttpContext CreateContext(String remoteIp)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        return ctx;
    }

    [Fact(DisplayName = "未配置可信代理：兼容旧行为取转发头链首并折叠单一IP")]
    public void NoTrusted_TakeFirstForwarded()
    {
        HttpExtensions.TrustedProxies = null;

        var ctx = CreateContext("10.0.0.1");
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 10.0.0.1";

        Assert.Equal("1.2.3.4", ctx.GetUserHost());
    }

    [Fact(DisplayName = "未配置可信代理：无转发头时取直连地址")]
    public void NoTrusted_FallbackRemote()
    {
        HttpExtensions.TrustedProxies = null;

        var ctx = CreateContext("10.9.8.7");

        Assert.Equal("10.9.8.7", ctx.GetUserHost());
    }

    [Fact(DisplayName = "可信代理CIDR：直连可信时从链上取真实客户端")]
    public void TrustedCidr_ParseChain()
    {
        HttpExtensions.TrustedProxies = ["10.0.0.0/8"];

        var ctx = CreateContext("10.0.0.1");
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 10.0.0.5";

        Assert.Equal("1.2.3.4", ctx.GetUserHost());
    }

    [Fact(DisplayName = "可信代理：直连不可信时忽略转发头防伪造")]
    public void UntrustedDirect_IgnoreForwarded()
    {
        HttpExtensions.TrustedProxies = ["10.0.0.0/8"];

        var ctx = CreateContext("8.8.8.8");
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        Assert.Equal("8.8.8.8", ctx.GetUserHost());
    }

    [Fact(DisplayName = "可信代理通配：10.* 形式生效")]
    public void TrustedWildcard_Works()
    {
        HttpExtensions.TrustedProxies = ["10.*"];

        var ctx = CreateContext("10.1.2.3");
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        Assert.Equal("1.2.3.4", ctx.GetUserHost());
    }
}
