using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>节点在线记录填充测试</summary>
public class NodeOnlineTests
{
    [Fact(DisplayName = "可用内存缺失时用空闲内存兜底，避免列表长期显示为0")]
    public void Fill_NoAvailableMemory_FallbackToFreeMemory()
    {
        // 部分内核/客户端取不到 MemAvailable，上报值为 0；平台上会一直显示"可用内存 0"，内存告警也随之失真
        var online = new NodeOnline { Memory = 1024 };
        online.Fill(new NodeInfo { AvailableMemory = 0, FreeMemory = 800UL * 1024 * 1024 });

        Assert.Equal(800, online.AvailableMemory);
        Assert.Equal(800, online.FreeMemory);
        Assert.Equal(224, online.MemoryUsed);
    }

    [Fact(DisplayName = "可用内存正常时不覆盖，按上报值换算为MB")]
    public void Fill_AvailableMemory_KeepReportedValue()
    {
        var online = new NodeOnline { Memory = 1024 };
        online.Fill(new NodeInfo { AvailableMemory = 512UL * 1024 * 1024, FreeMemory = 800UL * 1024 * 1024 });

        Assert.Equal(512, online.AvailableMemory);
        Assert.Equal(800, online.FreeMemory);
        Assert.Equal(224, online.MemoryUsed);
    }
}
