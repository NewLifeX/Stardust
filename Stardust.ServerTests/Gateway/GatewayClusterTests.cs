using Stardust.Data.Gateway;
using Xunit;

namespace ServerTest.Gateway;

/// <summary>网关集群管理单元测试</summary>
public class GatewayClusterTests
{
    [Fact]
    public void InsertAndFindById()
    {
        var entity = new GatewayCluster
        {
            Name = "test-cluster-" + Guid.NewGuid().ToString("n")[..8],
            LoadBalance = "RoundRobin",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = GatewayCluster.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal("RoundRobin", found.LoadBalance);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void Search_ByKey_FindsResults()
    {
        var name = "test-cluster-s-" + Guid.NewGuid().ToString("n")[..8];

        var entity = new GatewayCluster
        {
            Name = name,
            LoadBalance = "RoundRobin",
            Enable = true,
        };

        try
        {
            entity.Insert();

            var list = GatewayCluster.Search(name[..10], null);
            Assert.NotEmpty(list);
            Assert.Contains(list, e => e.Name == name);
        }
        finally
        {
            entity.Delete();
        }
    }
}
