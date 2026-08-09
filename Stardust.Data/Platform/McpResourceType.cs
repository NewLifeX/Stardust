#nullable enable
using System;
using System.Collections.Generic;

namespace Stardust.Data.Platform;

/// <summary>MCP资源类型枚举。
/// 作为资源类型的唯一来源，避免多处硬编码字符串导致存储（大驼峰）与协议（小写）大小写不一致。
/// 存储列 ResourceType 使用大驼峰（枚举成员名，如 "Project"）；
/// 协议/表单/工具Schema使用小写（ToWireName，如 "project"）。</summary>
public enum McpResourceType
{
    /// <summary>项目（GalaxyProject）</summary>
    Project,
    /// <summary>节点（Node）</summary>
    Node,
    /// <summary>应用（App）</summary>
    App,
    /// <summary>部署集（AppDeploy）</summary>
    Deploy,
    /// <summary>流水线（AppPipeline）</summary>
    Pipeline,
    /// <summary>服务（AppService）</summary>
    Service,
}

/// <summary>McpResourceType 扩展方法</summary>
public static class McpResourceTypeExtensions
{
    /// <summary>存储名（大驼峰，等于枚举成员名，如 "Project"）</summary>
    public static String ToStorageName(this McpResourceType type) => type.ToString();

    /// <summary>协议/表单名（小写，如 "project"）</summary>
    public static String ToWireName(this McpResourceType type) => type.ToString().ToLowerInvariant();

    /// <summary>从小写协议名解析为枚举（忽略大小写）</summary>
    public static Boolean TryParseWire(String wire, out McpResourceType type)
        => Enum.TryParse(wire, ignoreCase: true, out type);

    /// <summary>直接资源（作为 Token 授权存储的类型）：Project/Node/App</summary>
    public static readonly IReadOnlyList<McpResourceType> DirectTypes = new[]
    {
        McpResourceType.Project,
        McpResourceType.Node,
        McpResourceType.App,
    };

    /// <summary>全部资源类型（用于工具Schema枚举）</summary>
    public static readonly IReadOnlyList<McpResourceType> AllTypes =
        (McpResourceType[])Enum.GetValues(typeof(McpResourceType));

    /// <summary>间接资源对应的实体名（用于反查 ProjectId）。直接资源（Project/Node/App）返回 null</summary>
    private static readonly Dictionary<McpResourceType, String> _indirectEntities = new()
    {
        [McpResourceType.Deploy] = "AppDeploy",
        [McpResourceType.Pipeline] = "AppPipeline",
        [McpResourceType.Service] = "AppService",
    };

    /// <summary>获取间接资源对应的实体名（AppDeploy/AppPipeline/AppService）；直接资源返回 null</summary>
    public static String? ToIndirectEntityName(this McpResourceType type)
        => _indirectEntities.TryGetValue(type, out var name) ? name : null;

    /// <summary>是否为直接资源（Token 授权存储类型）</summary>
    public static Boolean IsDirect(this McpResourceType type) => !_indirectEntities.ContainsKey(type);
}
