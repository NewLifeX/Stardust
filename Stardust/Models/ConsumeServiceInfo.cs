namespace Stardust.Models;

/// <summary>消费服务信息</summary>
public class ConsumeServiceInfo
{
    /// <summary>服务名</summary>
    public String ServiceName { get; set; } = null!;

    /// <summary>客户端。IP加进程</summary>
    public String? ClientId { get; set; }

    /// <summary>最低版本</summary>
    public String? MinVersion { get; set; }

    /// <summary>标签。带有指定特性，逗号分隔</summary>
    public String? Tag { get; set; }
}