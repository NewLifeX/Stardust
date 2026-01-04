using System.ComponentModel;
using NewLife.Remoting.Models;

namespace Stardust.Models;

/// <summary>心跳信息</summary>
/// <remarks>
/// 基类PingRequest包含：IP、AvailableMemory、AvailableFreeSpace、CpuRate、Temperature、Battery、Signal、UplinkSpeed、DownlinkSpeed、Uptime、Time、Delay
/// </remarks>
public class PingInfo : PingRequest
{
    #region 属性
    /// <summary>驱动器信息。各分区大小，逗号分隔</summary>
    public String? DriveInfo { get; set; }

    /// <summary>MAC地址</summary>
    public String? Macs { get; set; }

    /// <summary>框架。本地支持的所有版本框架</summary>
    public String? Framework { get; set; }

    /// <summary>网关地址。IP与MAC，随着网卡变动，可能改变</summary>
    public String? Gateway { get; set; }

    /// <summary>DNS地址。随着网卡变动，可能改变</summary>
    public String? Dns { get; set; }

    /// <summary>进程列表</summary>
    public String? Processes { get; set; }

    /// <summary>进程个数</summary>
    public Int32 ProcessCount { get; set; }

    /// <summary>正在传输的Tcp连接数</summary>
    public Int32 TcpConnections { get; set; }

    /// <summary>主动关闭的Tcp连接数</summary>
    public Int32 TcpTimeWait { get; set; }

    /// <summary>被动关闭的Tcp连接数</summary>
    public Int32 TcpCloseWait { get; set; }

    /// <summary>内网质量。综合评估到网关的心跳延迟和丢包率，满分1分</summary>
    public Double IntranetScore { get; set; }

    /// <summary>外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，满分1分</summary>
    public Double InternetScore { get; set; }

    /// <summary>系统负载。Linux上的Load1，Windows上的处理器队列长度</summary>
    public Double SystemLoad { get; set; }

    /// <summary>磁盘IOPS。每秒磁盘IO操作次数</summary>
    public Int32 DiskIOPS { get; set; }

    /// <summary>磁盘活动时间。多块磁盘的最大活动时间百分比，0-100</summary>
    public Double DiskActiveTime { get; set; }

    /// <summary>网关延迟。到网关的平均延迟，单位ms</summary>
    public Int32 GatewayLatency { get; set; }

    /// <summary>网关丢包率。到网关的丢包率</summary>
    public Double GatewayLossRate { get; set; }

    /// <summary>DNS延迟。到DNS的平均延迟，单位ms</summary>
    public Int32 DnsLatency { get; set; }

    /// <summary>DNS丢包率。到DNS的丢包率</summary>
    public Double DnsLossRate { get; set; }

    /// <summary>服务器延迟。到星尘服务器的平均延迟，单位ms</summary>
    public Int32 ServerLatency { get; set; }

    /// <summary>服务器丢包率。到星尘服务器的丢包率</summary>
    public Double ServerLossRate { get; set; }
    #endregion
}

/// <summary>心跳响应</summary>
public class MyPingResponse : PingResponse
{
    /// <summary>同步时间间隔。定期同步服务器时间到本地，默认0秒不同步</summary>
    public Int32 SyncTime { get; set; }
}