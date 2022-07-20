using System.Xml.Serialization;

namespace NetworkDetect;

/// <summary>服务信息</summary>
public class ServiceItem
{
    #region 属性
    /// <summary>名称</summary>
    [XmlAttribute]
    public String Name { get; set; }

    /// <summary>地址。如 192.168.1.1</summary>
    [XmlAttribute]
    public String Address { get; set; }

    /// <summary>超时时间。默认1000</summary>
    [XmlAttribute]
    public Int32 Timeout { get; set; } = 1000;

    /// <summary>启用</summary>
    [XmlAttribute]
    public Boolean Enable { get; set; }
    #endregion
}