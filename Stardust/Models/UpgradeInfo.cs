namespace Stardust.Models;

/// <summary>更新响应</summary>
public class UpgradeInfo
{
    /// <summary>版本号</summary>
    public String? Version { get; set; }

    /// <summary>更新源，Url地址</summary>
    public String? Source { get; set; }

    /// <summary>文件哈希</summary>
    public String? FileHash { get; set; }

    /// <summary>更新前要执行的命令。解压缩后，在解压缩目录执行</summary>
    public String? Preinstall { get; set; }

    /// <summary>更新后要执行的命令</summary>
    public String? Executor { get; set; }

    /// <summary>是否强制更新，覆盖文件后即刻重启应用，不需要用户同意</summary>
    public Boolean Force { get; set; }

    /// <summary>描述</summary>
    public String? Description { get; set; }
}