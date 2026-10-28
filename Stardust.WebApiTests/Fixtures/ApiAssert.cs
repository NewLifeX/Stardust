using System.Text.Json;
using Xunit;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>跨测试复用的业务断言。集中在此，避免各域测试复制粘贴同一段字段校验。</summary>
public static class ApiAssert
{
    /// <summary>
    /// 断言实体元数据 <c>meta</c> 响应里指定区段的字段集合非空，且每一项都有非空的 Name。
    /// </summary>
    /// <remarks>
    /// Cube API 标准端点把页面字段收敛到 <c>GET {area}/{controller}/meta</c>，
    /// data 为对象，包含 <c>list</c> / <c>form</c> / <c>search</c> 区段；
    /// <c>add</c> / <c>detail</c> 与 form 完全一致时省略，消费端回退 form。
    /// </remarks>
    /// <param name="data">meta 响应的 data</param>
    /// <param name="section">字段区段：list / form / search</param>
    /// <param name="endpoint">仅用于失败信息定位</param>
    public static void MetaFields(JsonElement data, String section, String endpoint)
    {
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.True(data.TryGetProperty(section, out var fields), $"[{endpoint}] meta 响应缺少 {section} 区段：{data}");

        FieldArray(fields, section, endpoint);
    }

    /// <summary>断言 meta 响应里 <c>add</c> / <c>detail</c> 区的取值规则：要么省略（回退 form），要么是非空字段数组。</summary>
    /// <param name="data">meta 响应的 data</param>
    /// <param name="section">字段区段：add / detail</param>
    /// <param name="endpoint">仅用于失败信息定位</param>
    public static void MetaOptionalFields(JsonElement data, String section, String endpoint)
    {
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        if (!data.TryGetProperty(section, out var fields)) return;
        if (fields.ValueKind == JsonValueKind.Null) return;

        FieldArray(fields, section, endpoint);
    }

    private static void FieldArray(JsonElement fields, String section, String endpoint)
    {
        Assert.Equal(JsonValueKind.Array, fields.ValueKind);
        Assert.True(fields.GetArrayLength() > 0, $"[{endpoint}] {section} 返回空字段集合");

        foreach (var field in fields.EnumerateArray())
            AssertFieldHasName(field, section, endpoint);
    }

    // 响应按 Web 约定序列化为小驼峰（name），而 JsonElement 的属性查找大小写敏感，故自行遍历匹配
    private static void AssertFieldHasName(JsonElement field, String section, String endpoint)
    {
        foreach (var prop in field.EnumerateObject())
        {
            if (!String.Equals(prop.Name, "Name", StringComparison.OrdinalIgnoreCase)) continue;

            Assert.False(String.IsNullOrWhiteSpace(prop.Value.GetString()), $"[{endpoint}] {section} 字段项 Name 为空：{field}");
            return;
        }

        Assert.Fail($"[{endpoint}] {section} 字段项缺少 Name：{field}");
    }
}
