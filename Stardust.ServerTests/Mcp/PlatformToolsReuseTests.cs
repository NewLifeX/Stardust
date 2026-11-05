using System.Text.Json;
using System.Text.Json.Nodes;
using Stardust.Data.Platform;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Platform;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// 平台读工具复用现有查询。
/// 审计列表与 Web/WebApi McpAuditController 同走 McpAudit.Search，分页交给 PageParameter。
/// 项目列表只按令牌上的项目编号取一条，不是项目目录。
/// </summary>
public class PlatformToolsReuseTests
{
    private const Int32 TokenId = 91234021;

    private static JsonElement Args(String json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static void ClearAudits(Int32 tokenId)
    {
        foreach (var row in McpAudit.FindAll(McpAudit._.TokenId == tokenId)) row.Delete();
    }

    [Fact(DisplayName = "审计列表走 Search 分页，工具名精确匹配且不串令牌")]
    public async Task AuditList_UsesControllerSearch()
    {
        ClearAudits(TokenId);
        ClearAudits(TokenId + 1);

        var now = DateTime.Now;
        var rows = new[]
        {
            new McpAudit { TokenId = TokenId, ToolName = "alpha_tool", Success = true, Duration = 1, CallerIp = "10.0.0.1", CreateTime = now.AddMinutes(-3) },
            new McpAudit { TokenId = TokenId, ToolName = "alpha_tool_extra", Success = false, ErrorMessage = "x", Duration = 2, CallerIp = "10.0.0.2", CreateTime = now.AddMinutes(-2) },
            new McpAudit { TokenId = TokenId, ToolName = "beta_tool", Success = true, Duration = 3, CallerIp = "10.0.0.3", CreateTime = now.AddMinutes(-1) },
        };
        var other = new McpAudit { TokenId = TokenId + 1, ToolName = "alpha_tool", Success = true, Duration = 9, CallerIp = "10.0.0.9", CreateTime = now };

        try
        {
            foreach (var row in rows) row.Insert();
            other.Insert();

            var tool = new AuditListTool();
            var ctx = new McpContext { TokenId = TokenId };

            var first = JsonSerializer.SerializeToNode(await tool.InvokeAsync(Args("""{"since":"2000-01-01","page":1,"page_size":1}"""), ctx))!;
            Assert.Equal("审计 1/3 条", first["summary"]!.GetValue<String>());
            Assert.Equal(3, first["pagination"]!["total"]!.GetValue<Int32>());
            Assert.Equal(1, first["pagination"]!["page"]!.GetValue<Int32>());
            Assert.Equal(1, first["pagination"]!["page_size"]!.GetValue<Int32>());
            Assert.Equal("beta_tool", first["data"]!["records"]![0]!["tool"]!.GetValue<String>());

            var second = JsonSerializer.SerializeToNode(await tool.InvokeAsync(Args("""{"since":"2000-01-01","page":2,"page_size":1}"""), ctx))!;
            Assert.Equal("alpha_tool_extra", second["data"]!["records"]![0]!["tool"]!.GetValue<String>());

            var filtered = JsonSerializer.SerializeToNode(await tool.InvokeAsync(Args("""{"tool":"alpha_tool","since":"2000-01-01","page":1,"page_size":20}"""), ctx))!;
            Assert.Equal(1, filtered["pagination"]!["total"]!.GetValue<Int32>());
            Assert.Equal("alpha_tool", filtered["data"]!["records"]![0]!["tool"]!.GetValue<String>());
            Assert.Equal("10.0.0.1", filtered["data"]!["records"]![0]!["ip"]!.GetValue<String>());
        }
        finally
        {
            foreach (var row in rows) row.Delete();
            other.Delete();
            ClearAudits(TokenId);
            ClearAudits(TokenId + 1);
        }
    }

    [Fact(DisplayName = "项目列表只返回令牌绑定的那一个项目")]
    public async Task ProjectList_ReadsBoundProjectOnly()
    {
        var project = new GalaxyProject
        {
            Name = "mcp-reuse-" + Guid.NewGuid().ToString("n")[..8],
            Enable = true,
        };

        try
        {
            project.Insert();
            var tool = new ProjectListTool();

            var bound = JsonSerializer.SerializeToNode(await tool.InvokeAsync(Args("{}"), new McpContext { TokenId = 1, ProjectId = project.Id }))!;
            Assert.Equal($"当前项目：{project.Name}", bound["summary"]!.GetValue<String>());
            Assert.Equal(project.Id, bound["data"]!["project"]!["id"]!.GetValue<Int32>());
            Assert.Equal(project.Name, bound["data"]!["project"]!["name"]!.GetValue<String>());
            Assert.True(bound["data"]!["project"]!["enable"]!.GetValue<Boolean>());

            var unbound = JsonSerializer.SerializeToNode(await tool.InvokeAsync(Args("{}"), new McpContext { TokenId = 1, ProjectId = 0 }))!;
            Assert.Equal("当前令牌未绑定项目", unbound["summary"]!.GetValue<String>());
            var unboundData = unbound["data"]!.AsObject();
            Assert.True(unboundData.ContainsKey("project"));
            Assert.Null(unboundData["project"]);
        }
        finally
        {
            project.Delete();
        }
    }
}
