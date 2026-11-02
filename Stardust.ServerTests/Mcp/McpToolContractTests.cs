using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using NewLife;
using Stardust.Mcp;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// MCP 工具契约特征测试。覆盖本轮修复：
/// <list type="bullet">
///   <item>D-3: 所有工具 InputSchema 有实质 properties（不是空 object）</item>
///   <item>风险粒度: deploy_node_operate / app_process 为 L1（不是 L2）；node_command 为 L2</item>
///   <item>S4: config_publish 工具已移除，tools/list 不含该名</item>
///   <item>S2: config_get 描述提到脱敏；config_set 描述不引用旧方案 config_publish</item>
/// </list>
/// 纯元数据测试，不连数据库。
/// </summary>
public class McpToolContractTests
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

    private static IMcpTool FindTool(String name)
    {
        var t = GetToolTypes().FirstOrDefault(x => Create(x).Name == name);
        Assert.NotNull(t);
        return Create(t!);
    }

    /// <summary>真正无需参数的工具：whoami/project_list/audit_list/now_changes</summary>
    private static readonly HashSet<String> NoParamTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "stardust_token_whoami",
        "stardust_project_list",
        "stardust_audit_list",
        "stardust_now_changes",
    };

    [Fact(DisplayName = "D-3: 所有带参工具 InputSchema 有 properties")]
    public void AllTools_InputSchema_HasProperties()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            if (NoParamTools.Contains(tool.Name)) continue;

            var schema = tool.InputSchema;
            if (schema.ValueKind == JsonValueKind.Undefined || schema.ValueKind == JsonValueKind.Null)
            {
                bad.Add($"{tool.Name}: InputSchema 为默认空");
                continue;
            }

            // 解析 properties
            if (!schema.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object || !props.EnumerateObject().Any())
            {
                bad.Add($"{tool.Name}: InputSchema 缺少 properties（空 object）");
            }
        }

        Assert.True(bad.Count == 0, $"以下工具 InputSchema 不完整：{Environment.NewLine}{String.Join(Environment.NewLine, bad)}");
    }

    [Fact(DisplayName = "风险粒度: deploy_node_operate 为 L1（可逆写，可回滚）")]
    public void DeployNodeOperate_RiskIsL1()
    {
        var tool = FindTool("stardust_deploy_node_operate");
        Assert.Equal(McpRiskLevel.L1, tool.Risk);
    }

    [Fact(DisplayName = "风险粒度: app_process 为 L1（可逆写）")]
    public void AppProcess_RiskIsL1()
    {
        var tool = FindTool("stardust_app_process");
        Assert.Equal(McpRiskLevel.L1, tool.Risk);
    }

    [Fact(DisplayName = "风险粒度: node_command 为 L2（高危，需 confirm）")]
    public void NodeCommand_RiskIsL2()
    {
        var tool = FindTool("stardust_node_command");
        Assert.Equal(McpRiskLevel.L2, tool.Risk);
    }

    [Fact(DisplayName = "风险粒度: app_command 为 L2（高危）")]
    public void AppCommand_RiskIsL2()
    {
        var tool = FindTool("stardust_app_command");
        Assert.Equal(McpRiskLevel.L2, tool.Risk);
    }

    [Fact(DisplayName = "S4: tools/list 不含 stardust_config_publish")]
    public void ToolsList_DoesNotContainConfigPublish()
    {
        var names = GetToolTypes().Select(t => Create(t).Name).ToHashSet();
        Assert.DoesNotContain("stardust_config_publish", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "S4: 确认 config 域只有 List/Get/Set/Rollback 四个工具")]
    public void ConfigTools_HasOnlyFourTools()
    {
        var names = GetToolTypes()
            .Select(t => Create(t).Name)
            .Where(n => n.StartsWith("stardust_config_"))
            .OrderBy(n => n)
            .ToList();

        Assert.Contains("stardust_config_list", names);
        Assert.Contains("stardust_config_get", names);
        Assert.Contains("stardust_config_set", names);
        Assert.Contains("stardust_config_rollback", names);
        Assert.Equal(4, names.Count);
    }

    [Fact(DisplayName = "S2: config_get 描述提到自动脱敏")]
    public void ConfigGet_Description_MentionsMasking()
    {
        var tool = FindTool("stardust_config_get");
        var desc = tool.Description;
        Assert.True(
            desc.Contains("mask", StringComparison.OrdinalIgnoreCase) || desc.Contains("***", StringComparison.Ordinal),
            $"config_get 描述未提及脱敏：{desc}");
    }

    [Fact(DisplayName = "S4: config_set 描述不引用已移除的 config_publish")]
    public void ConfigSet_Description_NoPublishReference()
    {
        var tool = FindTool("stardust_config_set");
        Assert.DoesNotContain("config_publish", tool.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "S2: config_set 描述不引用已废弃的 config:secret scope")]
    public void ConfigSet_Description_NoSecretScope()
    {
        var tool = FindTool("stardust_config_set");
        Assert.DoesNotContain("config:secret", tool.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "每个工具 Name 匹配 stardust_域_动作 规范")]
    public void AllTools_Name_FollowsConvention()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            if (!tool.Name.StartsWith("stardust_"))
                bad.Add(tool.Name);
        }
        Assert.True(bad.Count == 0, $"以下工具名不规范：{String.Join(", ", bad)}");
    }

    [Fact(DisplayName = "每个工具 ScopeDomain 非空")]
    public void AllTools_ScopeDomain_NonEmpty()
    {
        var bad = new List<String>();
        foreach (var type in GetToolTypes())
        {
            var tool = Create(type);
            // whoami 不要求 scope，但 ScopeDomain 仍应有值
            if (tool.ScopeDomain.IsNullOrEmpty())
                bad.Add(tool.Name);
        }
        Assert.True(bad.Count == 0, $"以下工具 ScopeDomain 为空：{String.Join(", ", bad)}");
    }
}
