using System;

namespace Stardust.Models;

/// <summary>星尘包信息</summary>
public class PackageInfo
{
    /// <summary>完整包名。项目名/应用名，如 NewLife/StarServer</summary>
    public String Name { get; set; } = null!;

    /// <summary>项目名。如 NewLife</summary>
    public String Publisher { get; set; } = null!;

    /// <summary>应用短名。如 StarServer</summary>
    public String ShortName { get; set; } = null!;

    /// <summary>显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>分类</summary>
    public String? Category { get; set; }

    /// <summary>说明</summary>
    public String? Description { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>当前版本。管理员在发布中心显式设置，star 拉取此版本</summary>
    public String? Version { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>星尘包版本信息</summary>
public class PackageVersionInfo
{
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>完整包名</summary>
    public String Name { get; set; } = null!;

    /// <summary>版本</summary>
    public String Version { get; set; } = null!;

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>目标操作系统</summary>
    public OSKind OS { get; set; }

    /// <summary>目标架构</summary>
    public CpuArch Arch { get; set; }

    /// <summary>目标框架</summary>
    public String? TargetFramework { get; set; }

    /// <summary>文件大小</summary>
    public Int64 Size { get; set; }

    /// <summary>文件哈希。MD5</summary>
    public String? Hash { get; set; }

    /// <summary>下载地址。已附加短时令牌</summary>
    public String? Url { get; set; }

    /// <summary>服务端合成的单应用清单</summary>
    public String? Manifest { get; set; }

    /// <summary>应用数量</summary>
    public Int32 AppCount { get; set; }

    /// <summary>发布时间</summary>
    public DateTime PublishTime { get; set; }
}
