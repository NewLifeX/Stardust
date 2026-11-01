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
        Assert.True(ErrorSampleFilter.IsTransportAbort("System.Net.WebSockets.WebSocketException: The remote party closed the WebSocket connection without completing the close handshake."));
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

    [Fact]
    [DisplayName("传输层中断的异常采样只保留首行和首个业务帧，避免长堆栈刷屏")]
    public void Summarize_CompressesTransportAbort()
    {
        var error = "System.Net.WebSockets.WebSocketException (0x80004005): The remote party closed the WebSocket connection without completing the close handshake." +
            Environment.NewLine + "   at System.Net.WebSockets.ManagedWebSocket.ThrowEOFUnexpected()" +
            Environment.NewLine + "   at NewLife.Remoting.Extensions.Services.WsCommandSession.ReceiveLoopAsync(IPEndPoint remote, CancellationTokenSource source)" +
            Environment.NewLine + "   at Stardust.Server.Controllers.AppController.HandleNotify(DeviceContext context, WebSocket socket, CancellationToken cancellationToken) in C:\\X\\Stardust\\Stardust.Server\\Controllers\\AppController.cs:line 210" +
            Environment.NewLine + "   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()";

        var summary = ErrorSampleFilter.Summarize(error);

        Assert.Contains("WebSocketException", summary);
        Assert.Contains("AppController.HandleNotify", summary);
        Assert.DoesNotContain("ThrowEOFUnexpected", summary);
        Assert.DoesNotContain("ControllerActionInvoker", summary);
        Assert.True(summary.Length < error.Length);
    }

    [Fact]
    [DisplayName("单行异常同样能压缩堆栈")]
    public void Summarize_CompressesSingleLineStack()
    {
        var error = "Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException: Unexpected end of request content." +
            " at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.Http1ContentLengthMessageBody.ReadAsyncInternal(CancellationToken cancellationToken)" +
            " at Stardust.Server.Controllers.TraceController.ReportRaw(String token) in C:\\X\\Stardust\\Stardust.Server\\Controllers\\TraceController.cs:line 90" +
            " at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()";

        var summary = ErrorSampleFilter.Summarize(error);

        Assert.Contains("BadHttpRequestException", summary);
        Assert.Contains("TraceController.ReportRaw", summary);
        Assert.DoesNotContain("Http1ContentLengthMessageBody", summary);
        Assert.DoesNotContain("ControllerActionInvoker", summary);
    }

    [Fact]
    [DisplayName("真实业务异常保留完整堆栈")]
    public void Summarize_KeepsRealError()
    {
        var error = "System.NullReferenceException: Object reference not set to an instance of an object." +
            Environment.NewLine + "   at Stardust.Server.Controllers.OrderController.Pay(Int32 id)";

        Assert.Equal(error, ErrorSampleFilter.Summarize(error));
    }
}
