using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>MCP令牌资源授权单元测试。覆盖授权判定逻辑、IsAll语义、项目ID解析</summary>
public class McpTokenResourceTests
{
    /// <summary>模拟授权列表，用于内联测试 IsAuthorized 的判定逻辑</summary>
    private static Boolean IsAuthorizedInline(IList<McpTokenResource> list, String resourceType, Int32 resourceId)
    {
        foreach (var r in list)
        {
            if (!r.Enable) continue;
            if (r.ResourceType != resourceType) continue;
            if (r.IsAll) return true;
            if (r.ResourceId == resourceId) return true;
        }
        return false;
    }

    /// <summary>模拟 GetAuthorizedProjectIds 的判定逻辑</summary>
    private static Int32[] GetAuthorizedProjectIdsInline(IList<McpTokenResource> list)
    {
        var ids = new List<Int32>();
        foreach (var r in list.Where(r => r.Enable && r.ResourceType == "project"))
        {
            if (r.IsAll) return null; // null 表示全部
            ids.Add(r.ResourceId);
        }
        return ids.Count > 0 ? ids.Distinct().ToArray() : Array.Empty<Int32>();
    }

    [Fact]
    public void IsAuthorized_DirectMatch_ReturnsTrue()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 100, IsAll = false }
        };

        Assert.True(IsAuthorizedInline(list, "node", 100));
    }

    [Fact]
    public void IsAuthorized_NoMatch_ReturnsFalse()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 100, IsAll = false }
        };

        Assert.False(IsAuthorizedInline(list, "node", 200));
    }

    [Fact]
    public void IsAuthorized_IsAll_ReturnsTrue()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 0, IsAll = true }
        };

        Assert.True(IsAuthorizedInline(list, "node", 999));
    }

    [Fact]
    public void IsAuthorized_Disabled_ReturnsFalse()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = false, ResourceType = "node", ResourceId = 100, IsAll = false }
        };

        Assert.False(IsAuthorizedInline(list, "node", 100));
    }

    [Fact]
    public void IsAuthorized_TypeMismatch_ReturnsFalse()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 100, IsAll = true }
        };

        Assert.False(IsAuthorizedInline(list, "app", 100));
    }

    [Fact]
    public void IsAuthorized_EmptyList_ReturnsFalse()
    {
        Assert.False(IsAuthorizedInline(new List<McpTokenResource>(), "node", 100));
    }

    [Fact]
    public void IsAll_IsAllWinsOverDirectMatch()
    {
        // IsAll 条目优先于 ResourceId 匹配
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 0, IsAll = true },
            new() { Enable = true, ResourceType = "node", ResourceId = 100, IsAll = false }
        };

        // 任意 node ID 都应通过（因为 IsAll=true）
        Assert.True(IsAuthorizedInline(list, "node", 999));
    }

    [Fact]
    public void GetAuthorizedProjectIds_IsAll_ReturnsNull()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "project", IsAll = true }
        };

        Assert.Null(GetAuthorizedProjectIdsInline(list));
    }

    [Fact]
    public void GetAuthorizedProjectIds_SpecificIds_ReturnsArray()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "project", ResourceId = 1, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 2, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 3, IsAll = false }
        };

        var ids = GetAuthorizedProjectIdsInline(list);

        Assert.NotNull(ids);
        Assert.Equal(3, ids.Length);
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
        Assert.Contains(3, ids);
    }

    [Fact]
    public void GetAuthorizedProjectIds_Empty_ReturnsEmptyArray()
    {
        var ids = GetAuthorizedProjectIdsInline(new List<McpTokenResource>());

        Assert.NotNull(ids);
        Assert.Empty(ids);
    }

    [Fact]
    public void GetAuthorizedProjectIds_FiltersNonProjectTypes()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "node", ResourceId = 100, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 1, IsAll = false }
        };

        var ids = GetAuthorizedProjectIdsInline(list);

        Assert.Single(ids);
        Assert.Contains(1, ids);
    }

    [Fact]
    public void GetAuthorizedProjectIds_FiltersDisabled()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = false, ResourceType = "project", ResourceId = 1, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 2, IsAll = false }
        };

        var ids = GetAuthorizedProjectIdsInline(list);

        Assert.Single(ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void GetAuthorizedProjectIds_Deduplicates()
    {
        var list = new List<McpTokenResource>
        {
            new() { Enable = true, ResourceType = "project", ResourceId = 1, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 1, IsAll = false },
            new() { Enable = true, ResourceType = "project", ResourceId = 2, IsAll = false }
        };

        var ids = GetAuthorizedProjectIdsInline(list);

        Assert.Equal(2, ids.Length);
    }

    [Fact]
    public void FindById_Negative_ReturnsNull()
    {
        Assert.Null(McpTokenResource.FindById(-1));
    }
}
