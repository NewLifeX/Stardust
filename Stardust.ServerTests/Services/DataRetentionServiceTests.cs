using System;
using System.Reflection;
using Stardust.Data.Monitors;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>数据保留服务测试。覆盖保留期开关与多级保留期编排（SVR-3）</summary>
public class DataRetentionServiceTests
{
    /// <summary>直接调用私有的清理方法，避免等待定时器</summary>
    private static void RunWork(DataRetentionService service)
    {
        var mi = typeof(DataRetentionService).GetMethod("DoWork", BindingFlags.NonPublic | BindingFlags.Instance);
        mi.Invoke(service, [null]);
    }

    private static AppMinuteStat CreateMinuteStat(DateTime statTime) => new()
    {
        AppId = 992801,
        StatTime = statTime,
        Total = 1,
        Errors = 0,
        Cost = 1,
    };

    [Fact(DisplayName = "保留期为零时跳过清理，过期数据保留")]
    public void DoWork_Disabled_KeepsData()
    {
        var service = new DataRetentionService(new StarServerSetting { DataRetention = 0 }, null);

        var entity = CreateMinuteStat(DateTime.Now.AddDays(-30));
        entity.Insert();
        try
        {
            RunWork(service);

            Assert.NotNull(AppMinuteStat.FindByID(entity.ID));
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "启用保留期后按分钟统计保留期清理过期数据，保留期内数据不动")]
    public void DoWork_Enabled_RemovesExpiredKeepsFresh()
    {
        // 三级保留期分别为 1/2/3 天，分钟统计使用第一级
        var service = new DataRetentionService(new StarServerSetting { DataRetention = 1, DataRetention2 = 2, DataRetention3 = 3 }, null);

        var old = CreateMinuteStat(DateTime.Now.AddDays(-10));
        old.Insert();
        var fresh = CreateMinuteStat(DateTime.Now);
        fresh.Insert();
        try
        {
            RunWork(service);

            // 过期数据被清理，保留期内数据不受影响（单表失败不影响其它表）
            Assert.Null(AppMinuteStat.FindByID(old.ID));
            Assert.NotNull(AppMinuteStat.FindByID(fresh.ID));
        }
        finally
        {
            AppMinuteStat.FindByID(old.ID)?.Delete();
            AppMinuteStat.FindByID(fresh.ID)?.Delete();
        }
    }
}