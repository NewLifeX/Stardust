namespace Stardust.Models;

/// <summary>应用发布信息</summary>
public class DeployInfo
{
    /// <summary>应用名称</summary>
    public String Name { get; set; }

    /// <summary>应用版本</summary>
    public String Version { get; set; }

    /// <summary>应用包地址</summary>
    public String Url { get; set; }

    /// <summary>哈希散列</summary>
    public String Hash { get; set; }

    /// <summary>应用服务</summary>
    public ServiceInfo Service { get; set; }
}