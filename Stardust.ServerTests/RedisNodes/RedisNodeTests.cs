using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.RedisNodes;

/// <summary>Redis 节点管理单元测试</summary>
public class RedisNodeTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var server = $"127.0.0.1:{new Random().Next(10000, 60000)}";
        var entity = new RedisNode
        {
            Name = "test-redis-" + Guid.NewGuid().ToString("n")[..8],
            Server = server,
            Category = "Test",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = RedisNode.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal(entity.Server, found.Server);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void Search_ByCategory_FindsResults()
    {
        var name = "test-redis-s-" + Guid.NewGuid().ToString("n")[..8];
        var server = $"127.0.0.1:{new Random().Next(10000, 60000)}";

        var entity = new RedisNode
        {
            Name = name,
            Server = server,
            Category = "UnitTest",
            Enable = true,
        };

        try
        {
            entity.Insert();

            var list = RedisNode.Search(null, "UnitTest", null, DateTime.MinValue, DateTime.MinValue, null, null);
            Assert.NotEmpty(list);
            Assert.Contains(list, e => e.Name == name);
        }
        finally
        {
            entity.Delete();
        }
    }
}
