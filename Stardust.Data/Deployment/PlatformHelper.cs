using System;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Data.Deployment;

/// <summary>平台匹配工具。按节点操作系统与架构选择应用包或部署版本</summary>
public static class PlatformHelper
{
    /// <summary>获取节点的操作系统和架构</summary>
    /// <param name="node">节点</param>
    /// <returns>操作系统与架构</returns>
    public static (OSKind os, CpuArch arch) GetNodePlatform(Node? node)
    {
        if (node == null) return (OSKind.Any, CpuArch.Any);

        var os = node.OSKind switch
        {
            >= OSKinds.iOS => OSKind.iOS,
            >= OSKinds.Android => OSKind.Android,
            >= OSKinds.MacOSX => OSKind.OSX,
            // Linux 各发行版（100+）属于 glibc，必须排在 Alpine（90）之前判断，
            // 否则 Ubuntu/CentOS 等会被误判为 musl，导致多版本部署选不到 Linux 包
            >= OSKinds.Linux or OSKinds.SmartOS => OSKind.Linux,
            >= OSKinds.Alpine => OSKind.LinuxMusl,
            >= OSKinds.Win10 => OSKind.Windows,
            _ => OSKind.Any,
        };

        var arch = node.Architecture?.ToLower() switch
        {
            "x86" => CpuArch.X86,
            "x64" => CpuArch.X64,
            "arm" => CpuArch.Arm,
            "arm64" => CpuArch.Arm64,
            "loongarch64" => CpuArch.LA64,
            "riscv64" => CpuArch.RiscV64,
            "mips64" => CpuArch.Mips64,
            _ => CpuArch.Any,
        };

        return (os, arch);
    }

    /// <summary>检查版本的平台是否匹配目标节点</summary>
    /// <param name="verOS">版本操作系统</param>
    /// <param name="verArch">版本架构</param>
    /// <param name="nodeOS">节点操作系统</param>
    /// <param name="nodeArch">节点架构</param>
    /// <returns>是否匹配</returns>
    public static Boolean MatchPlatform(OSKind verOS, CpuArch verArch, OSKind nodeOS, CpuArch nodeArch)
    {
        // 版本未指定平台，匹配所有
        if (verOS == OSKind.Any && verArch == CpuArch.Any) return true;

        // 操作系统匹配：版本未指定或与节点相同
        if (verOS != OSKind.Any && verOS != nodeOS) return false;

        // 架构匹配：版本未指定或与节点相同
        if (verArch != CpuArch.Any && verArch != nodeArch) return false;

        return true;
    }
}
