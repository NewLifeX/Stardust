using System.Text.Json;
using Stardust.Data.Monitors;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>CARD-MON-0506（task-71）：时/分统计 type 假过滤修复。
/// 缺陷实锤：TraceDayStat 实体自带 Type 列（SQL 过滤正确），TraceHourStat/TraceMinuteStat
/// 实体无 Type 列且控制器不消费 type 参数 → 请求 200 但返回混合种类行（假过滤）。
/// TDD 先红（四例全红：混合行/小写变体/空集退化/Minute 对称）→ A1 方案补过滤
/// （控制器读 type → TraceItem.GetKind 内存筛 ItemId 集 → 实体 Search itemIds[] 重载 In 过滤，
/// EqualIgnoreCase 对齐 Day 先例 跟踪每日统计.Biz.cs L149）→ 绿。
/// 空集短路语义：type 有值但该应用无匹配种类埋点 → 空列表（非退化全量）——过滤条件无匹配≠忽略条件。</summary>
[Collection(WebApiCollection.Name)]
public class TraceStatTypeFilterTests
{
    private static readonly Lazy<Seed> _seed = new(() => new Seed());

    private readonly ApiClient _api;

    public TraceStatTypeFilterTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _api = new(factory, output);
        _ = _seed.Value;
    }

    private sealed class Seed
    {
        /// <summary>专用 appId，避免与其他测试类共享种子互扰（临时库内 factory 生命周期共享）</summary>
        public const Int32 AppId = 50601;

        public readonly TraceItem HttpItem;
        public readonly TraceItem ApiItem;

        public Seed()
        {
            // GetKind(Name) 派生规则（跟踪项.Biz.cs:187-200）：http:// 前缀→http；/ 开头→api
            HttpItem = GetOrAddItem(AppId, "http://10.188.32.212:6680/files");
            ApiItem = GetOrAddItem(AppId, "/api/galaxy/status");

            var today = DateTime.Today;
            GetOrAddHour(AppId, HttpItem.Id, HttpItem.Name, today);
            GetOrAddHour(AppId, ApiItem.Id, ApiItem.Name, today);
            GetOrAddMinute(AppId, HttpItem.Id, HttpItem.Name, today);
            GetOrAddMinute(AppId, ApiItem.Id, ApiItem.Name, today);
        }

        private static TraceItem GetOrAddItem(Int32 appId, String name)
        {
            var entity = TraceItem.Find(TraceItem._.AppId == appId & TraceItem._.Name == name);
            if (entity != null) return entity;

            entity = new TraceItem { AppId = appId, Name = name, Kind = TraceItem.GetKind(name), Enable = true };
            entity.Insert();
            return entity;
        }

        private static void GetOrAddHour(Int32 appId, Int32 itemId, String name, DateTime statTime)
        {
            var entity = TraceHourStat.Find(TraceHourStat._.AppId == appId & TraceHourStat._.ItemId == itemId & TraceHourStat._.StatTime == statTime);
            if (entity != null) return;

            entity = new TraceHourStat { AppId = appId, ItemId = itemId, Name = name, StatTime = statTime, Total = 100 };
            entity.Insert();
        }

        private static void GetOrAddMinute(Int32 appId, Int32 itemId, String name, DateTime statTime)
        {
            var entity = TraceMinuteStat.Find(TraceMinuteStat._.AppId == appId & TraceMinuteStat._.ItemId == itemId & TraceMinuteStat._.StatTime == statTime);
            if (entity != null) return;

            entity = new TraceMinuteStat { AppId = appId, ItemId = itemId, Name = name, StatTime = statTime, Total = 100 };
            entity.Insert();
        }
    }

    /// <summary>统一请求：显式 dtStart/dtEnd=今天，绕开 ResolveDateRange 未传参语义，保证 Between 确定性</summary>
    private async Task<JsonElement> GetRowsAsync(String entity, String type)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"Monitors/{entity}?appId={Seed.AppId}&dtStart={today}&dtEnd={today}&pageSize=100";
        if (!String.IsNullOrEmpty(type)) url += $"&type={Uri.EscapeDataString(type)}";

        var api = await _api.GetJsonAsync(url);
        Assert.Equal(0, api.Code);
        Assert.Equal(JsonValueKind.Array, api.Data.ValueKind);
        return api.Data;
    }

    private static List<String> RowNames(JsonElement rows)
    {
        var list = new List<String>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.TryGetProperty("Name", out var name) || row.TryGetProperty("name", out name))
                list.Add(name.GetString() ?? "");
        }
        return list;
    }

    [Fact]
    public async Task HourStat_TypeFiltersByKind_OnlyMatchingRows()
    {
        // type=Api → 仅 api 行（/api/galaxy/status），http 行（http://10.188.32.212:6680/files）被滤除
        var rows = await GetRowsAsync("TraceHourStat", "Api");
        var names = RowNames(rows);

        Assert.Single(names);
        Assert.Contains("/api/galaxy/status", names);
        Assert.DoesNotContain("http://10.188.32.212:6680/files", names);
    }

    [Fact]
    public async Task HourStat_TypeLowerCase_MatchesToo()
    {
        // EqualIgnoreCase 对齐 Day 先例（跟踪每日统计.Biz.cs L149）：type=api 小写同效
        var rows = await GetRowsAsync("TraceHourStat", "api");
        var names = RowNames(rows);

        Assert.Single(names);
        Assert.Contains("/api/galaxy/status", names);
    }

    [Fact]
    public async Task HourStat_TypeNoMatch_ReturnsEmpty()
    {
        // 空集短路：该应用无 redis 埋点 → 空列表（红=当前退化全量返回 2 行）
        var rows = await GetRowsAsync("TraceHourStat", "Redis");

        Assert.False(rows.EnumerateArray().Any(), $"type 无匹配埋点应返回空列表，实际：{rows}");
    }

    [Fact]
    public async Task MinuteStat_TypeFiltersByKind_OnlyMatchingRows()
    {
        // Minute 对称补（j-config 定稿闸裁补）：同款行为，三粒度一致
        var rows = await GetRowsAsync("TraceMinuteStat", "Api");
        var names = RowNames(rows);

        Assert.Single(names);
        Assert.Contains("/api/galaxy/status", names);
        Assert.DoesNotContain("http://10.188.32.212:6680/files", names);
    }
}
