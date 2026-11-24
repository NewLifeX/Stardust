using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Data.Platform;

namespace Stardust.Mcp.Tools.Alarm;

public class AlarmListTool : McpToolBase
{
    public override String Name => "stardust_alarm_list";
    public override String Description => "List alarm records in the token project time window. Optional status and level. Paged.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "monitor";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            since = new { type = "string", description = "时间窗，如 -24h 或 ISO 时间，默认最近24小时" },
            page_size = new { type = "integer", description = "每页条数，默认20，最大100" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var since = Since(args);
        var page = new PageParameter
        {
            PageIndex = Page(args),
            PageSize = PageSize(args),
            Sort = AlarmRecord._.Id,
            Desc = true,
            RetrieveTotalCount = true,
        };
        // 与 MVC/WebApi AlarmRecordController.Search 同一份。未传 groupId/category/status/关键字时与列表页缺省一致；结束时间留空表示不封顶
        var list = AlarmRecord.Search(-1, null, (AlarmStatuses)(-1), since, default, null, page, ctx.ProjectId);
        var rows = list.Select(e => new { id = e.Id, name = e.Name, category = e.Category, action = e.Action, times = e.Times, content = e.Content, start = e.StartTime }).ToArray();
        var total = (Int32)page.TotalCount;
        return Task.FromResult<Object>(McpResult.Ok($"告警 {rows.Length}/{total} 条", new { records = rows },
            ["stardust_alarm_detail"], null, McpResult.Paged(total, Page(args), PageSize(args))));
    }
}

public class AlarmDetailTool : McpToolBase
{
    public override String Name => "stardust_alarm_detail";
    public override String Description => "Read one alarm and attach the next diagnostic tools for its application.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "monitor";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            alarm_id = new { type = "integer", description = "告警编号" },
        },
        required = new[] { "alarm_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var alarm = ProjectScopeRules.FilterVisible(AlarmRecord.FindById(GetInt64(args, "alarm_id")));
        if (alarm == null) throw new McpException(-32602, "告警不存在。");
        if (!AlarmProject.Contains(alarm, ctx.ProjectId))
            throw new McpException(-32003, $"告警 {alarm.Id} 不属于当前令牌绑定的项目。");

        var app = AlarmProject.FindApp(alarm, ctx.ProjectId);
        return Task.FromResult<Object>(McpResult.Ok($"告警 {alarm.Name}：{alarm.Content}", new
        {
            id = alarm.Id,
            name = alarm.Name,
            action = alarm.Action,
            content = alarm.Content,
            times = alarm.Times,
            app_id = app?.Id,
        }, app == null ? null : ["stardust_app_health", "stardust_trace_search"]));
    }
}

public class AlarmResolveTool : McpToolBase
{
    public override String Name => "stardust_alarm_resolve";
    public override String Description => "Resolve an alarm with a required note. This records the operator decision; it does not delete the alarm.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "monitor";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            alarm_id = new { type = "integer", description = "告警编号" },
            note = new { type = "string", description = "处置意见" },
        },
        required = new[] { "alarm_id", "note" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var note = GetString(args, "note");
        if (note.IsNullOrEmpty()) throw new McpException(-32602, "无效参数：处置意见 note 必填。");
        var alarm = ProjectScopeRules.FilterVisible(AlarmRecord.FindById(GetInt64(args, "alarm_id")));
        if (alarm == null) throw new McpException(-32602, "告警不存在。");
        if (!AlarmProject.Contains(alarm, ctx.ProjectId))
            throw new McpException(-32003, $"告警 {alarm.Id} 不属于当前令牌绑定的项目。");

        if (!alarm.Resolve(note)) throw new McpException(-32000, $"告警 {alarm.Id} 处置失败。");
        return Task.FromResult<Object>(McpResult.Ok($"告警 {alarm.Id} 已记下处置意见", new { alarm.Id, alarm.Action },
            null, "处置意见已追加到内容，并写下结束时间。动作名未改，恢复仍按原对象名匹配。"));
    }
}

/// <summary>告警是否属于令牌项目。应用用已有 FindByName，告警组用 FindById，不扫应用表</summary>
static class AlarmProject
{
    public static Boolean Contains(AlarmRecord alarm, Int32 projectId)
    {
        if (alarm == null || projectId <= 0) return false;
        if (FindApp(alarm, projectId) != null) return true;
        if (alarm.GroupId <= 0) return false;

        var group = ProjectScopeRules.FilterVisible(AlarmGroup.FindById(alarm.GroupId));
        return group != null && group.ProjectId == projectId;
    }

    public static App FindApp(AlarmRecord alarm, Int32 projectId)
    {
        if (alarm == null) return null;

        var app = Match(alarm.Name, projectId) ?? Match(Head(alarm.Action), projectId);
        return app ?? Match(alarm.Category, projectId);
    }

    static App Match(String name, Int32 projectId)
    {
        if (name.IsNullOrEmpty() || projectId <= 0) return null;

        var app = ProjectScopeRules.FilterVisible(App.FindByName(name));
        return app != null && app.ProjectId == projectId ? app : null;
    }

    static String Head(String action)
    {
        if (action.IsNullOrEmpty()) return null;

        var cut = action.IndexOfAny(['/', '\\']);
        return cut > 0 ? action[..cut] : action;
    }
}
