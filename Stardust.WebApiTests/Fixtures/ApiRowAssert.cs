using System.Text.Json;
using NewLife.Cube;
using Xunit;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>
/// 列表/详情行（ApiRow 投影 JSON）脱敏断言助手：ServerTests 反射刀组测试迁 HTTP 集成式（2026-11-14 Q2 裁决）。
/// 行键=OnProject camelCase 投影（首字母小写），值语义两型：值级置 null（键在值 null）与条件哨兵（键在值=哨兵串）。
/// </summary>
public static class ApiRowAssert
{
    /// <summary>GET 列表并解析行数组：信封 data 为 ApiRow 对象数组，逐行拍平为 Ordinal 字典。</summary>
    public static async Task<List<Dictionary<String, JsonElement>>> GetRowsAsync(ApiClient api, String endpoint)
    {
        var resp = await api.GetAsync(endpoint);
        var raw = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"[{endpoint}] HTTP {(Int32)resp.StatusCode} 非成功：{raw}");

        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(envelope);
        Assert.True(envelope!.Code == 0, $"[{endpoint}] 信封 Code={envelope.Code}（Message={envelope.Message}）：{raw}");
        Assert.Equal(JsonValueKind.Array, envelope.Data.ValueKind);

        var rows = new List<Dictionary<String, JsonElement>>();
        foreach (var row in envelope.Data.EnumerateArray())
        {
            var dict = new Dictionary<String, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in row.EnumerateObject()) dict[prop.Name] = prop.Value.Clone();
            rows.Add(dict);
        }
        return rows;
    }

    /// <summary>按行内键值前缀过滤本用例造数行（键如 name/domain/deployName）。</summary>
    public static List<Dictionary<String, JsonElement>> Mine(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String prefix) =>
        rows.Where(r => r.TryGetValue(key, out var v) && (v.GetString() ?? "").StartsWith(prefix, StringComparison.Ordinal)).ToList();

    /// <summary>值级置 null 语义（task-94 惯例）：键必须在（键不删）且值必须 JSON null（明文零落行）。</summary>
    public static void Masked(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        Assert.All(rows, r => Assert.True(r.ContainsKey(key),
            $"{ctx}: 行 JSON 缺键 '{key}'（值级置 null 语义要求键不删）；实际键=[{String.Join(",", r.Keys)}]"));
        Assert.All(rows, r => Assert.True(r[key].ValueKind == JsonValueKind.Null,
            $"{ctx}: 行 JSON '{key}' 值未脱敏（期望 JSON null，实际 {r[key].ValueKind}）"));
    }

    /// <summary>详情行值级置 null 语义：键必须在且值 null。</summary>
    public static void DetailMasked(JsonElement data, String key, String ctx)
    {
        Assert.True(data.TryGetProperty(key, out var v),
            $"{ctx}: 详情 JSON 缺键 '{key}'（值级置 null 语义要求键不删）；实际键=[{String.Join(",", data.EnumerateObject().Select(p => p.Name))}]");
        Assert.True(v.ValueKind == JsonValueKind.Null, $"{ctx}: 详情 JSON '{key}' 值未脱敏（期望 JSON null，实际 {v.ValueKind}）");
    }

    /// <summary>条件哨兵语义（刀3/4/8）：键必须在且值=哨兵串（非 null——null 回填会经 PUT 静默清库）。</summary>
    public static void Sentinel(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String mask, String ctx)
    {
        Assert.All(rows, r => Assert.True(r.ContainsKey(key), $"{ctx}: 行 JSON 缺键 '{key}'"));
        Assert.All(rows, r => Assert.True(r[key].ValueKind == JsonValueKind.String && r[key].GetString() == mask,
            $"{ctx}: 行 JSON '{key}' 非掩码哨兵（期望 \"{mask}\"，实际 {r[key].ValueKind}:{r[key].GetString()}）"));
    }

    /// <summary>详情行条件哨兵语义。</summary>
    public static void DetailSentinel(JsonElement data, String key, String mask, String ctx)
    {
        Assert.True(data.TryGetProperty(key, out var v), $"{ctx}: 详情 JSON 缺键 '{key}'");
        Assert.True(v.ValueKind == JsonValueKind.String && v.GetString() == mask,
            $"{ctx}: 详情 JSON '{key}' 非掩码哨兵（期望 \"{mask}\"，实际 {v.ValueKind}:{v.GetString()}）");
    }

    /// <summary>详情行键缺席断言（新管线字段集合投影：EditFormFields/DetailFields 已裁剪的键不得出现在详情 JSON）。</summary>
    public static void DetailHidden(JsonElement data, String key, String ctx)
    {
        Assert.True(!data.TryGetProperty(key, out var v),
            $"{ctx}: 详情 JSON 不应含键 '{key}'（meta 字段集已裁剪），实际值={v}");
    }

    /// <summary>行键在场断言（行自描述回归：值不拘）。</summary>
    public static void KeyPresent(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        Assert.All(rows, r => Assert.True(r.ContainsKey(key),
            $"{ctx}: 行 JSON 缺键 '{key}'；实际键=[{String.Join(",", r.Keys)}]"));
    }

    /// <summary>行键缺席断言（新管线字段集合投影：被 meta 裁剪/TrimFields 的键不得出现在行 JSON——
    /// 比键在值 null 更强的防泄漏；2026-11-14 迁移定谳：敏感键清单与前端消费面见各测试类注释）。</summary>
    public static void KeyAbsent(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        Assert.All(rows, r =>
        {
            if (r.ContainsKey(key))
                Assert.Fail($"{ctx}: 行 JSON 不应含键 '{key}'（meta 字段集已裁剪），实际值={r[key]}");
        });
    }
}
