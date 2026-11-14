using System.Text.Json;
using Stardust.Data;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Security;

/// <summary>
/// task-95 二期刀2（2026-11-12，盘点稿 Doc/安全-02 §4 刀2）HTTP 集成式迁移（2026-11-14 Q2 裁决）：
/// App.Secret/WebHook 行级脱敏。前置：刀1 预览链已改道 /Config/GetAllByApp（行内 Secret 零消费方）；
/// F3 先例：apps 编辑=partial-PUT，Secret 留空不提交、WebHook 不在 Vue 表单（不 Dirty）→ 行裁不破编辑流；
/// Valid "*****" 守卫（AppDeploy 先例）保编辑表单回填原样提交不写坏。
/// 与原 ServerTests 反射版差异：Valid GET 回显（MVC 编辑表单输入侧）非 API 面，不再断言；读回走实体 Find 直连查询。
/// 新管线（Cube 1105 字段集合投影）契约定谳（2026-11-14 首轮实测）：
/// 列表行 Secret/WebHook、详情 Secret **键级缺席**（meta 字段集已裁——比键在值 null 更强防泄漏）；
/// 详情 WebHook 键仍在（掩码值 null）。掩码机制=薄基类 GetRowMasks + OnProject 统一应用。
/// </summary>
[Collection(WebApiCollection.Name)]
public class MaskedRowsKnife2ApiTests
{
    private const String Mask = "*****";

    private readonly ApiClient _api;

    public MaskedRowsKnife2ApiTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    [Fact(DisplayName = "task-95 刀2：App 列表行 Secret/WebHook 键级缺席（meta 已裁，明文零泄漏）")]
    public async Task App_Index_Row_Secret_WebHook_Should_Be_Masked()
    {
        var tag = "mw2a-" + Guid.NewGuid().ToString("N")[..8];
        var e = new App
        {
            Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
            Secret = "sec-" + tag, WebHook = "http://hook/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Registry/App?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.KeyAbsent(mine, "secret", "App");
            ApiRowAssert.KeyAbsent(mine, "webHook", "App");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀2：App Detail——Secret 键缺席，WebHook 键在值 null")]
    public async Task App_Detail_Crops_Secret_WebHook()
    {
        var tag = "mw2b-" + Guid.NewGuid().ToString("N")[..8];
        var e = new App
        {
            Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
            Secret = "sec-" + tag, WebHook = "http://hook/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var api = await _api.GetJsonAsync($"Registry/App/{e.Id}");

            ApiRowAssert.DetailHidden(api.Data, "secret", "App");
            ApiRowAssert.DetailMasked(api.Data, "webHook", "App");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀2：App F1 提交——PUT '*****' 守卫恢复原值并真写读回不变；WebHook 原值提交不受影响")]
    public async Task App_Put_MaskedSecret_Restores_Old()
    {
        var tag = "mw2c-" + Guid.NewGuid().ToString("N")[..8];
        var e = new App
        {
            Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
            Secret = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            // 模拟 MVC 编辑表单：Secret 回显 "*****" 原样提交；WebHook 服务端表单原值提交（非掩码）
            var put = new
            {
                id = e.Id,
                name = e.Name,
                displayName = e.DisplayName,
                category = e.Category,
                secret = Mask,
                webHook = e.WebHook,
                enable = true,
            };
            var api = await _api.PutApiAsync<JsonElement>($"Registry/App/{e.Id}", put);

            var back = App.FindById(e.Id);
            Assert.NotNull(back);
            Assert.Equal("real-" + tag, back.Secret);
            Assert.Equal("http://keep/" + tag, back.WebHook);
        }
        finally { e.Delete(); }
    }
}
