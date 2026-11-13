using System.Text.Json;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>
/// AppService 列表元数据三链接列（ServiceName/NodeName/Client）的
/// displayName / header 承载断言。迁移页前端按「displayName 含模板串 → 回退 header → 回退字段名」
/// 兜底渲染列头，本用例锁定后端 meta 通道的实名输出（不允许字面 {ServiceName} 模板串出线）。
/// </summary>
[Collection(WebApiCollection.Name)]
public class AppServiceMetaTests
{
    private readonly ApiClient _api;
    private readonly ITestOutputHelper _output;

    public AppServiceMetaTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _api = new(factory, output);
        _output = output;
    }

    public static TheoryData<String> LinkColumns => new()
    {
        "ServiceName",
        "NodeName",
        "Client",
    };

    [Theory]
    [MemberData(nameof(LinkColumns))]
    public async Task Meta_LinkColumns_DisplayName_IsRealNameNotTemplate(String name)
    {
        var api = await _api.GetJsonAsync("Registry/AppService/meta");
        var field = FindField(api.Data, name);
        _output.WriteLine($"[{name}] displayName 断言原始字段：{field}");

        var displayName = GetString(field, "displayName");
        Assert.True(!String.IsNullOrEmpty(displayName), $"[{name}] meta.list displayName 缺失或为空：{field}");
        Assert.False(displayName.Contains('{') || displayName.Contains('}'), $"[{name}] displayName 是字面模板串：{displayName}（原始字段：{field}）");
    }

    [Theory]
    [MemberData(nameof(LinkColumns))]
    public async Task Meta_LinkColumns_Header_IsRealNameNotBlank(String name)
    {
        var api = await _api.GetJsonAsync("Registry/AppService/meta");
        var field = FindField(api.Data, name);
        _output.WriteLine($"[{name}] header 断言原始字段：{field}");

        var header = GetString(field, "header");
        Assert.True(!String.IsNullOrEmpty(header), $"[{name}] meta.list header 缺失或为空（原始字段：{field}）");
        Assert.False(header.Contains('{') || header.Contains('}'), $"[{name}] header 是字面模板串：{header}");
    }

    private static JsonElement FindField(JsonElement data, String name)
    {
        Assert.True(data.TryGetProperty("list", out var list), $"meta 缺少 list 区段：{data}");
        foreach (var field in list.EnumerateArray())
        {
            foreach (var prop in field.EnumerateObject())
            {
                if (String.Equals(prop.Name, "name", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(prop.Value.GetString(), name, StringComparison.OrdinalIgnoreCase))
                    return field;
            }
        }

        Assert.Fail($"meta.list 未找到字段 {name}：{list}");
        return default;
    }

    private static String? GetString(JsonElement field, String property)
    {
        foreach (var prop in field.EnumerateObject())
        {
            if (String.Equals(prop.Name, property, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                return prop.Value.GetString();
        }

        return null;
    }
}
