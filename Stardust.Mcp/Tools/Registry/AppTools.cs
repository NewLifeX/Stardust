using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Mcp.Tools.Registry;

public class AppListTool : McpToolBase
{
    public override String Name => "stardust_app_list";
    public override String Description => "List applications in the token project. Optional keyword and category. Returns enable state, version and online instance count. Does not return secrets.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            keyword = new { type = "string", description = "按名称或显示名模糊匹配" },
            category = new { type = "string", description = "按应用分类精确匹配" },
            page = new { type = "integer", description = "页码，默认1" },
            page_size = new { type = "integer", description = "每页条数，默认20，最大100" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var key = GetString(args, "keyword");
        var category = GetString(args, "category");
        var exp = App._.ProjectId == ctx.ProjectId;
        if (!key.IsNullOrEmpty()) exp &= App._.Name.Contains(key) | App._.DisplayName.Contains(key);
        if (!category.IsNullOrEmpty()) exp &= App._.Category == category;
        var total = (Int32)App.FindCount(exp);
        var apps = App.FindAll(exp, null, null, Page(args) - 1, PageSize(args));
        var rows = apps.Select(a => new
        {
            id = a.Id,
            name = a.Name,
            display_name = a.DisplayName,
            category = a.Category,
            enable = a.Enable,
            version = a.Version,
            online = AppOnline.FindAll(AppOnline._.AppId == a.Id).Count,
        }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"项目内应用 {rows.Length}/{total} 个", new { records = rows },
            null, null, McpResult.Paged(total, Page(args), PageSize(args))));
    }
}

public class AppHealthTool : McpToolBase
{
    public override String Name => "stardust_app_health";
    public override String Description => "One-call health check for one application: online instances, latest error rate, latest deploy version, latest pipeline run, active alarms and recent error logs (last 24h, shard-aware; empty records with log_note when log shards not created yet).";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
        },
        required = new[] { "app_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var online = AppOnline.FindAll(AppOnline._.AppId == app.Id);
        var stat = AppMinuteStat.FindAll(AppMinuteStat._.AppId == app.Id).OrderByDescending(e => e.StatTime).FirstOrDefault();
        var deploy = AppDeploy.FindAll(AppDeploy._.AppId == app.Id & AppDeploy._.ProjectId == ctx.ProjectId).FirstOrDefault();
        var pipeline = deploy == null ? null : AppPipeline.FindAll(AppPipeline._.DeployId == deploy.Id).FirstOrDefault();
        var run = pipeline == null ? null : AppPipelineRun.FindAll(AppPipelineRun._.PipelineId == pipeline.Id).OrderByDescending(e => e.Id).FirstOrDefault();
        // AppClientLog 是分表实体（按月分库+按天分表），必须带时间窗做分表感知查询；
        // 全新环境（尚无应用上报日志）分表不存在，兜底为空数据而非 -32000
        var logs = AppClientLog.SearchSafe(app.Id, DateTime.Now.AddHours(-24), DateTime.Now, null, null,
            new PageParameter { PageIndex = 1, PageSize = 5, Sort = AppClientLog._.Id, Desc = true }, out var logNote)
            .Select(e => e.Message).ToArray();
        var data = new
        {
            app = app.Name,
            online = online.Count,
            error_rate = stat?.ErrorRate,
            deploy_version = deploy?.Version,
            pipeline_status = run?.Status.ToString(),
            recent_errors = logs,
            log_note = logNote,
        };
        return Task.FromResult<Object>(McpResult.Ok($"{app.Name}：在线 {online.Count}，错误率 {stat?.ErrorRate ?? 0:P2}", data,
            ["stardust_deploy_status", "stardust_trace_search", "stardust_alarm_list"]));
    }
}

