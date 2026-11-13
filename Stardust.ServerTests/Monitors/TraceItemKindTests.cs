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

    [Fact]
    [DisplayName("阻塞类命令单独归类，等待时长不混入依赖与应用耗时")]
    public void GetKind_ClassifiesBlockingCommand()
    {
        Assert.Equal("block", TraceItem.GetKind("NewLife.Redis:XREADGROUP-GROUP"));
        Assert.Equal("block", TraceItem.GetKind("NewLife.Redis:XREAD"));
        Assert.Equal("block", TraceItem.GetKind("NewLife.Redis:BLPOP"));
        Assert.Equal("block", TraceItem.GetKind("NewLife.Redis:BZPOPMIN"));

        // 普通命令与非阻塞命名不受影响
        Assert.Equal("NewLife.Redis", TraceItem.GetKind("NewLife.Redis:GET"));
        Assert.Equal("rpc", TraceItem.GetKind("rpc:Broadcast"));
        Assert.Equal("other", TraceItem.GetKind("BLPOP"));
    }

    [Fact]
    [DisplayName("阻塞类命令判定识别参数后缀且不受大小写影响")]
    public void IsBlocking_HandlesSuffixAndCase()
    {
        Assert.True(TraceItem.IsBlocking("NewLife.Redis:xreadgroup-group"));
        Assert.True(TraceItem.IsBlocking("NewLife.Redis:BRPOPLPUSH"));
        Assert.False(TraceItem.IsBlocking("NewLife.Redis:GET"));
        Assert.False(TraceItem.IsBlocking("rpc:Broadcast"));
        Assert.False(TraceItem.IsBlocking(null));
        Assert.False(TraceItem.IsBlocking(""));
    }
}
