using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>跟踪每日统计单元测试</summary>
public class TraceDayStatTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new TraceDayStat
        {
            AppId = 999100,
            ItemId = 888100,
            Name = "test-day-" + Guid.NewGuid().ToString("n")[..8],
            StatDate = DateTime.Today,
            Total = 1000,
            Errors = 50,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.ID > 0);

            var found = TraceDayStat.FindByID(entity.ID);
            Assert.NotNull(found);
            Assert.Equal(entity.AppId, found.AppId);
            Assert.Equal(entity.Total, found.Total);
        }
        finally
        {
            entity.Delete();
        }
    }
}
