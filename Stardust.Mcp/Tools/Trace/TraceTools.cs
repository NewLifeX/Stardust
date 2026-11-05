using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Monitors;

namespace Stardust.Mcp.Tools.Trace;

public class TraceSearchTool : McpToolBase
{
    public override String Name => "stardust_trace_search";
    public override String Description => "Search slow or failed traces for one application. Optional min_cost milliseconds and error_only. Paged.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "monitor";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
            min_cost = new { type = "integer", description = "最小耗时毫秒" },
            error_only = new { type = "boolean", description = "只看错误" },
            since = new { type = "string", description = "时间窗，如 -24h 或 ISO 时间，默认最近24小时" },
            page_size = new { type = "integer", description = "每页条数，默认20，最大100" },
        },
        required = new[] { "app_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var min = GetInt32(args, "min_cost");
        var errorOnly = GetBoolean(args, "error_only");
        var since = Since(args).Date;
        // 复用实体 Biz 层查询（与 MVC/WebApi 同一份）：跨天逐分片聚合，避免只查 since 起始分片而漏掉今天
        var list = TraceData.FindAllByAppIdAndTime(app.Id, since, DateTime.Today, errorOnly ? 1 : -1);
        var rows = list.Where(e => e.Cost >= min)
            .OrderByDescending(e => e.Cost).Take(PageSize(args))
            .Select(e => new { e.Id, e.Name, e.Cost, e.Errors, e.Total, client = e.ClientId, node_id = e.NodeId }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{app.Name} 调用 {rows.Length} 条", new { records = rows }, ["stardust_trace_detail"]));
    }
}

public class TraceDetailTool : McpToolBase
{
    public override String Name => "stardust_trace_detail";
    public override String Description => "Read one trace waterfall by trace_id or data_id, including spans, cost and errors.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "monitor";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            data_id = new { type = "integer", description = "采样数据编号，与 trace_id 二选一" },
            trace_id = new { type = "string", description = "链路追踪Id，与 data_id 二选一" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var id = GetInt64(args, "data_id");
        var traceId = GetString(args, "trace_id");
        if (id <= 0 && traceId.IsNullOrEmpty()) throw new McpException(-32602, "请传 data_id 或 trace_id。");

        // SampleData/TraceData 按天分表（SQLite 下按天分库），都没有基础表。
        // 采样查询走 Biz.Search 的 AutoShard 逐分片扫描（与 Web 调用链页一致，覆盖最近30天），避免裸查基础表。
        var start = DateTime.Today.AddDays(-30);
        var end = DateTime.Today;
        var samples = SampleData.Search(id, traceId, null, start, end, new PageParameter { PageIndex = 1, PageSize = 10_000 });

        // 项目隔离：SampleData 本身无 AppId，需经 DataId 反查 TraceData.AppId，只保留属于当前项目的片段。
        // TraceData 同样按天分表，复用实体 Biz 层的分片感知查询（与 MVC/WebApi 同一份）
        var projectAppIds = App.FindAllByProjectId(ctx.ProjectId).Select(e => e.Id).ToHashSet();
        var dataIds = samples.Select(e => e.DataId).Distinct().ToArray();
        var appIdByDataId = TraceData.FindAllByIds(dataIds).ToDictionary(e => e.Id, e => e.AppId);
        var filtered = samples.Where(e => appIdByDataId.TryGetValue(e.DataId, out var appId) && projectAppIds.Contains(appId)).ToList();
        if (filtered.Count == 0) throw new McpException(-32003, "未找到该调用链，或它不属于当前令牌绑定的项目。");

        var rows = filtered.OrderBy(e => e.StartTime).Take(100)
            .Select(e => new { e.SpanId, e.ParentId, e.Cost, e.Success, error = e.Error, tag = e.Tag }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"调用链片段 {rows.Length} 个", new { records = rows }));
    }
}
