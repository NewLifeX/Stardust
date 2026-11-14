using System.Text.Json;
using NewLife.Cube;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Mcp;

/// <summary>
/// F-PLT-1 回归（HTTP 集成式迁移，2026-11-14 Q2 裁决）：POST /api/Platform/McpServer 曾全部丢值 500「SQL logic error near ','」。
/// 根因（build#7 echo 双槽结构定谳）：API 版 NewLife.Cube 模型绑定把 JSON 请求值写入实体属性，而 XCode 持久化读取的
/// 值存储（Extends）为空——属性通道与持久化通道脱节，SQL 渲染全默认值。修复（build#8 终态）：
/// WebEntityController.Insert 对绑定实体本体逐业务字段双通道补值（属性值→值存储同步）→ OnInsert（业务钩子）→
/// 二次同步（钩子生成字段如 Token）→ Insert（内部 Valid 在钩子后，与部署版顺序一致）。
/// 迁后经真实 HTTP POST 管道驱动（请求体即属性通道，与生产等态）；断言全部走 HTTP 面：
/// 自增 Id 取写响应 data.id（写回包主键恒带，CertPfx 先例），落库值经 GET 详情面读回（详情面由服务端按主键加载，
/// 回包为字段集合投影），清理用键实体 Delete。原「SetItem 预置 stale Extends」形态为反射面专属
/// （HTTP 请求体天然无陈值槽），随之退役。
/// </summary>
[Collection(WebApiCollection.Name)]
public class McpServerApiBindingTests
{
    private readonly ApiClient _api;

    public McpServerApiBindingTests(WebApiTestFactory factory, ITestOutputHelper output) =>
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

    /// <summary>GET 详情面读回（服务端按主键加载，回包=字段集合投影）。</summary>
    private async Task<JsonElement> DetailAsync(Int32 id)
    {
        var api = await _api.GetJsonAsync($"Platform/McpServer/{id}");
        Assert.Equal(JsonValueKind.Object, api.Data.ValueKind);
        return api.Data;
    }

    [Fact(DisplayName = "F-PLT-1 回归：POST 全字段绑定落库——提交值经详情面读回（修复前=全默认值 SQL 错）")]
    public async Task Insert_Should_Persist_AllBoundFields()
    {
        var name = "fplt1-dual-" + Guid.NewGuid().ToString("N")[..8];
        var id = 0;
        try
        {
            var api = await _api.PostApiAsync<JsonElement>("Platform/McpServer", new
            {
                name,
                url = "http://127.0.0.1:9103",
                enable = true,
                toolCount = 2,
                remark = "F-PLT-1 dual-channel",
            });
            Assert.Equal(0, api.Code);
            id = RowId(api.Data);

            var detail = await DetailAsync(id);

            // 详情面（DetailFields∪EditFormFields 投影）读回：提交值已持久化（修复前=全默认值 + SQL 错 500）
            Assert.Equal(name, detail.GetProperty("name").GetString());
            Assert.Equal("http://127.0.0.1:9103", detail.GetProperty("url").GetString());
            Assert.Equal("F-PLT-1 dual-channel", detail.GetProperty("remark").GetString());
        }
        finally
        {
            if (id > 0) McpServer.FindById(id)?.Delete();
        }
    }

    [Fact(DisplayName = "F-PLT-1 对照：字段齐全的正常实体 Insert 不受重建逻辑影响")]
    public async Task Insert_Should_PassThrough_ProperEntity()
    {
        var name = "fplt1-ctl-" + Guid.NewGuid().ToString("N")[..8];
        var id = 0;
        try
        {
            var api = await _api.PostApiAsync<JsonElement>("Platform/McpServer", new
            {
                name,
                url = "http://127.0.0.1:9102",
                enable = true,
                toolCount = 1,
                remark = "F-PLT-1 control",
            });
            Assert.Equal(0, api.Code);
            id = RowId(api.Data);

            var detail = await DetailAsync(id);

            Assert.Equal(name, detail.GetProperty("name").GetString());
            Assert.Equal("http://127.0.0.1:9102", detail.GetProperty("url").GetString());
        }
        finally
        {
            if (id > 0) McpServer.FindById(id)?.Delete();
        }
    }

    [Fact(DisplayName = "F-PLT-1 回归：空实体 POST 必须被业务 Valid 拒绝，不允许静默成功")]
    public async Task Insert_EmptyEntity_Should_Be_Rejected()
    {
        var resp = await _api.PostAsJsonAsync("Platform/McpServer", new { });
        var raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return; // HTTP 层拒绝=未被静默接受

        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (envelope == null)
            Assert.Fail($"空实体 POST 响应无法解析为信封：{raw}");

        Assert.True(envelope.Code != 0, $"空实体 POST 应被业务 Valid 拒绝，实际 Code=0：{raw}");
    }
}
