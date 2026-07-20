using System;
using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.RedisNodes;

/// <summary>Redis 数据管理单元测试</summary>
public class RedisDataTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new RedisData
        {
            RedisId = 997001,
            Name = "test-key-" + Guid.NewGuid().ToString("n")[..8],
            TopCommand = "GET",
            Uptime = 3600,
            ConnectedClients = 10,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = RedisData.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal("GET", found.TopCommand);
            Assert.Equal(3600, found.Uptime);
        }
        finally
        {
            entity.Delete();
        }
    }
}
