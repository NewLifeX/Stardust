using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Monitors;

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
        var apps = App.FindAll(App._.ProjectId == ctx.ProjectId).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var since = Since(args);
        var all = AlarmRecord.FindAll(AlarmRecord._.CreateTime >= since)
            .Where(e => apps.Contains(e.Name) || apps.Contains(e.Category)).OrderByDescending(e => e.Id).ToArray();
        var total = all.Length;
        var rows = all.Skip((Page(args) - 1) * PageSize(args)).Take(PageSize(args))
            .Select(e => new { id = e.Id, name = e.Name, category = e.Category, action = e.Action, times = e.Times, content = e.Content, start = e.StartTime }).ToArray();
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
        var alarm = AlarmRecord.FindById(GetInt64(args, "alarm_id"));
        if (alarm == null) throw new McpException(-32602, "告警不存在。");
        var app = App.FindAll(App._.ProjectId == ctx.ProjectId & App._.Name == alarm.Name).FirstOrDefault();
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
        var alarm = AlarmRecord.FindById(GetInt64(args, "alarm_id"));
        if (alarm == null) throw new McpException(-32602, "告警不存在。");
        alarm.Action = "resolved";
        alarm.Content = (alarm.Content + "\nMCP处置：" + note).Cut(1000);
        alarm.EndTime = DateTime.Now;
        alarm.Update();
        return Task.FromResult<Object>(McpResult.Ok($"告警 {alarm.Id} 已标记处置", new { alarm.Id, alarm.Action },
            null, "告警已标记处置；如需重新打开，手动编辑该告警记录将 action 改回原值"));
    }
}
