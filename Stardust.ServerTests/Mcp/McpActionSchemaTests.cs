using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using NewLife;
using Stardust.Mcp;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>MCP 工具契约测试。反射扫描 Stardust.Mcp 中的 IMcpTool 实现，
/// 验证元数据完整性（名称、描述、域、风险级别、Schema）。旧版 27 个 action 的清单断言已随重设计移除。</summary>
public class McpActionSchemaTests
{
    /// <summary>获取所有 IMcpTool 实现类型</summary>
    private static List<Type> GetToolTypes()
    {
        var assembly = typeof(IMcpTool).Assembly;
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IMcpTool).IsAssignableFrom(t))
            .ToList();
    }

    /// <summary>创建未初始化实例（元数据均为硬编码返回值，不依赖实例字段）</summary>
    private static IMcpTool Create(Type type) => (IMcpTool)FormatterServices.GetUninitializedObject(type);

    [Fact]
    public void Tools_AtLeastWhoAmI()
    {
        var types = GetToolTypes();
        Assert.Contains(types, t => Create(t).Name == "stardust_token_whoami");
    }

    [Fact]
    public void AllTools_HaveNonEmptyNameAndDescription()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            if (tool.Name.IsNullOrEmpty() || tool.Description.IsNullOrEmpty()) bad.Add(type.Name);
        }
        Assert.Empty(bad);
    }

    [Fact]
    public void AllTools_HaveScopeDomain()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            if (tool.ScopeDomain.IsNullOrEmpty()) bad.Add(tool.Name);
        }
        Assert.Empty(bad);
    }

    [Fact]
    public void AllToolNames_AreSnakeCaseAndUnique()
    {
        var names = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var name = Create(type).Name;
            Assert.Matches("^[a-z0-9_]+$", name);
            names.Add(name);
        }
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void WhoAmI_IsZeroScopeReadOnly()
    {
        var tool = GetToolTypes().Select(Create).First(t => t.Name == "stardust_token_whoami");
        Assert.False(tool.RequireScope);
        Assert.Equal(McpRiskLevel.L0, tool.Risk);
        Assert.Equal("platform", tool.ScopeDomain);
    }

    [Fact]
    public void InputSchema_IsObjectWithType_WhenPresent()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            var schema = tool.InputSchema;
            if (schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) continue;
            if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out _))
                bad.Add(tool.Name);
        }
        Assert.Empty(bad);
    }
}
