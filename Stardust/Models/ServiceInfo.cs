using System;
using System.Xml.Serialization;

namespace Stardust.Models;

/// <summary>应用服务信息</summary>
public class ServiceInfo
{
    #region 属性
    /// <summary>名称。全局唯一，默认应用名，根据场景可以加dev等后缀</summary>
    [XmlAttribute]
    public String Name { get; set; }

    /// <summary>文件</summary>
    [XmlAttribute]
    public String FileName { get; set; }

    /// <summary>参数</summary>
    [XmlAttribute]
    public String Arguments { get; set; }

    /// <summary>工作目录</summary>
    [XmlAttribute]
    public String WorkingDirectory { get; set; }

    /// <summary>启用</summary>
    [XmlAttribute]
    public Boolean Enable { get; set; }

    /// <summary>是否自动启动</summary>
    [XmlAttribute]
    public Boolean AutoStart { get; set; }

    ///// <summary>是否自动停止。随着宿主的退出，同时停止该应用进程</summary>
    //[XmlAttribute]
    //public Boolean AutoStop { get; set; }

    ///// <summary>检测文件变动。当文件发生改变时，自动重启应用</summary>
    //[XmlAttribute]
    //public Boolean ReloadOnChange { get; set; }

    /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
    [XmlAttribute]
    public Int32 MaxMemory { get; set; }
    #endregion
}