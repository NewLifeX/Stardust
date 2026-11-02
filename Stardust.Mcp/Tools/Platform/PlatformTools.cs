using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;

namespace Stardust.Mcp.Tools.Platform;

public class ProjectListTool : McpToolBase
{
    public override String Name => "stardust_project_list";
    public override String Description => "List the project bound to the current token. A token is strictly single-project.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "platform";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object" });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var project = ctx.ProjectId > 0 ? GalaxyProject.FindById(ctx.ProjectId) : null;
        var data = project == null ? null : new { id = project.Id, name = project.Name, enable = project.Enable, nodes = project.Nodes, apps = project.Apps };
        return Task.FromResult<Object>(McpResult.Ok(project == null ? "当前令牌未绑定项目" : $"当前项目：{project.Name}", new { project = data }));
    }
}

public class AuditListTool : McpToolBase
{
    public override String Name => "stardust_audit_list";
    public override String Description => "List recent MCP audit records for the current token. Optional tool name filter.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "platform";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            tool = new { type = "string", description = "按工具名过滤" },
            since = new { type = "string", description = "时间窗，如 -24h" },
            page = new { type = "integer", description = "页码" },
            page_size = new { type = "integer", description = "每页条数" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var key = GetString(args, "tool");
        var start = Since(args);
        var pageIdx = Page(args);
        var pageSize = PageSize(args);
        var exp = McpAudit._.TokenId == ctx.TokenId & McpAudit._.CreateTime >= start;
        if (!key.IsNullOrEmpty()) exp &= McpAudit._.ToolName.Contains(key);
        var all = McpAudit.FindAll(exp).OrderByDescending(e => e.Id).ToArray();
        var total = all.Length;
        var rows = all.Skip((pageIdx - 1) * pageSize).Take(pageSize)
            .Select(e => new { time = e.CreateTime, tool = e.ToolName, success = e.Success, error = e.ErrorMessage, duration = e.Duration, ip = e.CallerIp }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"审计 {rows.Length}/{total} 条", new { records = rows },
            null, null, McpResult.Paged(total, pageIdx, pageSize)));
    }
}
