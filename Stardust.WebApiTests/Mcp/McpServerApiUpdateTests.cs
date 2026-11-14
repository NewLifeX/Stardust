using System.Text.Json;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Mcp;

/// <summary>
/// DEF-MW-SRV-01 回归（HTTP 集成式迁移，2026-11-14 Q2 裁决）：PUT /api/Platform/McpServer/{id} 更新链路双态炸
/// （j-deploy-b 2026-11-11 CRUD 走查实锤）。生产机制：请求体可重读 → 基类 GetSubmittedFieldsAsync 返回提交键集 →
/// CopySubmitted 用属性反射把提交值写到库中实体（仅属性通道）；但 XCode 属性 setter 不写 Extends 值存储 →
/// ① 全字段改名 PUT：属性通道=新值（Valid 过）→ OnUpdate→entity.Update() 从 Extends 读值 → UPDATE 落旧值
/// = code=0 静默空更新（现象 A）；② 部分提交（UI toggle 仅 {id,enable}，mcpToken.logic.ts 契约）：
/// 未提交字段属性通道保持默认空 → Valid 读 Name 属性=空 → 500「名称不能为空」（现象 B）。
/// 修复：WebEntityController.Update 镜像 Insert（F-PLT-1）双通道补值——提交字段属性+SetItem 双写并显式置脏，
/// 未提交字段属性通道对齐 Extends 库值。
/// 迁后经真实 HTTP PUT 管道驱动（请求体天然可重读，与生产等态）；断言全部走 HTTP 面：
/// Id 取写响应 data.id（主键恒带），落库值经 GET 详情面读回（详情面由服务端按主键加载，
/// 回包为字段集合投影），清理用键实体 Delete。
/// </summary>
[Collection(WebApiCollection.Name)]
public class McpServerApiUpdateTests
{
    private readonly ApiClient _api;

    public McpServerApiUpdateTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    /// <summary>从写响应 data 中取自增 Id（主键恒带）。</summary>
    private static Int32 RowId(JsonElement data)
    {
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        var id = 0;
        Assert.True(data.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out id) && id > 0,
            $"写响应应带自增 id（主键恒带），实际 data={data}");
        return id;
    }

    /// <summary>经 HTTP POST 造一行（走 F-PLT-1 修复链），Id 取写响应。</summary>
    private async Task<Int32> SeedRowAsync(String name, Boolean enable)
    {
        var api = await _api.PostApiAsync<JsonElement>("Platform/McpServer", new
        {
            name,
            url = "http://127.0.0.1:9104",
            enable,
            projectId = 0,
            remark = "DEF-MW-SRV-01 seed",
        });
        Assert.Equal(0, api.Code);
        return RowId(api.Data);
    }

    /// <summary>GET 详情面读回（服务端按主键加载，回包=字段集合投影）。</summary>
    private async Task<JsonElement> DetailAsync(Int32 id)
    {
        var api = await _api.GetJsonAsync($"Platform/McpServer/{id}");
        Assert.Equal(JsonValueKind.Object, api.Data.ValueKind);
        return api.Data;
    }

    [Fact(DisplayName = "DEF-MW-SRV-01 回归：改名 PUT 必须真实落库，详情面读回新值，禁止静默空更新")]
    public async Task Update_Rename_Should_Persist_NewName()
    {
        var seedName = "mw01a-seed-" + Guid.NewGuid().ToString("N")[..8];
        var newName = "mw01a-renamed-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedRowAsync(seedName, true);
        try
        {
            // 前端全字段提交契约（McpServerForm 编辑 PUT）：Id+五业务字段
            var api = await _api.PutApiAsync<JsonElement>($"Platform/McpServer/{id}", new
            {
                id,
                name = newName,
                url = "http://127.0.0.1:9104",
                enable = true,
                projectId = 0,
                remark = "DEF-MW-SRV-01 rename",
            });
            Assert.Equal(0, api.Code);

            var detail = await DetailAsync(id);
            Assert.Equal(newName, detail.GetProperty("name").GetString());   // 库值=新名（修复前=旧名静默空更新）
            Assert.True(IsOn(detail.GetProperty("enable")));
        }
        finally
        {
            McpServer.FindById(id)?.Delete();
        }
    }

    [Fact(DisplayName = "DEF-MW-SRV-01 回归：启停 PUT（UI 部分提交 {id,enable}）必须真实落库且不清空未提交字段")]
    public async Task Update_ToggleEnable_Should_Persist()
    {
        var seedName = "mw01b-seed-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedRowAsync(seedName, true);
        try
        {
            // UI toggle 契约（mcpToken.logic.ts：PUT 启停仅提交 {id,enable}）：部分提交形态，
            // 未提交字段不得被清空或改名（修复前=500「名称不能为空」）
            var api = await _api.PutApiAsync<JsonElement>($"Platform/McpServer/{id}", new { id, enable = false });
            Assert.Equal(0, api.Code);

            var detail = await DetailAsync(id);
            Assert.False(IsOn(detail.GetProperty("enable")));   // 库值=停用（修复前=仍启用）
            Assert.Equal(seedName, detail.GetProperty("name").GetString());
            Assert.Equal("http://127.0.0.1:9104", detail.GetProperty("url").GetString());   // 未提交字段不得被清空
        }
        finally
        {
            McpServer.FindById(id)?.Delete();
        }
    }

    /// <summary>enable 键在详情面可为 JSON false 或数字 0（字段投影值类型随实体序列化）。</summary>
    private static Boolean IsOn(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
        _ => throw new Xunit.Sdk.XunitException($"enable 值类型异常：{v.ValueKind}:{v}"),
    };
}
