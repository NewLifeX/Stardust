namespace Stardust.Models;

/// <summary>节点信息</summary>
public class NodeInfo
{
    #region 属性
    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>编译时间</summary>
    public DateTime Compile { get; set; }

    /// <summary>系统名</summary>
    public String? OSName { get; set; }

    /// <summary>系统版本</summary>
    public String? OSVersion { get; set; }

    /// <summary>处理器架构</summary>
    public String? Architecture { get; set; }

    /// <summary>机器名</summary>
    public String? MachineName { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

    /// <summary>本地IP地址。随着网卡变动，可能改变</summary>
    public String? IP { get; set; }

    /// <summary>网关地址。IP与MAC，随着网卡变动，可能改变</summary>
    public String? Gateway { get; set; }

    /// <summary>DNS地址。随着网卡变动，可能改变</summary>
    public String? Dns { get; set; }

    /// <summary>核心数</summary>
    public Int32 ProcessorCount { get; set; }

    /// <summary>内存大小</summary>
    public UInt64 Memory { get; set; }

    /// <summary>可用内存大小</summary>
    public UInt64 AvailableMemory { get; set; }

    /// <summary>磁盘大小。应用所在盘</summary>
    public UInt64 TotalSize { get; set; }

    /// <summary>磁盘可用空间。应用所在盘</summary>
    public UInt64 AvailableFreeSpace { get; set; }

    /// <summary>驱动器大小。所有分区总大小，单位M</summary>
    public UInt64 DriveSize { get; set; }

    /// <summary>驱动器信息。各分区大小，逗号分隔</summary>
    public String? DriveInfo { get; set; }

    /// <summary>最大打开文件。Linux上的ulimit -n</summary>
    public Int32 MaxOpenFiles { get; set; }

    /// <summary>像素点。默认96*96</summary>
    public String? Dpi { get; set; }

    /// <summary>分辨率。例如1024*768</summary>
    public String? Resolution { get; set; }

    /// <summary>产品名称</summary>
    public String? Product { get; set; }

    /// <summary>制造商</summary>
    public String? Vendor { get; set; }

    /// <summary>处理器</summary>
    public String? Processor { get; set; }

    ///// <summary>处理器标识</summary>
    //public String? CpuID { get; set; }

    ///// <summary>主频</summary>
    //public Single CpuRate { get; set; }

    /// <summary>唯一标识</summary>
    public String? UUID { get; set; }

    /// <summary>机器标识</summary>
    public String? MachineGuid { get; set; }

    /// <summary>计算机序列号。适用于品牌机，跟笔记本标签显示一致</summary>
    public String? SerialNumber { get; set; }

    /// <summary>主板。序列号或家族信息</summary>
    public String? Board { get; set; }

    /// <summary>磁盘标识</summary>
    public String? DiskID { get; set; }

    /// <summary>MAC地址</summary>
    public String? Macs { get; set; }

    ///// <summary>串口</summary>
    //public String? COMs { get; set; }

    /// <summary>安装路径</summary>
    public String? InstallPath { get; set; }

    /// <summary>运行时。运行应用的当前运行时版本</summary>
    public String? Runtime { get; set; }

    /// <summary>框架。本地支持的所有版本框架</summary>
    public String? Framework { get; set; }

    /// <summary>本地UTC时间</summary>
    /// <remarks>
    /// 跨系统传递UTC时间是严谨的，但是UTC时间序列化比较头疼，目前能够做到自己序列化后，自己能够解析出来，暂时用着，将来向netcore的system.text.json序列化迁移
    /// </remarks>
    public DateTime Time { get; set; }

    /// <summary>插件列表</summary>
    public String[]? Plugins { get; set; }
    #endregion
}