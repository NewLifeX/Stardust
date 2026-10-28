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
}