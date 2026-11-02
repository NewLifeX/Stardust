using System.Text.Json;
using NewLife;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;

namespace Stardust.Mcp.Tools.Now;

public class ChangesTool : McpToolBase
{
    public override String Name => "stardust_now_changes";
    public override String Description => "Project-scoped change timeline across applications: deploy history, pipeline runs and config publishes, newest first. Paged.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "now";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            since = new { type = "string", description = "时间窗，如 -24h 或 ISO 时间，默认最近24小时" },
            page = new { type = "integer", description = "页码，默认1" },
            page_size = new { type = "integer", description = "每页条数，默认20，最大100" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var deploys = AppDeploy.FindAll(AppDeploy._.ProjectId == ctx.ProjectId);
        var deployIds = deploys.Select(e => e.Id).ToHashSet();
        var configs = AppConfig.FindAll(AppConfig._.ProjectId == ctx.ProjectId);
        var configIds = configs.Select(e => e.Id).ToHashSet();
        var pipelines = AppPipeline.FindAll().Where(e => deployIds.Contains(e.DeployId)).ToList();
        var pipelineIds = pipelines.Select(e => e.Id).ToHashSet();

        var rows = new List<(DateTime Time, Object Item)>();
        rows.AddRange(AppDeployHistory.FindAll(AppDeployHistory._.CreateTime >= Since(args)).Where(e => deployIds.Contains(e.DeployId)).Take(100)
            .Select(e => (e.CreateTime, (Object)new { time = e.CreateTime, kind = "D", id = e.Id, action = e.Action, success = e.Success, remark = e.Remark })));
        rows.AddRange(AppPipelineRun.FindAll(AppPipelineRun._.CreateTime >= Since(args)).Where(e => pipelineIds.Contains(e.PipelineId)).Take(100)
            .Select(e => (e.CreateTime, (Object)new { time = e.CreateTime, kind = "P", id = e.Id, action = e.Status.ToString(), success = e.Status.ToString() == "Success", remark = e.CommitMessage })));
        rows.AddRange(ConfigHistory.FindAll(ConfigHistory._.CreateTime >= Since(args)).Where(e => configIds.Contains(e.ConfigId)).Take(100)
            .Select(e => (e.CreateTime, (Object)new { time = e.CreateTime, kind = "C", id = e.Id, action = e.Action, success = e.Success, remark = "" })));

        var page = rows.OrderByDescending(e => e.Time).Skip((Page(args) - 1) * PageSize(args)).Take(PageSize(args)).Select(e => e.Item).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"项目变更 {page.Length} 条", new { records = page }));
    }
}
