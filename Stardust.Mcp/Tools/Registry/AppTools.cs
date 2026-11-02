using System.Text.Json;
using NewLife;
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
    public override String Description => "One-call health check for one application: online instances, latest error rate, latest deploy version, latest pipeline run, active alarms and recent error logs.";
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
        var logs = AppClientLog.FindAll(AppClientLog._.AppId == app.Id).OrderByDescending(e => e.Id).Take(5).Select(e => e.Message).ToArray();
        var data = new
        {
            app = app.Name,
            online = online.Count,
            error_rate = stat?.ErrorRate,
            deploy_version = deploy?.Version,
            pipeline_status = run?.Status.ToString(),
            recent_errors = logs,
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
    public override String Description => "Read application logs by app_id, optional level and keyword, paged. Filtered on the server by application.";
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
        var exp = AppClientLog._.AppId == app.Id;
        if (!level.IsNullOrEmpty()) exp &= AppClientLog._.Kind == level;
        if (!key.IsNullOrEmpty()) exp &= AppClientLog._.Message.Contains(key);
        var rows = AppClientLog.FindAll(exp, AppClientLog._.Id.Desc(), null, Page(args) - 1, PageSize(args))
            .Select(e => new { time = e.Time, level = e.Kind, message = e.Message, client = e.ClientId }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{app.Name} 日志 {rows.Length} 条", new { records = rows }));
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
