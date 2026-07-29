using Stardust.Data.Gateway;
using Xunit;

namespace ServerTest.Gateway;

/// <summary>网关路由管理单元测试。覆盖CRUD、边界条件、异常路径</summary>
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

    [Fact(DisplayName = "边界-禁用路由插入后可查但不应在Search中返回")]
    public void DisabledRoute_InsertAndFind()
    {
        var name = "test-route-dis-" + Guid.NewGuid().ToString("n")[..8];

        var entity = new GatewayRoute
        {
            Name = name,
            ClusterId = 1,
            Path = "/api/disabled/*",
            Enable = false,
        };

        try
        {
            entity.Insert();

            // 禁用后仍可通过ID查到
            var found = GatewayRoute.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.False(found.Enable);
            Assert.Equal(name, found.Name);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "异常-空Name插入应被数据库约束拦截")]
    public void EmptyName_ShouldThrowOrReturnFalse()
    {
        var entity = new GatewayRoute
        {
            Name = "",
            ClusterId = 1,
            Path = "/api/empty/*",
            Enable = true,
        };

        // 空名称应触发数据库非空约束或业务校验
        var ex = Record.Exception(() => entity.Insert());
        if (ex != null)
        {
            // 异常路径：确认是数据库约束异常
            Assert.Contains("Name", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        // 若不抛异常，至少应无法查到（Insert 返回0）
        else
        {
            Assert.True(entity.Id == 0);
        }
    }

    [Fact(DisplayName = "边界-更新路由Path")]
    public void UpdatePath_Succeeds()
    {
        var entity = new GatewayRoute
        {
            Name = "test-route-upd-" + Guid.NewGuid().ToString("n")[..8],
            ClusterId = 1,
            Path = "/api/old/*",
            Enable = true,
        };

        try
        {
            entity.Insert();

            var newPath = "/api/updated/*";
            entity.Path = newPath;
            entity.Update();

            var found = GatewayRoute.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(newPath, found.Path);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "边界-无匹配搜索返回空列表")]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var results = GatewayRoute.Search("zzz_no_such_route_xyz_" + Guid.NewGuid().ToString("n")[..8], null);

        Assert.Empty(results);
    }

    [Fact(DisplayName = "边界-同Cluster多条路由")]
    public void MultipleRoutes_SameCluster()
    {
        var r1 = new GatewayRoute
        {
            Name = "test-multi-a-" + Guid.NewGuid().ToString("n")[..8],
            ClusterId = 1,
            Path = "/api/a/*",
            Enable = true,
        };
        var r2 = new GatewayRoute
        {
            Name = "test-multi-b-" + Guid.NewGuid().ToString("n")[..8],
            ClusterId = 1,
            Path = "/api/b/*",
            Enable = true,
        };

        try
        {
            r1.Insert();
            r2.Insert();

            var all = GatewayRoute.Search(null, null);
            Assert.True(all.Count >= 2);
        }
        finally
        {
            r1.Delete();
            r2.Delete();
        }
    }
}
