using System.Text.Json;
using NewLife.Data;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using XCode;

namespace Stardust.Mcp.Tools.Now;

public class ChangesTool : McpToolBase
{
    const Int32 SourceCap = 100;

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
        var start = Since(args);
        var deploys = AppDeploy.FindAllByProjectId(ctx.ProjectId);
        var configs = AppConfig.FindAllByProjectId(ctx.ProjectId);

        var rows = new List<(DateTime Time, Object Item)>();
        rows.AddRange(TakeNewest(DeployRows(deploys, start)));
        rows.AddRange(TakeNewest(PipelineRows(deploys, start)));
        rows.AddRange(TakeNewest(ConfigRows(configs, start)));

        var page = rows.OrderByDescending(e => e.Time)
            .Skip((Page(args) - 1) * PageSize(args))
            .Take(PageSize(args))
            .Select(e => e.Item)
            .ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"项目变更 {page.Length} 条", new { records = page }));
    }

    /// <summary>部署历史走 Biz 的 Search(deployId, nodeId, action, start, end, key, page)。nodeId=-1 表示不按节点收窄。</summary>
    static IEnumerable<(DateTime Time, Object Item)> DeployRows(IList<AppDeploy> deploys, DateTime start)
    {
        foreach (var deploy in deploys)
        {
            if (deploy.Id <= 0) continue;
            foreach (var e in AppDeployHistory.Search(deploy.Id, -1, null, start, DateTime.MinValue, null, Top("Id")))
                yield return (e.CreateTime, (Object)new { time = e.CreateTime, kind = "D", id = e.Id, action = e.Action, success = e.Success, remark = e.Remark });
        }
    }

    /// <summary>与 MVC/WebApi 的 AppPipelineRunController.Search 相同：FindAll，按 PipelineId 与 CreateTime 过滤，不按状态收窄。</summary>
    static IEnumerable<(DateTime Time, Object Item)> PipelineRows(IList<AppDeploy> deploys, DateTime start)
    {
        foreach (var deploy in deploys)
        {
            if (deploy.Id <= 0) continue;
            foreach (var pipeline in AppPipeline.FindAllByDeployId(deploy.Id))
            {
                if (pipeline.Id <= 0) continue;
                var pipelineId = pipeline.Id;
                foreach (var e in AppPipelineRun.FindAll(
                    (pipelineId < 0 ? null : AppPipelineRun._.PipelineId == pipelineId) &
                    AppPipelineRun._.CreateTime.Between(start, DateTime.MinValue),
                    Top("CreateTime")))
                    yield return (e.CreateTime, (Object)new { time = e.CreateTime, kind = "P", id = e.Id, action = e.Status.ToString(), success = e.Status.ToString() == "Success", remark = e.CommitMessage });
            }
        }
    }

    /// <summary>配置历史走 Biz 的 Search(configId, action, success, start, end, key, page)。</summary>
    static IEnumerable<(DateTime Time, Object Item)> ConfigRows(IList<AppConfig> configs, DateTime start)
    {
        foreach (var config in configs)
        {
            if (config.Id <= 0) continue;
            foreach (var e in ConfigHistory.Search(config.Id, null, null, start, DateTime.MinValue, null, Top("CreateTime")))
                yield return (e.CreateTime, (Object)new { time = e.CreateTime, kind = "C", id = e.Id, action = e.Action, success = e.Success, remark = "" });
        }
    }

    static PageParameter Top(String sort) => new()
    {
        PageIndex = 1,
        PageSize = SourceCap,
        Sort = sort,
        Desc = true,
    };

    static IEnumerable<(DateTime Time, Object Item)> TakeNewest(IEnumerable<(DateTime Time, Object Item)> rows)
        => rows.OrderByDescending(e => e.Time).Take(SourceCap);
}
