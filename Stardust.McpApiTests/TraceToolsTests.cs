using System.Text.Json;
using ModelContextProtocol.Client;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>调用链 MCP 工具集成测试（2026-11-05 分表路由回归）。
/// 回归背景：远端 stardust_trace_detail 报 -32000 Invalid object name 'TraceData'——
/// TraceData/SampleData 是按天分表实体、没有基础表；单 DataId 的 Id.In(...) 会退化为等于，
/// XCode 能自动定位分片，只有「同一条 TraceId 关联多个 DataId」时才会裸查基础表。
/// 因此初始化器特意造了两个 DataId 共用一个 TraceId，本组用例锁三件事：
/// ① trace_id 入口能跨多个 DataId 返回完整片段；② data_id 入口可用；③ trace_search 默认 24h 窗口能查到当天数据。</summary>
[Collection(nameof(McpApiTestCollection))]
public class TraceToolsTests : McpSdkTestBase
{
    public TraceToolsTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "调用链-1：trace_detail 按 trace_id 返回多 DataId 的全部片段")]
    public async Task TraceDetail_ByTraceId_MultiDataIds()
    {
        var traceId = McpTestDataInitializer.TraceId;
        Assert.False(String.IsNullOrEmpty(traceId));

        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_trace_detail",
                new Dictionary<String, Object?> { ["trace_id"] = traceId }));
            var records = content.GetProperty("data").GetProperty("records");
            Assert.Equal(JsonValueKind.Array, records.ValueKind);
            Assert.True(records.GetArrayLength() >= 2,
                $"同一 TraceId 应返回两个 DataId 对应的片段，实际 {records.GetArrayLength()} 个（多值 Id In 分表路由回归）");
            Output.WriteLine($"✅ trace_detail 返回 {records.GetArrayLength()} 个片段");
        });
    }

    [Fact(DisplayName = "调用链-2：trace_detail 按 data_id 返回对应片段")]
    public async Task TraceDetail_ByDataId()
    {
        var dataId = McpTestDataInitializer.TraceDataId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_trace_detail",
                new Dictionary<String, Object?> { ["data_id"] = dataId }));
            var records = content.GetProperty("data").GetProperty("records");
            Assert.True(records.GetArrayLength() >= 1,
                $"data_id={dataId} 应返回至少 1 个片段，实际 {records.GetArrayLength()} 个");
            Output.WriteLine($"✅ trace_detail(data_id) 返回 {records.GetArrayLength()} 个片段");
        });
    }

    [Fact(DisplayName = "调用链-3：trace_search 默认 24h 窗口能看到当天造数")]
    public async Task TraceSearch_DefaultWindow_SeesToday()
    {
        var appId = McpTestDataInitializer.AppId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_trace_search",
                new Dictionary<String, Object?> { ["app_id"] = appId }));
            var records = content.GetProperty("data").GetProperty("records");
            Assert.True(records.GetArrayLength() >= 1,
                $"默认 24h 窗口应至少看到初始化器造的当天 TraceData，实际 {records.GetArrayLength()} 条（跨天分片回归）");
            Output.WriteLine($"✅ trace_search 默认窗口返回 {records.GetArrayLength()} 条");
        });
    }
}
