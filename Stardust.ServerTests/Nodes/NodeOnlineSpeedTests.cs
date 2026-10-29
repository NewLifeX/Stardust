using System;
using System.ComponentModel;
using System.Reflection;
using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>节点在线速度修正测试</summary>
public class NodeOnlineSpeedTests
{
    private static Int64 FixSpeed(Double speed)
    {
        var method = typeof(NodeOnline).GetMethod("FixSpeed", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (Int64)method.Invoke(null, [speed]);
    }

    [Fact]
    [DisplayName("计数器回绕产生的负速度归零，正速度保持不变")]
    public void FixSpeed_ClampsNegative()
    {
        Assert.Equal(0, FixSpeed(-641_363));
        Assert.Equal(0, FixSpeed(0));
        Assert.Equal(1024, FixSpeed(1024.9));
    }
}
