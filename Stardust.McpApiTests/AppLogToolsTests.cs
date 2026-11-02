using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>app_logs / app_health 集成测试（2026-11-02 AppClientLog 分表修复配套）。
/// 回归背景：两工具旧实现用无时间条件查询打分表实体的基础表（分表实体没有基础表），
/// 全新环境报 -32000 Invalid object name 'AppClientLog'，远端 6681 实测踩中；用户指出
/// 「就是测试缺了所以没发现」——本地旧初始化器裸 SQL 建基础表人为掩盖了缺陷。
/// 修复后走 Id.Between 时间窗分表感知查询。本组用例锁四件事：
/// ① 缺表/全新分库环境优雅空数据（未来窗=明年分库必不存在）② 时间窗与级别过滤语义
/// ③ 逆序时间窗 -32602 ④ app_health 结构（recent_errors/log_note）。</summary>
[Collection(nameof(McpApiTestCollection))]
public class AppLogToolsTests : McpSdkTestBase
{
    public AppLogToolsTests(ITestOutputHelper output) : base(output) { }

    [Fact(DisplayName = "日志-1：app_logs 默认窗口能查到初始化器造的日志（分表可见）")]
    public async Task AppLogs_DefaultWindow_SeesSeeded()
    {
        var appId = McpTestDataInitializer.AppId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_app_logs",
                new Dictionary<String, Object?> { ["app_id"] = appId }));
            var data = content.GetProperty("data");
            var records = data.GetProperty("records");
            Assert.Equal(JsonValueKind.Array, records.ValueKind);
            Assert.True(records.GetArrayLength() >= 1,
                $"默认 24h 窗口应至少看到初始化器实体 Insert 的 1 条日志（分库/分表与 6681 共享 Bin\\Data）");
            Output.WriteLine($"✅ app_logs 默认窗口 {records.GetArrayLength()} 条（含分表造数）");
        });
    }

    [Fact(DisplayName = "日志-2：未来时间窗（全新分库/分表必不存在）优雅空数据不报 -32000")]
    public async Task AppLogs_FutureWindow_EmptyGraceful()
    {
        var appId = McpTestDataInitializer.AppId;
        // 明年同刻：ConnPolicy 按月分库 → StardustData_{明年yyyyMM} 分库此刻必不存在。
        // 旧实现在这里会 -32000 Invalid object name；修复后 XCode 自动建分库/分表并返回空。
        var start = DateTime.Now.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");
        var end = DateTime.Now.AddYears(1).AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_app_logs",
                new Dictionary<String, Object?>
                {
                    ["app_id"] = appId,
                    ["dt_start"] = start,
                    ["dt_end"] = end,
                }));
            var records = content.GetProperty("data").GetProperty("records");
            Assert.Equal(0, records.GetArrayLength());
            Output.WriteLine("✅ 未来窗（全新分库/分表）返回空数据而非 -32000");
        });
    }

    [Fact(DisplayName = "日志-3：dt_start 晚于 dt_end 应被网关以 -32602 拒绝")]
    public async Task AppLogs_ReversedWindow_InvalidParams()
    {
        var appId = McpTestDataInitializer.AppId;
        var start = DateTime.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm:ss");
        var end = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var ex = await AssertRejectedAsync(
                () => client.CallToolAsync("stardust_app_logs",
                    new Dictionary<String, Object?>
                    {
                        ["app_id"] = appId,
                        ["dt_start"] = start,
                        ["dt_end"] = end,
                    }).AsTask(),
                -32602);
            Output.WriteLine($"✅ 逆序时间窗 -32602：{ex.Message}");
        });
    }

    [Fact(DisplayName = "日志-4：level 过滤后每行级别字段均为该级别")]
    public async Task AppLogs_LevelFilter()
    {
        var appId = McpTestDataInitializer.AppId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var all = AssertOk(await client.CallToolAsync("stardust_app_logs",
                new Dictionary<String, Object?> { ["app_id"] = appId }));
            var nAll = all.GetProperty("data").GetProperty("records").GetArrayLength();

            var info = AssertOk(await client.CallToolAsync("stardust_app_logs",
                new Dictionary<String, Object?> { ["app_id"] = appId, ["level"] = "Info" }));
            var records = info.GetProperty("data").GetProperty("records");
            foreach (var r in records.EnumerateArray())
                Assert.Equal("Info", r.GetProperty("level").GetString());
            Output.WriteLine($"✅ level=Info：全量 {nAll} 条 → 过滤后 {records.GetArrayLength()} 条且级别全为 Info");
        });
    }

    [Fact(DisplayName = "日志-5：app_health 返回 recent_errors 数组与 log_note（缺表也不报错）")]
    public async Task AppHealth_Structure()
    {
        var appId = McpTestDataInitializer.AppId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_app_health",
                new Dictionary<String, Object?> { ["app_id"] = appId }));
            var data = content.GetProperty("data");
            Assert.Equal(JsonValueKind.Array, data.GetProperty("recent_errors").ValueKind);
            var note = data.GetProperty("log_note");
            Assert.True(note.ValueKind is JsonValueKind.Null or JsonValueKind.String,
                $"log_note 应为 null 或字符串说明：{note}");
            Output.WriteLine($"✅ app_health 结构正确（recent_errors 数组，log_note={note}）");
        });
    }
}