public class AppTopologyTool : McpToolBase
{
    public override String Name => "stardust_app_topology";
    public override String Description => "Show services provided and consumed by one application.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
        },
        required = new[] { "app_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var provides = AppService.FindAll(AppService._.AppId == app.Id).Select(e => new { e.ServiceName, e.Address, e.Healthy, e.Enable }).ToArray();
        var consumes = AppConsume.FindAll(AppConsume._.AppId == app.Id).Select(e => new { e.ServiceName, e.Address, e.Enable }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{app.Name}：提供 {provides.Length} 个服务，消费 {consumes.Length} 个依赖", new { provides, consumes }));
    }
}

public class AppLogsTool : McpToolBase
{
    public override String Name => "stardust_app_logs";
    public override String Description => "Read application client logs by app_id within a time window (dt_start/dt_end, default last 24h), optional level and keyword, paged, newest first. Shard-aware: AppClientLog is time-sharded; returns empty records with a note when shards are not created yet.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
            level = new { type = "string", description = "日志级别 Info/Warn/Error/Fatal" },
            keyword = new { type = "string", description = "按消息内容模糊匹配" },
            dt_start = new { type = "string", description = "开始时间 yyyy-MM-dd HH:mm:ss，默认24小时前" },
            dt_end = new { type = "string", description = "结束时间 yyyy-MM-dd HH:mm:ss，默认当前" },
            page = new { type = "integer", description = "页码，默认1" },
            page_size = new { type = "integer", description = "每页条数，默认20，最大100" },
        },
        required = new[] { "app_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var level = GetString(args, "level");
        var key = GetString(args, "keyword");

        var end = DateTime.Now;
        var start = end.AddHours(-24);
        var sRaw = GetString(args, "dt_start");
        var eRaw = GetString(args, "dt_end");
        if (!eRaw.IsNullOrEmpty() && !DateTime.TryParse(eRaw, out end))
            throw new McpException(-32602, "无效参数：dt_end 不是有效时间");
        if (!sRaw.IsNullOrEmpty() && !DateTime.TryParse(sRaw, out start))
            throw new McpException(-32602, "无效参数：dt_start 不是有效时间");
        if (start >= end) throw new McpException(-32602, "无效参数：dt_start 必须早于 dt_end");

        // AppClientLog 是分表实体（按月分库+按天分表），必须带 Id 时间窗做分表感知查询；
        // 全新环境（尚无应用上报日志）分表不存在，兜底为空数据而非 -32000
        var list = AppClientLog.SearchSafe(app.Id, start, end, key, level,
            new PageParameter { PageIndex = Page(args), PageSize = PageSize(args), Sort = AppClientLog._.Id, Desc = true }, out var note);
        var rows = list.Select(e => new { time = e.Time, level = e.Kind, message = e.Message, client = e.ClientId }).ToArray();
        var summary = $"{app.Name} 日志 {rows.Length} 条（{start:yyyy-MM-dd HH:mm:ss} ~ {end:yyyy-MM-dd HH:mm:ss}）";
        if (!note.IsNullOrEmpty()) summary += "；" + note;
        return Task.FromResult<Object>(McpResult.Ok(summary, new { records = rows, note, window = new { start, end } }));
    }
}

public class ServiceLookupTool : McpToolBase
{
    public override String Name => "stardust_service_lookup";
    public override String Description => "Find providers and consumers of a service name inside the token project.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            service_name = new { type = "string", description = "服务名，模糊匹配" },
        },
        required = new[] { "service_name" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var name = GetString(args, "service_name");
        if (name.IsNullOrEmpty()) throw new McpException(-32602, "无效参数：缺少 service_name");
        var appIds = App.FindAll(App._.ProjectId == ctx.ProjectId).Select(e => e.Id).ToHashSet();
        var providers = AppService.FindAll(AppService._.ServiceName.Contains(name)).Where(e => appIds.Contains(e.AppId))
            .Select(e => new { e.AppId, e.ServiceName, e.Address, e.Healthy }).ToArray();
        var consumers = AppConsume.FindAll(AppConsume._.ServiceName.Contains(name)).Where(e => appIds.Contains(e.AppId))
            .Select(e => new { e.AppId, e.ServiceName, e.Address }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"服务 {name}：提供者 {providers.Length}，消费者 {consumers.Length}", new { providers, consumers }));
    }
}
