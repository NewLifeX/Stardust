using System.Text.Json;
using NewLife;
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
        var exp = TraceData._.AppId == app.Id & TraceData._.StatDate >= Since(args).Date;
        // SQLite 下 TraceData/SampleData 已按天分库分表（SplitSqliteTables），查询必须包分片路由，否则查主表报 no such table
        var since = Since(args).Date;
        using var split = TraceData.Meta.CreateShard(since);
        var rows = TraceData.FindAll(exp).Where(e => e.Cost >= min && (!errorOnly || e.Errors > 0))
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
        // SQLite 下 SampleData/TraceData 按天分库分表，必须包分片路由
        using var split = SampleData.Meta.CreateShard(DateTime.Today);
        var samples = id > 0
            ? SampleData.FindAll(SampleData._.DataId == id)
            : SampleData.FindAll(SampleData._.TraceId == traceId);

        // 项目隔离：SampleData 本身无 AppId，需经 DataId 反查 TraceData.AppId，只保留属于当前项目的片段
        var projectAppIds = App.FindAll(App._.ProjectId == ctx.ProjectId).Select(e => e.Id).ToHashSet();
        var dataIds = samples.Select(e => e.DataId).Distinct().ToArray();
        var appIdByDataId = dataIds.Length == 0
            ? new Dictionary<Int64, Int32>()
            : TraceData.FindAll(TraceData._.Id.In(dataIds)).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First().AppId);
        var filtered = samples.Where(e => appIdByDataId.TryGetValue(e.DataId, out var appId) && projectAppIds.Contains(appId)).ToList();
        if (filtered.Count == 0) throw new McpException(-32003, "该调用链不属于当前令牌绑定的项目。");

        var rows = filtered.OrderBy(e => e.StartTime).Take(100)
            .Select(e => new { e.SpanId, e.ParentId, e.Cost, e.Success, error = e.Error, tag = e.Tag }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"调用链片段 {rows.Length} 个", new { records = rows }));
    }
}
