using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.MySqlNodes;

/// <summary>MySQL 节点管理单元测试</summary>
public class MySqlNodeTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var server = $"192.168.1.{new Random().Next(100, 200)}:3306";
        var entity = new MySqlNode
        {
            Name = "test-mysql-" + Guid.NewGuid().ToString("n")[..8],
            Server = server,
            Category = "Test",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = MySqlNode.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal(entity.Server, found.Server);
        }
        finally
        {
            entity.Delete();
        }
    }
}
