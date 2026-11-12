using System.Text.Json;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>CARD-MON-12 裁定 1：跟踪项类别列表只读 action。
/// TDD 先红（action 未实现时 404）→ 后端补 `GET /api/Monitors/TraceItem/GetKinds`（Cube 信封，
/// data=IDictionary&lt;String,String&gt;，来自 跟踪项.Biz.cs GetKinds()=FieldCache FindAllName）→ 绿。</summary>
[Collection(WebApiCollection.Name)]
public class TraceItemGetKindsTests
{
    private readonly ApiClient _api;

    public TraceItemGetKindsTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    [Fact]
    public async Task GetKinds_ReturnsDictionaryEnvelope()
    {
        // WebEntityController 只读 action：Json(0, null, TraceItem.GetKinds())，登录可见
        var api = await _api.GetJsonAsync("Monitors/TraceItem/GetKinds");

        Assert.Equal(JsonValueKind.Object, api.Data.ValueKind);
        foreach (var prop in api.Data.EnumerateObject())
        {
            Assert.False(String.IsNullOrEmpty(prop.Name), $"GetKinds 词典出现空键：{api.Data}");
            Assert.Equal(JsonValueKind.String, prop.Value.ValueKind);
        }
    }
}
