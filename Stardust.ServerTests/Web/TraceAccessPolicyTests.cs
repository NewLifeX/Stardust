extern alias web;

using System;
using Xunit;
using web::Stardust.Web.Services;

namespace ServerTest.Web;

/// <summary>调用链访问判定测试</summary>
public class TraceAccessPolicyTests
{
    [Theory(DisplayName = "登录用户、分享令牌、匿名开关依次放行")]
    [InlineData(true, false, false, TraceAccessModes.User)]
    [InlineData(true, true, true, TraceAccessModes.User)]
    [InlineData(false, true, false, TraceAccessModes.Token)]
    [InlineData(false, true, true, TraceAccessModes.Token)]
    [InlineData(false, false, true, TraceAccessModes.Anonymous)]
    [InlineData(false, false, false, TraceAccessModes.None)]
    public void Evaluate(Boolean signedIn, Boolean tokenValid, Boolean anonymous, TraceAccessModes expected)
    {
        Assert.Equal(expected, TraceAccessPolicy.Evaluate(signedIn, tokenValid, anonymous));
    }

    [Fact(DisplayName = "W3C traceparent 可归一化为追踪编号")]
    public void ParseTraceIds_TraceParent()
    {
        var ids = TraceAccessPolicy.ParseTraceIds("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        Assert.Single(ids);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", ids[0]);
    }

    [Fact(DisplayName = "逗号分隔的多个追踪编号全部保留")]
    public void ParseTraceIds_Multiple()
    {
        var ids = TraceAccessPolicy.ParseTraceIds("aaa,bbb , ccc");

        Assert.Equal(3, ids.Length);
        Assert.Equal("aaa", ids[0]);
        Assert.Equal("bbb", ids[1]);
        Assert.Equal("ccc", ids[2]);
    }

    [Fact(DisplayName = "空编号返回空数组，归一化返回空")]
    public void ParseTraceIds_Empty()
    {
        Assert.Empty(TraceAccessPolicy.ParseTraceIds(null));
        Assert.Empty(TraceAccessPolicy.ParseTraceIds(""));
        Assert.Null(TraceAccessPolicy.NormalizeTraceId(" , "));
    }
}
