using System;
using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Trace;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>trace_detail 分表路由回归测试。
/// 现场：远端调用 stardust_trace_detail 报 Invalid object name 'TraceData'——
/// TraceData/SampleData 是按天分表实体（TraceData_{dd}/SampleData_{dd}，没有基础表），工具却裸查了 TraceData。
/// 必须造出多个 DataId，让 WhereExpression 保持为 Id In(...)；
/// 单个 Id 时 In 会退化为等于，XCode 能自动定位分片，测不出缺表问题。</summary>
public class TraceDetailShardTests
{
    private const Int32 ProjectId = 999777;

    public TraceDetailShardTests() => StarDataHelper.SplitSqliteTables();

    [Fact(DisplayName = "trace_detail/trace_search 均按天分片查询，不查基础表也不漏当天")]
    public async Task TraceDetail_RoutesToDailyShards()
    {
        var today = DateTime.Today;
        var app = new App
        {
            Name = "trace-shard-" + Guid.NewGuid().ToString("n")[..8],
            ProjectId = ProjectId,
            Enable = true,
        };
        app.Insert();

        var traceId = "shard-" + Guid.NewGuid().ToString("n")[..16];
        var now = DateTime.Now.ToLong();
        var traceData1 = new TraceData
        {
            AppId = app.Id,
            ClientId = "test-client",
            Name = "shard-op-1",
            Cost = 1,
            Total = 1,
            StatDate = today,
            StartTime = now,
            EndTime = now,
        };
        var traceData2 = new TraceData
        {
            AppId = app.Id,
            ClientId = "test-client",
            Name = "shard-op-2",
            Cost = 1,
            Total = 1,
            StatDate = today,
            StartTime = now,
            EndTime = now,
        };
        using (var split = TraceData.Meta.CreateShard(today))
        {
            traceData1.Insert();
            traceData2.Insert();
        }

        var sample1 = new SampleData
        {
            DataId = traceData1.Id,
            TraceId = traceId,
            SpanId = traceId + "-span-1",
            Success = true,
            StartTime = now,
            EndTime = now,
            CreateTime = DateTime.Now,
        };
        var sample2 = new SampleData
        {
            DataId = traceData2.Id,
            TraceId = traceId,
            SpanId = traceId + "-span-2",
            Success = true,
            StartTime = now,
            EndTime = now,
            CreateTime = DateTime.Now,
        };
        using (var split = SampleData.Meta.CreateShard(today))
        {
            sample1.Insert();
            sample2.Insert();
        }

        try
        {
            // TokenId 取一个无资源授权记录的值，避免 CheckResource 受测试库历史数据影响
            var ctx = new McpContext { TokenId = 999999, ProjectId = ProjectId };

            var byTraceId = await new TraceDetailTool().InvokeAsync(JsonSerializer.SerializeToElement(new { trace_id = traceId }), ctx);
            var byTraceJson = JsonSerializer.Serialize(byTraceId);
            Assert.Contains(sample1.SpanId, byTraceJson);
            Assert.Contains(sample2.SpanId, byTraceJson);

            var byDataId = await new TraceDetailTool().InvokeAsync(JsonSerializer.SerializeToElement(new { data_id = traceData1.Id }), ctx);
            Assert.Contains(sample1.SpanId, JsonSerializer.Serialize(byDataId));

            // 默认 since=最近24h，分片日期是昨天；跨天聚合后应能查到今天写入的跟踪数据
            var search = await new TraceSearchTool().InvokeAsync(JsonSerializer.SerializeToElement(new { app_id = app.Id }), ctx);
            var searchJson = JsonSerializer.Serialize(search);
            Assert.Contains(traceData1.Name, searchJson);
            Assert.Contains(traceData2.Name, searchJson);
        }
        finally
        {
            using (var split = SampleData.Meta.CreateShard(today))
            {
                sample1.Delete();
                sample2.Delete();
            }
            using (var split = TraceData.Meta.CreateShard(today))
            {
                traceData1.Delete();
                traceData2.Delete();
            }
            app.Delete();
        }
    }
}
