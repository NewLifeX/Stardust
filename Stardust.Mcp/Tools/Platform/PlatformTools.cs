using System.Text.Json;
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
        // 令牌只绑定一个项目。控制台没有「当前令牌项目」页，这里按编号取那一条实体。
        var project = ctx.ProjectId > 0 ? ProjectScopeRules.FilterVisible(GalaxyProject.FindById(ctx.ProjectId)) : null;
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
        var tool = GetString(args, "tool");
        var start = Since(args);
        var page = new PageParameter
        {
            PageIndex = Page(args),
            PageSize = PageSize(args),
            Sort = McpAudit._.Id,
            Desc = true,
            RetrieveTotalCount = true,
        };
        // 与 Web/WebApi McpAuditController 同一份查询（MCP审计日志.cs 的 Search）。end 留空，与未填 dtEnd 相同。
        var list = McpAudit.Search(ctx.TokenId, tool, null, null, start, DateTime.MinValue, null, page);
        var total = (Int32)page.TotalCount;
        var rows = list.Select(e => new { time = e.CreateTime, tool = e.ToolName, success = e.Success, error = e.ErrorMessage, duration = e.Duration, ip = e.CallerIp }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"审计 {rows.Length}/{total} 条", new { records = rows },
            null, null, McpResult.Paged(total, page.PageIndex, page.PageSize)));
    }
}
