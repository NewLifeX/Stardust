using System.Text.Json;
using NewLife.Cube;
using NewLife.Security;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>节点唤醒接口契约测试（不依赖真实 StarAgent）</summary>
[Collection(WebApiCollection.Name)]
public class NodeWakeTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly ApiClient _api;

    public NodeWakeTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    private async Task<ApiResponse<JsonElement>> WakeAndReadAsync(Int32 id, Boolean auth = true)
    {
        var resp = await _api.GetAsync($"Nodes/Node/WakeUp?id={id}", auth);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.True(resp.IsSuccessStatusCode, $"WakeUp 应答非 2xx：{(Int32)resp.StatusCode} {raw}");

        var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, _json);
        Assert.NotNull(api);

        return api!;
    }

    [Fact]
    public async Task WakeUp_NodeNotFound_Returns400()
    {
        var api = await WakeAndReadAsync(999_999_999);

        Assert.Equal(400, api.Code);
        Assert.Contains("节点不存在", api.Message);
    }

    [Fact]
    public async Task WakeUp_NoMac_Returns400()
    {
        var node = new Node { Code = "wake-no-mac-" + Rand.NextString(8), Name = "wake-no-mac", Enable = true };
        node.Insert();
        try
        {
            var api = await WakeAndReadAsync(node.ID);

            Assert.Equal(400, api.Code);
            Assert.Contains("MAC", api.Message);
        }
        finally
        {
            try { node.Delete(); } catch { }
        }
    }

    [Fact]
    public async Task WakeUp_NoRelay_Returns400()
    {
        // 随机第三段，确保临时库里没有同网段在线节点
        var third = Rand.Next(100, 250);
        var node = new Node
        {
            Code = "wake-no-relay-" + Rand.NextString(8),
            Name = "wake-no-relay",
            Enable = true,
            MACs = "AA-BB-CC-DD-EE-FF",
            Gateway = $"10.{third}.0.1/11-22-33-44-55-66",
            IP = $"10.{third}.0.10",
        };
        node.Insert();
        try
        {
            var api = await WakeAndReadAsync(node.ID);

            Assert.Equal(400, api.Code);
            Assert.Contains("局域网", api.Message);
        }
        finally
        {
            try { node.Delete(); } catch { }
        }
    }

    [Fact]
    public void FindAllByIds_ReturnsOnlyExisting()
    {
        var node = new Node { Code = "wake-biz-" + Rand.NextString(8), Name = "wake-biz", Enable = true };
        node.Insert();
        try
        {
            var list = Node.FindAllByIds([node.ID, 0, -1]);

            var found = Assert.Single(list);
            Assert.Equal(node.ID, found.ID);
        }
        finally
        {
            try { node.Delete(); } catch { }
        }
    }

    [Fact]
    public async Task WakeUp_WithoutToken_Returns401()
    {
        var resp = await _api.GetAsync("Nodes/Node/WakeUp?id=1", auth: false);
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
}
