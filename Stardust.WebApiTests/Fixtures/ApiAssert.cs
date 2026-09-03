using System.Text.Json;
using Xunit;

namespace Stardust.WebApiTests.Fixtures;

/// <summary>跨测试复用的业务断言。集中在此，避免各域测试复制粘贴同一段字段校验。</summary>
public static class ApiAssert
{
    /// <summary>
    /// 断言 GetFields 的 data 是非空字段数组，且每一项都有非空的 Name。
    /// </summary>
    /// <param name="data">GetFields 的响应 data</param>
    /// <param name="endpoint">仅用于失败信息定位</param>
    public static void FieldList(JsonElement data, String endpoint)
    {
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0, $"[{endpoint}] GetFields 返回空字段集合");

        foreach (var field in data.EnumerateArray())
            AssertFieldHasName(field, endpoint);
    }

    // 响应按 Web 约定序列化为小驼峰（name），而 JsonElement 的属性查找大小写敏感，故自行遍历匹配
    private static void AssertFieldHasName(JsonElement field, String endpoint)
    {
        foreach (var prop in field.EnumerateObject())
        {
            if (!String.Equals(prop.Name, "Name", StringComparison.OrdinalIgnoreCase)) continue;

            Assert.False(String.IsNullOrWhiteSpace(prop.Value.GetString()), $"[{endpoint}] 字段项 Name 为空：{field}");
            return;
        }

        Assert.Fail($"[{endpoint}] 字段项缺少 Name：{field}");
    }
}
