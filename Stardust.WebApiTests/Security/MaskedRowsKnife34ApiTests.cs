using System.Text.Json;
using Stardust.Data.Monitors;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Security;

/// <summary>
/// task-95 二期刀3/刀4（2026-11-12，盘点稿 Doc/安全-02 §4 刀3/4）HTTP 集成式迁移（2026-11-14 Q2 裁决）：
/// AlarmGroup/AppTracer WebHook 行级掩码+F2 消解。刀3/4 偏离一期「值级 null」惯例（刀6 "••••••" 同款哨兵）：
/// F2=前端编辑抽屉从列表行回填且 PUT 恒带 webHook 键（alarmGroup.logic groupBody / appTracer.logic tracerPutBody）
/// ——null 回填→PUT 空→静默清库；哨兵回填→原样提交→Valid POST 守卫识别还原。断言=键在值等于哨兵（非 null）。
/// 与原 ServerTests 反射版差异：Valid GET 回显（MVC 编辑表单输入侧）非 API 面，不再断言；
/// 空值放行（显式清空）改经真实 PUT 管道；读回走实体 Find 直连查询。
/// </summary>
[Collection(WebApiCollection.Name)]
public class MaskedRowsKnife34ApiTests
{
    private const String Mask = "••••••";

    private readonly ApiClient _api;

    public MaskedRowsKnife34ApiTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    [Fact(DisplayName = "task-95 刀3：AlarmGroup 列表行 WebHook=掩码哨兵（F2 哨兵语义，非 null）")]
    public async Task AlarmGroup_Index_WebHook_Sentinel()
    {
        var tag = "mw3a-" + Guid.NewGuid().ToString("N")[..8];
        var e = new AlarmGroup
        {
            Name = tag + "-grp", WebHook = "https://hook.example/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Monitors/AlarmGroup?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.Sentinel(mine, "webHook", Mask, "AlarmGroup");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀3：AlarmGroup Detail 哨兵；PUT 守卫真写读回原值；空串放行=显式清空")]
    public async Task AlarmGroup_Detail_Guard_And_Clear()
    {
        var tag = "mw3b-" + Guid.NewGuid().ToString("N")[..8];
        var e = new AlarmGroup
        {
            Name = tag + "-grp", WebHook = "https://keep.example/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            // Detail 掩码哨兵
            var detail = await _api.GetJsonAsync($"Monitors/AlarmGroup/{e.Id}");
            ApiRowAssert.DetailSentinel(detail.Data, "webHook", Mask, "AlarmGroup");

            // PUT 守卫：哨兵原样提交（F2 回填形态）→ Valid 识别还原 → 真写读回原值
            var put = new { id = e.Id, name = e.Name, webHook = Mask, enable = false };
            var api = await _api.PutApiAsync<JsonElement>($"Monitors/AlarmGroup/{e.Id}", put);
            var back = AlarmGroup.FindById(e.Id);
            Assert.NotNull(back);
            Assert.Equal("https://keep.example/" + tag, back.WebHook);

            // 空值放行：显式清空（空串不走哨兵分支直通写库）
            var clear = new { id = e.Id, name = e.Name, webHook = "", enable = true };
            var api2 = await _api.PutApiAsync<JsonElement>($"Monitors/AlarmGroup/{e.Id}", clear);
            var cleared = AlarmGroup.FindById(e.Id);
            Assert.NotNull(cleared);
            Assert.True(String.IsNullOrEmpty(cleared.WebHook),
                $"空串 PUT 应显式清空 WebHook，实际='{cleared.WebHook}'");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀4：AppTracer 列表行/Detail WebHook=掩码哨兵 + PUT 守卫真写读回")]
    public async Task AppTracer_Row_Detail_Guard()
    {
        var tag = "mw4-" + Guid.NewGuid().ToString("N")[..8];
        var e = new AppTracer
        {
            Name = tag + "-trc", WebHook = "https://tracer.example/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Monitors/AppTracer?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");
            ApiRowAssert.Sentinel(mine, "webHook", Mask, "AppTracer");

            var detail = await _api.GetJsonAsync($"Monitors/AppTracer/{e.ID}");
            ApiRowAssert.DetailSentinel(detail.Data, "webHook", Mask, "AppTracer");

            var put = new { id = e.ID, webHook = Mask };
            var api = await _api.PutApiAsync<JsonElement>($"Monitors/AppTracer/{e.ID}", put);
            var back = AppTracer.FindByKey(e.ID);
            Assert.NotNull(back);
            Assert.Equal("https://tracer.example/" + tag, back.WebHook);
        }
        finally { e.Delete(); }
    }
}
