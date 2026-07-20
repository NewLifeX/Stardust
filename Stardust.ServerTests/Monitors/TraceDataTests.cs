using System;
using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest.Monitors;

/// <summary>追踪数据单元测试</summary>
public class TraceDataTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new TraceData
        {
            AppId = 998001,
            ItemId = 888001,
            Name = "test-trace-" + Guid.NewGuid().ToString("n")[..8],
            ClientId = "test-client",
            Cost = 100,
            Errors = 0,
            Total = 1,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = TraceData.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.AppId, found.AppId);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal(100, found.Cost);
        }
        finally
        {
            entity.Delete();
        }
    }
}
