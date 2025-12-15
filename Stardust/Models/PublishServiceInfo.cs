namespace Stardust.Models;

/// <summary>生产服务信息</summary>
public class PublishServiceInfo
{
    /// <summary>服务名</summary>
    public String ServiceName { get; set; } = null!;

    /// <summary>客户端。IP加进程</summary>
    public String? ClientId { get; set; }

    /// <summary>本地IP地址</summary>
    public String? IP { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>服务地址。本地服务地址，可能是固定地址，也可能是http://*:8080,https://*:8001之类的形式</summary>
    public String? Address { get; set; }

    /// <summary>外部服务地址。用户访问的原始外网地址，用于内部构造其它Url</summary>
    public String? ExternalAddress { get;set; }

    /// <summary>健康检测地址</summary>
    public String? Health{ get; set; }

    /// <summary>标签。带有指定特性，逗号分隔</summary>
    public String? Tag { get; set; }

    internal Func<String?>? AddressCallback;
}