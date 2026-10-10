using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Services;

/// <summary>数据保留清理的删除原语测试。覆盖 <c>DeleteBefore</c> 的阈值语义（DataRetentionService 的多级保留期依赖它）</summary>
public class DataRetentionDeleteTests
{
    [Fact(DisplayName = "小时统计：按 StatTime 阈值删除过期数据")]
    public void TraceHourStat_DeleteBefore_RemovesExpired()
    {
        var entity = new TraceHourStat
        {
            AppId = 991101,
            ItemId = 881101,
            Name = "retention-hour-" + Guid.NewGuid().ToString("n")[..8],
            StatTime = DateTime.Now.AddDays(-30),
            Total = 1,
        };
        entity.Insert();
        Assert.True(entity.ID > 0);

        try
        {
            var deleted = TraceHourStat.DeleteBefore(DateTime.Now.AddDays(-10));

            Assert.True(deleted >= 1);
            Assert.Null(TraceHourStat.FindByID(entity.ID));
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "应用分钟统计：按 StatTime 阈值删除过期数据")]
    public void AppMinuteStat_DeleteBefore_RemovesExpired()
    {
        var entity = new AppMinuteStat
        {
            AppId = 990101,
            StatTime = DateTime.Now.AddDays(-30),
            Total = 1,
            Errors = 0,
            Cost = 1,
        };
        entity.Insert();
        Assert.True(entity.ID > 0);

        try
        {
            var deleted = AppMinuteStat.DeleteBefore(DateTime.Now.AddDays(-10));

            Assert.True(deleted >= 1);
            Assert.Null(AppMinuteStat.FindByID(entity.ID));
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "追踪每日统计：按 StatDate 阈值删除过期数据")]
    public void TraceDayStat_DeleteBefore_RemovesExpired()
    {
        var entity = new TraceDayStat
        {
            AppId = 999101,
            ItemId = 888101,
            Name = "retention-day-" + Guid.NewGuid().ToString("n")[..8],
            StatDate = DateTime.Today.AddDays(-30),
            Total = 1,
            Errors = 0,
        };
        entity.Insert();
        Assert.True(entity.ID > 0);

        try
        {
            var deleted = TraceDayStat.DeleteBefore(DateTime.Today.AddDays(-10));

            Assert.True(deleted >= 1);
            Assert.Null(TraceDayStat.FindByID(entity.ID));
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "保留期内的新数据不被删除")]
    public void DeleteBefore_KeepsFreshData()
    {
        var entity = new TraceHourStat
        {
            AppId = 991102,
            ItemId = 881102,
            Name = "retention-fresh-" + Guid.NewGuid().ToString("n")[..8],
            StatTime = DateTime.Now,
            Total = 1,
        };
        entity.Insert();
        Assert.True(entity.ID > 0);

        try
        {
            TraceHourStat.DeleteBefore(DateTime.Now.AddDays(-10));

            Assert.NotNull(TraceHourStat.FindByID(entity.ID));
        }
        finally
        {
            entity.Delete();
        }
    }
}
