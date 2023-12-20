namespace Stardust.Models;

/// <summary>应用发布信息</summary>
public class DeployInfo
{
    /// <summary>应用名称</summary>
    public String? Name { get; set; }

    /// <summary>应用版本</summary>
    public String? Version { get; set; }

    /// <summary>应用包地址</summary>
    public String? Url { get; set; }

    /// <summary>哈希散列</summary>
    public String? Hash { get; set; }

    /// <summary>覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝</summary>
    public String? Overwrite { get; set; }

    /// <summary>应用服务</summary>
    public ServiceInfo? Service { get; set; }
}