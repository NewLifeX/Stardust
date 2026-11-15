using System;
using NewLife.Security;
using Stardust.Data;
using Xunit;

namespace Stardust.Tests;

/// <summary>应用性能采样节流测试</summary>
public class AppMeterSamplingTests
{
    [Fact(DisplayName = "应用性能按采样周期落库，周期内重复心跳被丢弃")]
    public void TryAcquireSample_ThrottlesWithinPeriod()
    {
        var appId = Rand.Next(1_000_000, 2_000_000);
        var client = "client-" + Guid.NewGuid().ToString("n")[..8];

        Assert.True(AppMeter.TryAcquireSample(appId, client, 60, 1_000));
        Assert.False(AppMeter.TryAcquireSample(appId, client, 60, 2_000));
        Assert.False(AppMeter.TryAcquireSample(appId, client, 60, 60_999));
        Assert.True(AppMeter.TryAcquireSample(appId, client, 60, 61_000));
    }

    [Fact(DisplayName = "不同实例或不同应用的采样互不影响")]
    public void TryAcquireSample_SeparateKeys()
    {
        var appId = Rand.Next(1_000_000, 2_000_000);
        var other = Rand.Next(2_000_000, 3_000_000);

        Assert.True(AppMeter.TryAcquireSample(appId, "c1", 60, 1_000));
        Assert.True(AppMeter.TryAcquireSample(appId, "c2", 60, 1_000));
        Assert.True(AppMeter.TryAcquireSample(other, "c1", 60, 1_000));
    }
}
