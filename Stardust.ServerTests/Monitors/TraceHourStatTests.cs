using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>跟踪小时统计单元测试</summary>
public class TraceHourStatTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new TraceHourStat
        {
            AppId = 991001,
            ItemId = 881001,
            Name = "test-hour-" + Guid.NewGuid().ToString("n")[..8],
            StatTime = new DateTime(2026, 7, 16, 10, 0, 0),
            Total = 3000,
            Errors = 30,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.ID > 0);

            var found = TraceHourStat.FindByID(entity.ID);
            Assert.NotNull(found);
            Assert.Equal(entity.AppId, found.AppId);
            Assert.Equal(entity.Total, found.Total);
            Assert.Equal(entity.Errors, found.Errors);
        }
        finally
        {
            entity.Delete();
        }
    }
}
