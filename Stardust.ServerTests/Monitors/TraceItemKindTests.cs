using System;
using System.ComponentModel;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>跟踪项分类测试</summary>
public class TraceItemKindTests
{
    [Fact]
    [DisplayName("WebSocket 长连接端点单独归类，不与普通接口混算耗时")]
    public void GetKind_ClassifiesWebSocket()
    {
        Assert.Equal("ws", TraceItem.GetKind("/app/notify"));
        Assert.Equal("ws", TraceItem.GetKind("/node/notify"));
        Assert.Equal("ws", TraceItem.GetKind("ws:command"));
        Assert.Equal("api", TraceItem.GetKind("/api/order"));
        Assert.Equal("http", TraceItem.GetKind("https://newlifex.com"));
        Assert.Equal("mq", TraceItem.GetKind("redismq:topic"));
        Assert.Equal("rpc", TraceItem.GetKind("rpc:Info"));
        Assert.Equal("other", TraceItem.GetKind("AppStart"));
    }

    [Fact]
    [DisplayName("长连接判定不受大小写影响")]
    public void IsWebSocket_IgnoresCase()
    {
        Assert.True(TraceItem.IsWebSocket("/App/Notify"));
        Assert.True(TraceItem.IsWebSocket("WS:command"));
        Assert.False(TraceItem.IsWebSocket("AppStart"));
        Assert.False(TraceItem.IsWebSocket(null));
    }
}
