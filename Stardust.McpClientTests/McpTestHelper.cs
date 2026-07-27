using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using XCode;

namespace Stardust.McpClientTests;

/// <summary>MCP测试数据辅助类。创建Token、资源授权、测试数据</summary>
public static class McpTestHelper
{
    /// <summary>创建测试Token（启用、永不过期）</summary>
    public static (McpToken token, String tokenString) CreateTestToken(String name = "test-token")
    {
        // 删除同名旧Token
        var existing = McpToken.Search(null, null, DateTime.MinValue, DateTime.MinValue, name, new PageParameter { PageIndex = 1, PageSize = 100 });
        foreach (var e in existing) e.Delete();

        var token = new McpToken
        {
            Name = name,
            Token = McpToken.GenerateToken(),
            Enable = true,
            ExpireTime = DateTime.MinValue,
        };
        token.Insert();
        return (token, token.Token);
    }

    /// <summary>为Token添加全部项目授权</summary>
    public static void AuthorizeAllProjects(Int32 tokenId)
    {
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = "Project",
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token添加全部节点授权</summary>
    public static void AuthorizeAllNodes(Int32 tokenId)
    {
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = "Node",
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token添加全部应用授权</summary>
    public static void AuthorizeAllApps(Int32 tokenId)
    {
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = "App",
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token授权特定项目</summary>
    public static void AuthorizeProject(Int32 tokenId, Int32 projectId)
    {
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = "Project",
            ResourceId = projectId,
            IsAll = false,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>清理Token及其授权</summary>
    public static void CleanupToken(Int32 tokenId)
    {
        var resources = McpTokenResource.FindAllByToken(tokenId);
        foreach (var r in resources) r.Delete();
        var token = McpToken.FindById(tokenId);
        token?.Delete();
    }
}
