using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>MCP审计日志单元测试。覆盖审计实体CRUD、必填字段校验、搜索过滤</summary>
public class McpAuditTests
{
    [Fact(DisplayName = "审计日志-插入并查询")]
    public void InsertAndSearch()
    {
        var entity = new McpAudit
        {
            TokenId = 1,
            ToolName = "test_tool",
            ActionName = "test_action",
            Success = true,
            Duration = 100,
            CallerIp = "127.0.0.1",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);
            Assert.True(entity.Id > 0);

            var found = McpAudit.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal("test_tool", found.ToolName);
            Assert.Equal("test_action", found.ActionName);
            Assert.True(found.Success);
            Assert.Equal(100, found.Duration);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "边界-失败调用审计")]
    public void FailedCall_AuditRecord()
    {
        var entity = new McpAudit
        {
            TokenId = 2,
            ToolName = "invoke_action",
            ActionName = "node_upgrade",
            Success = false,
            Duration = 5000,
            ErrorMessage = "Node not found",
            CallerIp = "10.0.0.1",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = McpAudit.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.False(found.Success);
            Assert.Equal("Node not found", found.ErrorMessage);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "边界-零耗时审计")]
    public void ZeroDuration_AuditRecord()
    {
        var entity = new McpAudit
        {
            TokenId = 1,
            ToolName = "list_actions",
            Success = true,
            Duration = 0,
            CallerIp = "::1",
        };

        try
        {
            var count = entity.Insert();
            Assert.True(count > 0);

            var found = McpAudit.FindById(entity.Id);
            Assert.NotNull(found);
            Assert.Equal(0, found.Duration);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "搜索-按TokenId过滤")]
    public void Search_ByTokenId()
    {
        var entity = new McpAudit
        {
            TokenId = 999,
            ToolName = "test_search",
            ActionName = "test_action",
            Success = true,
            Duration = 50,
            CallerIp = "127.0.0.1",
        };

        try
        {
            entity.Insert();

            var list = McpAudit.Search(999, null, DateTime.MinValue, DateTime.MinValue, null, null);
            Assert.NotEmpty(list);
            Assert.Contains(list, e => e.TokenId == 999);
        }
        finally
        {
            entity.Delete();
        }
    }

    [Fact(DisplayName = "边界-无匹配搜索返回空")]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var results = McpAudit.Search(-1, null, DateTime.MinValue, DateTime.MinValue, null, null);

        Assert.Empty(results);
    }
}
