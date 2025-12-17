namespace Stardust.Storages;

/// <summary>地址信息</summary>
public class AddressInfo
{
    /// <summary>节点名称</summary>
    public String? NodeName { get; set; }

    /// <summary>内网地址</summary>
    public String? InternalAddress { get; set; }

    /// <summary>外网地址</summary>
    public String? ExternalAddress { get; set; }
}
