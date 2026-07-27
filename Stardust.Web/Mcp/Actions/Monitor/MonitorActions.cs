using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Web.Mcp.Actions.Monitor;

/// <summary>搜索调用链跟踪数据。按应用/操作名/时间范围查询，支持错误数过滤</summary>
public class MonitorTraceSearchAction : McpActionBase
{
    public override String Name => "monitor_trace_search";
    public override String Description => "搜索应用调用链跟踪数据（按应用/操作名/时间范围），支持错误数过滤。可选传app_id（传则需授权）。";
    public override String Module => "monitor";

    // app_id 可选：传则需要授权，不传则不强制校验
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "app",
        Field = "app_id",
        Optional = true,
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "app_id": {"type": "integer", "description": "可选，按应用过滤（传则需Token已授权该应用）"},
                "name": {"type": "string", "description": "可选，按操作名过滤"},
                "client_id": {"type": "string", "description": "可选，按客户端标识过滤"},
                "min_error": {"type": "integer", "description": "可选，最小错误数阈值，默认0"},
                "kind": {"type": "string", "description": "可选，时间维度（day/hour/minute），默认day"},
                "start": {"type": "string", "description": "可选，开始时间（ISO 8601），默认最近1小时"},
                "end": {"type": "string", "description": "可选，结束时间（ISO 8601），默认当前"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              }
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var appId = GetInt32(@params, "app_id");
        var name = GetString(@params, "name");
        var clientId = GetString(@params, "client_id");
        var minError = GetInt32(@params, "min_error");
        var kind = GetString(@params, "kind"); if (kind.IsNullOrEmpty()) kind = "day";

        var end = DateTime.Now;
        var start = end.AddHours(-1);
        if (@params.TryGetProperty("start", out var sEl) && sEl.ValueKind == JsonValueKind.String && DateTime.TryParse(sEl.GetString(), out var sdt)) start = sdt;
        if (@params.TryGetProperty("end", out var eEl) && eEl.ValueKind == JsonValueKind.String && DateTime.TryParse(eEl.GetString(), out var edt)) end = edt;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var list = TraceData.Search(appId, 0, clientId, name, kind, minError, false, start, end, null, pageParam);

        var records = list.Select(t => new
        {
            id = t.Id,
            app_id = t.AppId,
            name = t.Name,
            client_id = t.ClientId,
            node_id = t.NodeId,
            item_id = t.ItemId,
            stat_date = t.StatDate,
            stat_hour = t.StatHour,
            stat_minute = t.StatMinute,
            total = t.Total,
            errors = t.Errors,
            error_rate = t.Total > 0 ? Math.Round((Double)t.Errors / t.Total * 100, 2) : 0,
            total_cost = t.TotalCost,
            avg_cost = t.Total > 0 ? t.TotalCost / t.Total : 0,
            max_cost = t.MaxCost,
            min_cost = t.MinCost,
            start_time = t.Start,
            end_time = t.End,
        }).ToList();

        return Task.FromResult<Object>(new
        {
            total = pageParam.TotalCount,
            page,
            page_size = pageSize,
            kind,
            start,
            end,
            records,
        });
    }
}

/// <summary>查询告警记录。按类别/状态/时间范围过滤</summary>
public class MonitorAlarmListAction : McpActionBase
{
    public override String Name => "monitor_alarm_list";
    public override String Description => "查询告警记录列表（按类别/状态/时间范围过滤）。可选传app_id（通过Action字段关联应用名，传则需授权）。";
    public override String Module => "monitor";

    // app_id 可选：由于 AlarmRecord 无 AppId 字段，这里仅做声明性授权（若传 app_id 需授权）
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "app",
        Field = "app_id",
        Optional = true,
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "app_id": {"type": "integer", "description": "可选，按应用过滤（传则需Token已授权该应用，通过应用名匹配AlarmRecord.Action）"},
                "category": {"type": "string", "description": "可选，按类别过滤（如 应用下线/节点下线/错误数过高）"},
                "status": {"type": "string", "description": "可选，按状态过滤（Alarming=告警中 / Recovered=已恢复）"},
                "keyword": {"type": "string", "description": "可选，匹配名称/类别/操作/内容/创建者"},
                "start": {"type": "string", "description": "可选，开始时间（ISO 8601），默认最近7天"},
                "end": {"type": "string", "description": "可选，结束时间（ISO 8601），默认当前"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              }
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var appId = GetInt32(@params, "app_id");
        var category = GetString(@params, "category");
        var keyword = GetString(@params, "keyword");

        AlarmStatuses? status = null;
        var statusStr = GetString(@params, "status");
        if (!statusStr.IsNullOrEmpty())
        {
            if (statusStr.EqualIgnoreCase("Alarming")) status = AlarmStatuses.Alarming;
            else if (statusStr.EqualIgnoreCase("Recovered")) status = AlarmStatuses.Recovered;
        }

        var end = DateTime.Now;
        var start = end.AddDays(-7);
        if (@params.TryGetProperty("start", out var sEl) && sEl.ValueKind == JsonValueKind.String && DateTime.TryParse(sEl.GetString(), out var sdt)) start = sdt;
        if (@params.TryGetProperty("end", out var eEl) && eEl.ValueKind == JsonValueKind.String && DateTime.TryParse(eEl.GetString(), out var edt)) end = edt;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        // 若传了 app_id，通过应用名匹配 AlarmRecord.Action 字段
        String? appName = null;
        if (appId > 0)
        {
            var app = Stardust.Data.App.FindById(appId);
            if (app != null) appName = app.Name;
        }

        // 合并 keyword 与 appName（若有）
        var finalKey = keyword;
        if (!appName.IsNullOrEmpty())
        {
            finalKey = finalKey.IsNullOrEmpty() ? appName : $"{finalKey} {appName}";
        }

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var list = AlarmRecord.Search(0, category, status ?? default, start, end, finalKey, pageParam);

        var records = list.Select(a => new
        {
            id = a.Id,
            group_id = a.GroupId,
            name = a.Name,
            category = a.Category,
            action = a.Action,
            status = a.Status.ToString(),
            content = a.Content,
            times = a.Times,
            start_time = a.StartTime,
            end_time = a.EndTime,
            duration = a.Duration,
            creator = a.Creator,
            create_time = a.CreateTime,
        }).ToList();

        return Task.FromResult<Object>(new
        {
            total = pageParam.TotalCount,
            page,
            page_size = pageSize,
            start,
            end,
            records,
        });
    }
}
