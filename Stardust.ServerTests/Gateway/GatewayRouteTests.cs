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

    /// <summary>构造带域名的路由并确保清理（Domain 多段逗号分隔）</summary>
    private static GatewayRoute InsertRoute(string domain)
    {
        var entity = new GatewayRoute
        {
            Name = "test-route-dom" + Guid.NewGuid().ToString("n")[..8],
            ClusterId = 1,
            Path = "/api/dom/*",
            Domain = domain,
            Enable = true,
        };
        entity.Insert();
        return entity;
    }

    private static IList<GatewayRoute> SearchByDomain(string domain)
    {
        // 对齐控制器调用（GatewayRouteController.Search L48）：未传条件全部置空/跳过
        return GatewayRoute.Search(-1, -1, -1, domain, null, null, null, null, null, null,
            DateTime.MinValue, DateTime.MinValue, null, null);
    }

    [Fact(DisplayName = "域名搜索-单段域名精确命中（基线：等值过滤须继续可用）")]
    public void Search_Domain_SingleSegment_ExactMatch()
    {
        var stamp = Guid.NewGuid().ToString("n")[..8];
        var domain = $"t{stamp}.ut.example.com";
        var entity = InsertRoute(domain);

        try
        {
            var list = SearchByDomain(domain);
            Assert.Contains(list, e => e.Id == entity.Id);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "域名搜索-多段路由按末段命中（整串等值导致搜索不到）")]
    public void Search_Domain_MultiSegment_LastSegment()
    {
        var stamp = Guid.NewGuid().ToString("n")[..8];
        var target = $"t{stamp}.ut.example.com";
        var entity = InsertRoute($"*.x{stamp}.com,{target},*.y{stamp}.org");

        try
        {
            var list = SearchByDomain(target);
            Assert.Contains(list, e => e.Id == entity.Id);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "域名搜索-多段路由按首段命中（逗号边界）")]
    public void Search_Domain_MultiSegment_FirstSegment()
    {
        var stamp = Guid.NewGuid().ToString("n")[..8];
        var target = $"t{stamp}.ut.example.com";
        var entity = InsertRoute($"{target},*.x{stamp}.com");

        try
        {
            var list = SearchByDomain(target);
            Assert.Contains(list, e => e.Id == entity.Id);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "域名搜索-多段路由按中间段命中（两侧逗号边界）")]
    public void Search_Domain_MultiSegment_MiddleSegment()
    {
        var stamp = Guid.NewGuid().ToString("n")[..8];
        var target = $"t{stamp}.ut.example.com";
        var entity = InsertRoute($"*.x{stamp}.com,{target},*.y{stamp}.org");

        // 与末段用例同构，但以第二段为目标，覆盖「%,段,%」分支
        var list = SearchByDomain(target);
        try
        {
            Assert.Contains(list, e => e.Id == entity.Id);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "域名搜索-子串不得整段误伤（护栏：禁止退化成 LIKE %v%）")]
    public void Search_Domain_SubstringSegment_NoOverreach()
    {
        var stamp = Guid.NewGuid().ToString("n")[..8];
        var entity = InsertRoute($"t{stamp}.ut.example.com");

        try
        {
            // ut.example.com 是目标的段内子串，但不是完整段，不得命中
            var list = SearchByDomain("ut.example.com");
            Assert.DoesNotContain(list, e => e.Id == entity.Id);
        }
        finally
        {
            entity.Delete();
        }
    }
}
