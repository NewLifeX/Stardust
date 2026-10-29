using System;
using System.ComponentModel;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>应用统计长连接排除测试</summary>
public class AppStatExcludeWebSocketTests
{
    [Fact]
    [DisplayName("应用级统计排除长连接埋点，避免连接时长污染平均耗时")]
    public void ExcludeWebSocket_SkipsConnectionSpans()
    {
        // 长连接端点由 TraceItem 归类为 ws，应用级聚合据此排除
        Assert.True(TraceItem.IsWebSocket("/app/notify"));
        Assert.Equal("ws", TraceItem.GetKind("/app/notify"));

        // 普通接口与基础设施埋点不受影响
        Assert.False(TraceItem.IsWebSocket("/App/Ping"));
        Assert.Equal("api", TraceItem.GetKind("/App/Ping"));
        Assert.False(TraceItem.IsWebSocket("NewLife.Redis:XREADGROUP-GROUP"));
        Assert.Equal("NewLife.Redis", TraceItem.GetKind("NewLife.Redis:XREADGROUP-GROUP"));
    }
}
