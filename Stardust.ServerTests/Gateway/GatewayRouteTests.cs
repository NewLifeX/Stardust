using Stardust.Data.Gateway;
using Xunit;

namespace ServerTest.Gateway;

/// <summary>网关路由管理单元测试</summary>
public class GatewayRouteTests
{
    [Fact]
    public void InsertAndSearch()
    {
        var entity = new GatewayRoute
        {
            Name = "test-route-" + Guid.NewGuid().ToString("n")[..8],
            ClusterId = 1,
            Path = "/api/test/*",
            Enable = true,
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = GatewayRoute.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(entity.Name, found.Name);
            Assert.Equal(entity.Path, found.Path);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact]
    public void Search_ByKey_FindsResults()
    {
        var name = "test-route-s-" + Guid.NewGuid().ToString("n")[..8];

        var entity = new GatewayRoute
        {
            Name = name,
            ClusterId = 1,
            Path = "/api/search/*",
            Enable = true,
        };

        try
        {
            entity.Insert();

            var list = GatewayRoute.Search(name[..10], null);
            Assert.NotEmpty(list);
            Assert.Contains(list, e => e.Name == name);
        }
        finally
        {
            entity.Delete();
        }
    }
}
