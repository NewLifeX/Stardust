using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>跟踪分钟统计单元测试。覆盖实体基础操作和搜索方法</summary>
public class TraceMinuteStatTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new TraceMinuteStat
        {
            AppId = 999001,
            ItemId = 888001,
            Name = "test-api-" + Guid.NewGuid().ToString("n")[..8],
            StatTime = new DateTime(2026, 7, 15, 10, 30, 0),
            Total = 100,
            Errors = 5,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.ID > 0);

            var found = TraceMinuteStat.FindByID(entity.ID);
            Assert.NotNull(found);
            Assert.Equal(entity.AppId, found.AppId);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal(100, found.Total);
            Assert.Equal(5, found.Errors);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void FindByID_NotFound_ReturnsNull()
    {
        var result = TraceMinuteStat.FindByID(0);
        Assert.Null(result);

        result = TraceMinuteStat.FindByID(-1);
        Assert.Null(result);
    }
}
