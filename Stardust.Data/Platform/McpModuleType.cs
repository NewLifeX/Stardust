#nullable enable
using System;
using System.Collections.Generic;

namespace Stardust.Data.Platform;

/// <summary>MCP动作所属模块枚举。
/// 作为模块的唯一来源，避免各处硬编码小写字符串（node/app/config/deploy/gateway/monitor/system）。
/// 协议/展示统一使用小写（ToWireName，如 "deploy"）。</summary>
public enum McpModuleType
{
    /// <summary>节点模块（Node）</summary>
    Node,
    /// <summary>应用模块（App）</summary>
    App,
    /// <summary>配置模块（Config）</summary>
    Config,
    /// <summary>部署模块（Deploy）</summary>
    Deploy,
    /// <summary>网关模块（Gateway）</summary>
    Gateway,
    /// <summary>监控模块（Monitor）</summary>
    Monitor,
    /// <summary>系统模块（System）</summary>
    System,
}

/// <summary>McpModuleType 扩展方法</summary>
public static class McpModuleTypeExtensions
{
    /// <summary>协议/展示名（小写，如 "deploy"）</summary>
    public static String ToWireName(this McpModuleType module) => module.ToString().ToLowerInvariant();

    /// <summary>从小写协议名解析为枚举（忽略大小写）</summary>
    public static Boolean TryParseWire(String wire, out McpModuleType module)
        => Enum.TryParse(wire, ignoreCase: true, out module);

    /// <summary>全部模块（用于文档/校验）</summary>
    public static readonly IReadOnlyList<McpModuleType> AllModules =
        (McpModuleType[])Enum.GetValues(typeof(McpModuleType));
}
