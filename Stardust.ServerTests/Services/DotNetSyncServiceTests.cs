using System;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>dotNet 元数据同步的周期判断测试。启动时的首次同步必须遵守配置周期，避免每次重启都全量拉取</summary>
public class DotNetSyncServiceTests
{
    [Fact(DisplayName = "从未同步过时需要同步")]
    public void NeedSync_NeverSynced()
    {
        Assert.True(DotNetSyncService.NeedSync(DateTime.MinValue, DateTime.Now, 43200));
    }

    [Fact(DisplayName = "上次同步在周期内时跳过")]
    public void NeedSync_WithinPeriod_Skips()
    {
        var now = new DateTime(2026, 10, 29, 8, 0, 0);

        Assert.False(DotNetSyncService.NeedSync(now.AddHours(-1), now, 43200));
        Assert.False(DotNetSyncService.NeedSync(now.AddSeconds(-11), now, 43200));
    }

    [Fact(DisplayName = "超过周期后重新同步")]
    public void NeedSync_Expired_Syncs()
    {
        var now = new DateTime(2026, 10, 29, 8, 0, 0);

        Assert.True(DotNetSyncService.NeedSync(now.AddHours(-13), now, 43200));
        Assert.True(DotNetSyncService.NeedSync(now.AddSeconds(-43200), now, 43200));
    }

    [Fact(DisplayName = "配置周期为 0 或负数时不同步")]
    public void NeedSync_Disabled()
    {
        var now = DateTime.Now;

        Assert.False(DotNetSyncService.NeedSync(DateTime.MinValue, now, 0));
        Assert.False(DotNetSyncService.NeedSync(now.AddDays(-30), now, -1));
    }

    [Fact(DisplayName = "识别 SQLite 锁定类异常并沿内层异常链查找")]
    public void IsLocked_DetectsNestedLockError()
    {
        Assert.True(DotNetSyncService.IsLocked(new InvalidOperationException("database is locked")));
        Assert.True(DotNetSyncService.IsLocked(new Exception("outer", new InvalidOperationException("database is busy"))));
        Assert.False(DotNetSyncService.IsLocked(new InvalidOperationException("timeout")));
        Assert.False(DotNetSyncService.IsLocked(null));
    }
}
