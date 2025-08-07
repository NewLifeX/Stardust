using System.Xml.Serialization;

namespace Stardust.Models;

/// <summary>应用服务信息</summary>
public class ServiceInfo
{
    #region 属性
    /// <summary>名称。全局唯一，默认应用名，根据场景可以加dev等后缀</summary>
    [XmlAttribute]
    public String Name { get; set; } = null!;

    /// <summary>文件名。启动进程时使用，如果是zip文件则经过内部处理</summary>
    [XmlAttribute]
    public String FileName { get; set; } = null!;

    /// <summary>参数。启动进程时使用</summary>
    [XmlAttribute]
    public String? Arguments { get; set; }

    /// <summary>工作目录。启动进程时使用</summary>
    [XmlAttribute]
    public String? WorkingDirectory { get; set; }

    /// <summary>用户。以该用户执行应用</summary>
    [XmlAttribute]
    public String? UserName { get; set; }

    /// <summary>启用</summary>
    [XmlAttribute]
    public Boolean Enable { get; set; }

    /// <summary>服务工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip</summary>
    [XmlAttribute]
    public ServiceModes Mode { get; set; }

    /// <summary>环境变量。启动应用前设置的环境变量</summary>
    [XmlAttribute]
    public String? Environments { get; set; }

    /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
    [XmlAttribute]
    public Boolean AutoStop { get; set; }

    /// <summary>检测变动。当文件发生改变时，自动重启应用</summary>
    [XmlAttribute]
    public Boolean ReloadOnChange { get; set; }

    /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
    [XmlAttribute]
    public Int32 MaxMemory { get; set; }

    /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
    [XmlAttribute]
    public ProcessPriority Priority { get; set; }

    /// <summary>压缩包文件</summary>
    [XmlIgnore]
    public String? ZipFile { get; set; }
    #endregion

    /// <summary>克隆当前对象</summary>
    /// <returns></returns>
    public ServiceInfo Clone() => (MemberwiseClone() as ServiceInfo)!;

    /// <summary>已重载。友好显示</summary>
    /// <returns></returns>
    public override String ToString() => $"{Name} {FileName}";
}