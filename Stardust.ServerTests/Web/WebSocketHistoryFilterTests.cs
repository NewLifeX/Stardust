using System;
using System.ComponentModel;
using Stardust.Server.Controllers;
using Xunit;

namespace ServerTest.Web;

/// <summary>WebSocket 连接历史过滤器测试</summary>
public class WebSocketHistoryFilterTests
{
    [Fact]
    [DisplayName("重连风暴中窗口内的重复连接与断开只记录一次")]
    public void TryAccept_SuppressesRepeatedConnectionLogs()
    {
        WebSocketHistoryFilter.Reset();
        WebSocketHistoryFilter.Window = 60;

        var message = "State=Open sid=1 Remote=127.0.0.1:1234";
        Assert.True(WebSocketHistoryFilter.TryAccept("10.0.0.3@1000", "WebSocket connected", ref message));

        message = "State=Aborted CloseStatus= sid=1 Remote=127.0.0.1:1234";
        Assert.True(WebSocketHistoryFilter.TryAccept("10.0.0.3@1000", "WebSocket disconnected", ref message));

        // 同一会话在窗口内的第二次重连被抑制
        message = "State=Open sid=2 Remote=127.0.0.1:1235";
        Assert.False(WebSocketHistoryFilter.TryAccept("10.0.0.3@1000", "WebSocket connected", ref message));
        message = "State=Aborted CloseStatus= sid=2 Remote=127.0.0.1:1235";
        Assert.False(WebSocketHistoryFilter.TryAccept("10.0.0.3@1000", "WebSocket disconnected", ref message));

        // 其它会话与业务日志不受影响
        message = "State=Open sid=3 Remote=127.0.0.1:1236";
        Assert.True(WebSocketHistoryFilter.TryAccept("10.0.0.4@2000", "WebSocket connected", ref message));
        message = "App/Ping";
        Assert.True(WebSocketHistoryFilter.TryAccept("10.0.0.3@1000", "App/Ping", ref message));
        Assert.Equal("App/Ping", message);
    }

    [Fact]
    [DisplayName("断开日志附带本次连接时长，便于区分心跳超时与客户端重连")]
    public void TryAccept_AppendsConnectionDuration()
    {
        WebSocketHistoryFilter.Reset();
        WebSocketHistoryFilter.Window = 0;

        var message = "State=Open";
        Assert.True(WebSocketHistoryFilter.TryAccept("node-1", "WebSocket connected", ref message));

        message = "State=Aborted CloseStatus=";
        Assert.True(WebSocketHistoryFilter.TryAccept("node-1", "WebSocket disconnected", ref message));
        Assert.Contains("Elapsed=", message);
        Assert.Contains("s", message);
    }

    [Fact]
    [DisplayName("只拦截连接与断开动作")]
    public void IsConnectionAction_OnlyMatchesConnectionActions()
    {
        Assert.True(WebSocketHistoryFilter.IsConnectionAction("WebSocket connected"));
        Assert.True(WebSocketHistoryFilter.IsConnectionAction("WebSocket disconnected"));
        Assert.True(WebSocketHistoryFilter.IsConnectionAction("WebSocket连接"));
        Assert.True(WebSocketHistoryFilter.IsConnectionAction("WebSocket断开"));
        Assert.False(WebSocketHistoryFilter.IsConnectionAction("WebSocket send"));
        Assert.False(WebSocketHistoryFilter.IsConnectionAction("App/Ping"));
    }
}
