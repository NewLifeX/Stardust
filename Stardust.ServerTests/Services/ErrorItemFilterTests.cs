using System;
using System.ComponentModel;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>错误埋点过滤器测试</summary>
public class ErrorItemFilterTests
{
    [Fact]
    [DisplayName("默认忽略旧版客户端的本地代理探测 rpc:Info")]
    public void IsMatch_DefaultIgnoresRpcInfo()
    {
        Assert.True(ErrorItemFilter.IsMatch("rpc:Info", null));
        Assert.True(ErrorItemFilter.IsMatch("rpc:Info", ""));
        Assert.True(ErrorItemFilter.IsMatch("RPC:INFO", ErrorItemFilter.DefaultItems));
        Assert.False(ErrorItemFilter.IsMatch("/api/order", null));
        Assert.False(ErrorItemFilter.IsMatch(null, null));
    }

    [Fact]
    [DisplayName("忽略规则支持逗号分隔与通配符")]
    public void IsMatch_SupportsListAndWildcard()
    {
        Assert.True(ErrorItemFilter.IsMatch("/app/notify", "rpc:Info,/app/*"));
        Assert.True(ErrorItemFilter.IsMatch("db:Query", "db:*"));
        Assert.False(ErrorItemFilter.IsMatch("db:Query", "rpc:Info,redis:*"));
        Assert.True(ErrorItemFilter.IsMatch("anything", "*"));
    }
}
