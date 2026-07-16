using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>应用分钟统计单元测试</summary>
public class AppMinuteStatTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new AppMinuteStat
        {
            AppId = 990001,
            StatTime = new DateTime(2026, 7, 16, 10, 0, 0),
            Total = 500,
            Errors = 25,
            Cost = 120,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.ID > 0);

            var found = AppMinuteStat.FindByID(entity.ID);
            Assert.NotNull(found);
            Assert.Equal(990001, found.AppId);
            Assert.Equal(500, found.Total);
            Assert.Equal(25, found.Errors);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void FindByID_NotFound_ReturnsNull()
    {
        Assert.Null(AppMinuteStat.FindByID(0));
        Assert.Null(AppMinuteStat.FindByID(-1));
    }
}
