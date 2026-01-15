namespace Stardust.Models;

/// <summary>资源下载信息</summary>
public class ResourceInfo
{
    /// <summary>资源名称</summary>
    public String? Name { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>资源地址</summary>
    public String? Url { get; set; }

    /// <summary>哈希散列</summary>
    public String? Hash { get; set; }

    /// <summary>目标路径。相对于应用工作目录</summary>
    public String? TargetPath { get; set; }

    /// <summary>解压缩。下载后是否自动解压</summary>
    public Boolean UnZip { get; set; }

    /// <summary>覆盖文件</summary>
    public String? Overwrite { get; set; }
}
