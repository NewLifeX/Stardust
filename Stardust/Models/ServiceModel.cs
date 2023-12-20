namespace Stardust.Models;

/// <summary>服务模型</summary>
public class ServiceModel
{
    /// <summary>服务名</summary>
    public String ServiceName { get; set; } = null!;

    /// <summary>显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>客户端。IP加进程</summary>
    public String? Client { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>服务地址。本地局域网地址</summary>
    public String? Address { get; set; }

    /// <summary>外部地址。经过网关之前的外部地址</summary>
    public String? Address2 { get; set; }

    /// <summary>作用域</summary>
    public String? Scope { get; set; }

    /// <summary>标签</summary>
    public String? Tag { get; set; }

    /// <summary>权重</summary>
    public Int32 Weight { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime UpdateTime { get; set; }
}