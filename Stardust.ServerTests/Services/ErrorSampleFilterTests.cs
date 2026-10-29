using System;
using System.Collections.Generic;
using System.ComponentModel;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>异常采样过滤器测试</summary>
public class ErrorSampleFilterTests
{
    [Fact]
    [DisplayName("客户端提前断开请求体属于传输层中断，不计入应用错误")]
    public void IsTransportAbort_MatchesClientAbort()
    {
        Assert.True(ErrorSampleFilter.IsTransportAbort("Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException: Unexpected end of request content."));
        Assert.True(ErrorSampleFilter.IsTransportAbort("The client disconnected."));
        Assert.True(ErrorSampleFilter.IsTransportAbort("Connection reset by peer"));
        Assert.False(ErrorSampleFilter.IsTransportAbort("SqlException: Timeout expired"));
        Assert.False(ErrorSampleFilter.IsTransportAbort(""));
        Assert.False(ErrorSampleFilter.IsTransportAbort(null));
    }

    [Fact]
    [DisplayName("全部采样均为传输层中断时才清零错误数，且采样不能少于错误数")]
    public void FixErrors_OnlyWhenAllSamplesAreAborts()
    {
        var keywords = ErrorSampleFilter.DefaultKeywords;
        List<String> aborts = ["Unexpected end of request content", "The client disconnected"];

        Assert.Equal(0, ErrorSampleFilter.FixErrors(2, aborts, keywords));

        // 混入真实业务异常，保持原错误数
        List<String> mixed = ["Unexpected end of request content", "NullReferenceException"];
        Assert.Equal(2, ErrorSampleFilter.FixErrors(2, mixed, keywords));

        // 错误数多于采样数，说明存在未采样的错误，不能整体忽略
        Assert.Equal(9, ErrorSampleFilter.FixErrors(9, aborts, keywords));

        // 没有采样时不做判定
        Assert.Equal(1, ErrorSampleFilter.FixErrors(1, [], keywords));

        // 本来就没有错误
        Assert.Equal(0, ErrorSampleFilter.FixErrors(0, aborts, keywords));
    }
}
