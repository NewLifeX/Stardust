using System.Text.Json;
using NewLife.Cube;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>
/// B1 节点命令下发（POST /Nodes/NodeCommand/Send）契约测试（CARD-NODE-06 定稿 §3.0 载体）。
/// TDD 先红：Send 路由未实现时全部用例 404（红），实现后逐条转绿。
/// 错误载体：HTTP 200 + 信封 Code!=0（Cube 仓库惯例：校验类 Code=400，投递失败 Code=500），
/// 前端 api-core 拦截器对 Code!=0 拒绝并给出可见提示（lead 裁定 2026-11-09 00:50）。
/// </summary>
[Collection(WebApiCollection.Name)]
public class NodeCommandSendTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly ApiClient _api;

    public NodeCommandSendTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    /// <summary>POST body {nodeId,command}，断言 HTTP 200 + 信封 Code=400 + 消息非空。</summary>
    private async Task<ApiResponse<JsonElement>> SendAndExpectBadRequestAsync(Object payload)
    {
        var resp = await _api.PostAsJsonAsync("Nodes/NodeCommand/Send", payload);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.True(resp.IsSuccessStatusCode, $"Send 应答非 2xx：{(Int32)resp.StatusCode} {raw}");

        var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, _json);
        Assert.NotNull(api);
        Assert.True(api!.Code == 400, $"信封 Code 应为 400，实际 {api.Code}：{raw}");
        Assert.False(String.IsNullOrEmpty(api.Message), "错误信封必须携带可见提示消息");

        return api;
    }

    [Fact]
    public async Task Send_NodeIdInvalid_Returns400()
    {
        await SendAndExpectBadRequestAsync(new { nodeId = 0, command = "restart" });
        await SendAndExpectBadRequestAsync(new { nodeId = -1, command = "restart" });
    }

    [Fact]
    public async Task Send_CommandEmpty_Returns400()
    {
        await SendAndExpectBadRequestAsync(new { nodeId = 1, command = "" });
        await SendAndExpectBadRequestAsync(new { nodeId = 1, command = (String?)null });
    }

    [Fact]
    public async Task Send_NodeNotFound_Returns400()
    {
        // 临时 SQLite 库节点极少，该编号必然不存在
        var api = await SendAndExpectBadRequestAsync(new { nodeId = 999_999_999, command = "restart" });

        Assert.Contains("节点不存在", api.Message);
    }

    [Fact]
    public async Task Send_WithoutToken_Returns401()
    {
        var resp = await _api.PostAsJsonAsync("Nodes/NodeCommand/Send", new { nodeId = 1, command = "restart" }, auth: false);
        var raw = await resp.Content.ReadAsStringAsync();

        // 匿名必须被拒：HTTP 401，或 200 + 信封 Code=401（两种拒绝形态之一）
        var bodyCode = 0;
        if (resp.IsSuccessStatusCode)
        {
            var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, _json);
            bodyCode = api!.Code;
        }
        var status = (Int32)resp.StatusCode;

        Assert.True(status == 401 || bodyCode == 401, $"匿名访问未被 401 拒绝：HTTP {status}，body={raw}");
    }

    [Fact]
    public async Task Send_OfflineNode_ReturnsFailureMessage()
    {
        // 临时 SQLite 造节点（数据环境铁律：仅本地栈）；宿主 StarServer 指向不可达 → 确定性失败
        var node = new Node { Name = "B1测试节点", Code = "b1-send-test-node" };
        node.Insert();

        var resp = await _api.PostAsJsonAsync("Nodes/NodeCommand/Send", new { nodeId = node.ID, command = "restart" });
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.True(resp.IsSuccessStatusCode, $"Send 应答非 2xx：{(Int32)resp.StatusCode} {raw}");
        var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, _json);
        Assert.NotNull(api);
        Assert.NotEqual(0, api!.Code);   // 失败必须非 0 码（超时/连接不可用 → 500），前端拦截器据此给可见提示
        Assert.False(String.IsNullOrEmpty(api.Message), "失败信封必须携带原因，旧壳将显示「指令下发失败：{原因}」");
    }
}
