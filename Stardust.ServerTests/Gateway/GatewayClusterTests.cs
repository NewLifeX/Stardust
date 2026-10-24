using Stardust.Data.Gateway;
using Xunit;

namespace ServerTest.Gateway;

/// <summary>网关集群管理单元测试</summary>
public class GatewayClusterTests
{
    /// <summary>确保网关节点表已建好</summary>
    /// <remarks>
    /// 集群删除会级联清理 GatewayNode 子表，而全新库中若尚无任何用例触达该实体，子表尚未创建，
    /// 删除语句会直接报 no such table。这里先触达一次实体，让 XCode 完成建表，避免用例顺序依赖。
    /// </remarks>
    static GatewayClusterTests() => _ = GatewayNode.Meta.Count;

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
