using System;
using Stardust.Deployment;

namespace Stardust.Models;

/// <summary>星尘包操作结果</summary>
public class StarPackageOperationResult
{
    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>消息</summary>
    public String? Message { get; set; }

    /// <summary>包名</summary>
    public String? Name { get; set; }
}

/// <summary>星尘包下载结果。返回本地 zip 路径，由调用方决定安装位置</summary>
public class StarPackageFetchResult
{
    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>消息</summary>
    public String? Message { get; set; }

    /// <summary>包名</summary>
    public String? Name { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>本地 zip 路径</summary>
    public String? ZipPath { get; set; }

    /// <summary>文件哈希。MD5</summary>
    public String? Hash { get; set; }

    /// <summary>包清单。star.json 快照</summary>
    public String? Manifest { get; set; }
}

/// <summary>星尘包安装结果</summary>
public class StarPackageInstallResult
{
    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>消息</summary>
    public String? Message { get; set; }

    /// <summary>包名</summary>
    public String? Name { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>安装后的应用服务</summary>
    public ServiceInfo[]? Services { get; set; }
}

/// <summary>星尘包列表结果</summary>
public class StarPackageListResult
{
    /// <summary>已安装的包</summary>
    public StarPackageInfo[]? Packages { get; set; }

    /// <summary>应用服务</summary>
    public ServiceInfo[]? Services { get; set; }

    /// <summary>运行中的进程</summary>
    public StarPackageProcessInfo[]? Running { get; set; }
}

/// <summary>星尘包运行进程</summary>
public class StarPackageProcessInfo
{
    /// <summary>服务名</summary>
    public String Name { get; set; } = null!;

    /// <summary>进程编号</summary>
    public Int32 ProcessId { get; set; }
}
