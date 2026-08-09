extern alias web;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using NewLife;
using web::Stardust.Web.Mcp;
using Xunit;
using Stardust.Data.Platform;

namespace ServerTest.Mcp;

/// <summary>MCP Action Schema 单元测试。通过反射扫描所有 IMcpAction 实现，
/// 验证 27 个 action 的元数据完整性和 RequiredResource 声明正确性</summary>
public class McpActionSchemaTests
{
    /// <summary>获取所有 IMcpAction 实现类型</summary>
    private static List<Type> GetActionTypes()
    {
        var assembly = typeof(IMcpAction).Assembly;
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IMcpAction).IsAssignableFrom(t))
            .ToList();
    }

    /// <summary>使用 FormatterServices 创建未初始化实例（绕过构造函数依赖），
    /// 因为 action 的 Name/Description/Module/InputSchema/RequiredResource 都是硬编码返回值，不依赖实例字段</summary>
    private static IMcpAction CreateActionInstance(Type type)
    {
        var obj = FormatterServices.GetUninitializedObject(type);
        return (IMcpAction)obj;
    }

    [Fact]
    public void Total_ActionCount_Is27()
    {
        var types = GetActionTypes();

        Assert.True(types.Count >= 27, $"Expected at least 27 actions, found {types.Count}: {String.Join(", ", types.Select(t => t.Name))}");
    }

    [Fact]
    public void AllActions_HaveNonEmptyName()
    {
        var types = GetActionTypes();
        var emptyNames = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            if (action.Name.IsNullOrEmpty()) emptyNames.Add(type.Name);
        }

        Assert.Empty(emptyNames);
    }

    [Fact]
    public void AllActions_HaveNonEmptyDescription()
    {
        var types = GetActionTypes();
        var emptyDescs = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            if (action.Description.IsNullOrEmpty()) emptyDescs.Add(action.Name ?? type.Name);
        }

        Assert.Empty(emptyDescs);
    }

    [Fact]
    public void AllActions_HaveValidModule()
    {
        var validModules = new HashSet<McpModuleType>(McpModuleTypeExtensions.AllModules);
        var types = GetActionTypes();
        var invalidModules = new List<(String Action, String Module)>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            if (!validModules.Contains(action.Module))
            {
                invalidModules.Add((action.Name ?? type.Name, action.Module.ToWireName()));
            }
        }

        Assert.Empty(invalidModules);
    }

    [Fact]
    public void AllActionNames_AreSnakeCase()
    {
        var types = GetActionTypes();
        var invalidNames = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            var name = action.Name;
            // snake_case: 只允许小写字母、数字、下划线
            if (name.IsNullOrEmpty() || !name.All(c => Char.IsLower(c) || Char.IsDigit(c) || c == '_'))
            {
                invalidNames.Add(name ?? type.Name);
            }
        }

        Assert.Empty(invalidNames);
    }

    [Fact]
    public void AllActionNames_AreUnique()
    {
        var types = GetActionTypes();
        var names = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            names.Add(action.Name);
        }

        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicates);
    }

    [Theory]
    // System
    [InlineData("get_audit_log", "system")]
    // Node
    [InlineData("node_list_online", "node")]
    [InlineData("node_search", "node")]
    [InlineData("node_send_command", "node")]
    [InlineData("node_upgrade", "node")]
    // App
    [InlineData("app_list_online", "app")]
    [InlineData("app_send_command", "app")]
    [InlineData("app_resolve_service", "app")]
    [InlineData("app_search_service", "app")]
    [InlineData("app_restart", "app")]
    [InlineData("app_stop", "app")]
    [InlineData("app_start", "app")]
    // Config
    [InlineData("config_get", "config")]
    [InlineData("config_set", "config")]
    // Deploy
    [InlineData("deploy_list", "deploy")]
    [InlineData("deploy_list_versions", "deploy")]
    [InlineData("deploy_list_history", "deploy")]
    [InlineData("deploy_list_nodes", "deploy")]
    [InlineData("deploy_compile", "deploy")]
    [InlineData("deploy_install", "deploy")]
    [InlineData("pipeline_trigger", "deploy")]
    [InlineData("pipeline_get_run", "deploy")]
    [InlineData("pipeline_cancel", "deploy")]
    // Gateway
    [InlineData("gateway_list_routes", "gateway")]
    [InlineData("gateway_list_clusters", "gateway")]
    // Monitor
    [InlineData("monitor_trace_search", "monitor")]
    [InlineData("monitor_alarm_list", "monitor")]
    public void ExpectedAction_Exists_WithCorrectModule(String expectedName, String expectedModule)
    {
        var types = GetActionTypes();
        var found = false;

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            if (action.Name == expectedName)
            {
                Assert.Equal(expectedModule, action.Module.ToWireName());
                found = true;
                break;
            }
        }

        Assert.True(found, $"Action '{expectedName}' not found. Available: {String.Join(", ", types.Select(t => CreateActionInstance(t).Name))}");
    }

    [Fact]
    public void RequiredResource_HasValidType_WhenNotNull()
    {
        var validTypes = new HashSet<String>
        {
            McpResourceType.Project.ToWireName(),
            McpResourceType.Node.ToWireName(),
            McpResourceType.App.ToWireName(),
            McpResourceType.Deploy.ToWireName(),
            McpResourceType.Pipeline.ToWireName(),
        };
        var types = GetActionTypes();
        var invalid = new List<(String Action, String Type)>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            var req = action.RequiredResource;
            if (req != null && !req.Type.IsNullOrEmpty() && !validTypes.Contains(req.Type))
            {
                invalid.Add((action.Name, req.Type));
            }
        }

        Assert.Empty(invalid);
    }

    [Fact]
    public void RequiredResource_HasField_WhenTypeSpecified()
    {
        var types = GetActionTypes();
        var invalid = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            var req = action.RequiredResource;
            if (req != null && !req.Type.IsNullOrEmpty() && req.Field.IsNullOrEmpty())
            {
                invalid.Add(action.Name);
            }
        }

        Assert.Empty(invalid);
    }

    [Fact]
    public void InputSchema_IsValidJson_WhenNonDefault()
    {
        var types = GetActionTypes();
        var invalid = new List<String>();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            var schema = action.InputSchema;

            // default(JsonElement) 的 ValueKind 是 Undefined
            if (schema.ValueKind == JsonValueKind.Undefined) continue;

            // 非默认值应该是 Object 类型
            if (schema.ValueKind != JsonValueKind.Object)
            {
                invalid.Add(action.Name);
                continue;
            }

            // 如果是 Object，应该有 type 属性
            if (!schema.TryGetProperty("type", out _))
            {
                invalid.Add(action.Name);
            }
        }

        Assert.Empty(invalid);
    }

    /// <summary>列出所有 action 及其元数据（用于调试和文档生成）</summary>
    [Fact(Skip = "调试用：列出所有 action 元数据")]
    public void ListAllActions()
    {
        var types = GetActionTypes();

        foreach (var type in types)
        {
            var action = CreateActionInstance(type);
            var req = action.RequiredResource;
            var reqStr = req != null
                ? $"type={req.Type}, field={req.Field}, indirect={req.Indirect}, optional={req.Optional}"
                : "null";

            Console.WriteLine($"[{action.Module.ToWireName()}] {action.Name}: {action.Description} | RequiredResource: {reqStr}");
        }

        Assert.NotEmpty(types);
    }
}
