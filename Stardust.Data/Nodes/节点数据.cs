using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes;

/// <summary>节点数据。保存设备上来的一些数据，如心跳状态</summary>
[Serializable]
[DataObject]
[Description("节点数据。保存设备上来的一些数据，如心跳状态")]
[BindIndex("IX_NodeData_NodeID", false, "NodeID")]
[BindTable("NodeData", Description = "节点数据。保存设备上来的一些数据，如心跳状态", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class NodeData
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _NodeID;
    /// <summary>节点</summary>
    [DisplayName("节点")]
    [Description("节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeID", "节点", "")]
    public Int32 NodeID { get => _NodeID; set { if (OnPropertyChanging("NodeID", value)) { _NodeID = value; OnPropertyChanged("NodeID"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _AvailableMemory;
    /// <summary>可用内存。单位M</summary>
    [DisplayName("可用内存")]
    [Description("可用内存。单位M")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AvailableMemory", "可用内存。单位M", "")]
    public Int32 AvailableMemory { get => _AvailableMemory; set { if (OnPropertyChanging("AvailableMemory", value)) { _AvailableMemory = value; OnPropertyChanged("AvailableMemory"); } } }

    private Int32 _AvailableFreeSpace;
    /// <summary>可用磁盘。应用所在盘，单位M</summary>
    [DisplayName("可用磁盘")]
    [Description("可用磁盘。应用所在盘，单位M")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AvailableFreeSpace", "可用磁盘。应用所在盘，单位M", "")]
    public Int32 AvailableFreeSpace { get => _AvailableFreeSpace; set { if (OnPropertyChanging("AvailableFreeSpace", value)) { _AvailableFreeSpace = value; OnPropertyChanged("AvailableFreeSpace"); } } }

    private Double _CpuRate;
    /// <summary>CPU率。占用率</summary>
    [DisplayName("CPU率")]
    [Description("CPU率。占用率")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CpuRate", "CPU率。占用率", "", ItemType = "percent")]
    public Double CpuRate { get => _CpuRate; set { if (OnPropertyChanging("CpuRate", value)) { _CpuRate = value; OnPropertyChanged("CpuRate"); } } }

    private Double _Temperature;
    /// <summary>温度</summary>
    [DisplayName("温度")]
    [Description("温度")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Temperature", "温度", "")]
    public Double Temperature { get => _Temperature; set { if (OnPropertyChanging("Temperature", value)) { _Temperature = value; OnPropertyChanged("Temperature"); } } }

    private Double _Battery;
    /// <summary>电量</summary>
    [DisplayName("电量")]
    [Description("电量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Battery", "电量", "", ItemType = "percent")]
    public Double Battery { get => _Battery; set { if (OnPropertyChanging("Battery", value)) { _Battery = value; OnPropertyChanged("Battery"); } } }

    private Int32 _Signal;
    /// <summary>信号。信号强度，WiFi/4G</summary>
    [DisplayName("信号")]
    [Description("信号。信号强度，WiFi/4G")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Signal", "信号。信号强度，WiFi/4G", "")]
    public Int32 Signal { get => _Signal; set { if (OnPropertyChanging("Signal", value)) { _Signal = value; OnPropertyChanged("Signal"); } } }

    private Int64 _UplinkSpeed;
    /// <summary>上行速度。网络发送速度，字节每秒</summary>
    [DisplayName("上行速度")]
    [Description("上行速度。网络发送速度，字节每秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UplinkSpeed", "上行速度。网络发送速度，字节每秒", "", ItemType = "GMK")]
    public Int64 UplinkSpeed { get => _UplinkSpeed; set { if (OnPropertyChanging("UplinkSpeed", value)) { _UplinkSpeed = value; OnPropertyChanged("UplinkSpeed"); } } }

    private Int64 _DownlinkSpeed;
    /// <summary>下行速度。网络接收速度，字节每秒</summary>
    [DisplayName("下行速度")]
    [Description("下行速度。网络接收速度，字节每秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DownlinkSpeed", "下行速度。网络接收速度，字节每秒", "", ItemType = "GMK")]
    public Int64 DownlinkSpeed { get => _DownlinkSpeed; set { if (OnPropertyChanging("DownlinkSpeed", value)) { _DownlinkSpeed = value; OnPropertyChanged("DownlinkSpeed"); } } }

    private Int32 _ProcessCount;
    /// <summary>进程数</summary>
    [DisplayName("进程数")]
    [Description("进程数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProcessCount", "进程数", "")]
    public Int32 ProcessCount { get => _ProcessCount; set { if (OnPropertyChanging("ProcessCount", value)) { _ProcessCount = value; OnPropertyChanged("ProcessCount"); } } }

    private Int32 _TcpConnections;
    /// <summary>连接数。传输数据Established的Tcp网络连接数</summary>
    [DisplayName("连接数")]
    [Description("连接数。传输数据Established的Tcp网络连接数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TcpConnections", "连接数。传输数据Established的Tcp网络连接数", "")]
    public Int32 TcpConnections { get => _TcpConnections; set { if (OnPropertyChanging("TcpConnections", value)) { _TcpConnections = value; OnPropertyChanged("TcpConnections"); } } }

    private Int32 _TcpTimeWait;
    /// <summary>主动关闭。主动关闭后TimeWait的Tcp网络连接数，等待2MSL，确保四次挥手的最后一个ACK能够发出，下一步Closed</summary>
    [DisplayName("主动关闭")]
    [Description("主动关闭。主动关闭后TimeWait的Tcp网络连接数，等待2MSL，确保四次挥手的最后一个ACK能够发出，下一步Closed")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TcpTimeWait", "主动关闭。主动关闭后TimeWait的Tcp网络连接数，等待2MSL，确保四次挥手的最后一个ACK能够发出，下一步Closed", "")]
    public Int32 TcpTimeWait { get => _TcpTimeWait; set { if (OnPropertyChanging("TcpTimeWait", value)) { _TcpTimeWait = value; OnPropertyChanged("TcpTimeWait"); } } }

    private Int32 _TcpCloseWait;
    /// <summary>被动关闭。作为客户端，收到服务器FIN后进入CloseWait的Tcp网络连接数，还没发送自己的FIN，主要原因是我方太忙</summary>
    [DisplayName("被动关闭")]
    [Description("被动关闭。作为客户端，收到服务器FIN后进入CloseWait的Tcp网络连接数，还没发送自己的FIN，主要原因是我方太忙")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TcpCloseWait", "被动关闭。作为客户端，收到服务器FIN后进入CloseWait的Tcp网络连接数，还没发送自己的FIN，主要原因是我方太忙", "")]
    public Int32 TcpCloseWait { get => _TcpCloseWait; set { if (OnPropertyChanging("TcpCloseWait", value)) { _TcpCloseWait = value; OnPropertyChanged("TcpCloseWait"); } } }

    private Double _IntranetScore;
    /// <summary>内网质量。评估到网关的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
    [DisplayName("内网质量")]
    [Description("内网质量。评估到网关的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IntranetScore", "内网质量。评估到网关的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%", "", ItemType = "percent")]
    public Double IntranetScore { get => _IntranetScore; set { if (OnPropertyChanging("IntranetScore", value)) { _IntranetScore = value; OnPropertyChanged("IntranetScore"); } } }

    private Double _InternetScore;
    /// <summary>外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
    [DisplayName("外网质量")]
    [Description("外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InternetScore", "外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%", "", ItemType = "percent")]
    public Double InternetScore { get => _InternetScore; set { if (OnPropertyChanging("InternetScore", value)) { _InternetScore = value; OnPropertyChanged("InternetScore"); } } }

    private Int32 _Delay;
    /// <summary>延迟。网络延迟，客户端最近一次心跳耗时的一半，单位ms</summary>
    [DisplayName("延迟")]
    [Description("延迟。网络延迟，客户端最近一次心跳耗时的一半，单位ms")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Delay", "延迟。网络延迟，客户端最近一次心跳耗时的一半，单位ms", "")]
    public Int32 Delay { get => _Delay; set { if (OnPropertyChanging("Delay", value)) { _Delay = value; OnPropertyChanged("Delay"); } } }

    private Int32 _Offset;
    /// <summary>偏移。客户端UTC时间加上一半延迟再减服务端UTC时间，单位ms</summary>
    [DisplayName("偏移")]
    [Description("偏移。客户端UTC时间加上一半延迟再减服务端UTC时间，单位ms")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Offset", "偏移。客户端UTC时间加上一半延迟再减服务端UTC时间，单位ms", "")]
    public Int32 Offset { get => _Offset; set { if (OnPropertyChanging("Offset", value)) { _Offset = value; OnPropertyChanged("Offset"); } } }

    private DateTime _LocalTime;
    /// <summary>本地时间</summary>
    [DisplayName("本地时间")]
    [Description("本地时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LocalTime", "本地时间", "")]
    public DateTime LocalTime { get => _LocalTime; set { if (OnPropertyChanging("LocalTime", value)) { _LocalTime = value; OnPropertyChanged("LocalTime"); } } }

    private Int32 _Uptime;
    /// <summary>开机时间。单位s</summary>
    [DisplayName("开机时间")]
    [Description("开机时间。单位s")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Uptime", "开机时间。单位s", "", ItemType = "TimeSpan")]
    public Int32 Uptime { get => _Uptime; set { if (OnPropertyChanging("Uptime", value)) { _Uptime = value; OnPropertyChanged("Uptime"); } } }

    private String _Creator;
    /// <summary>创建者。服务端节点</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者。服务端节点")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Creator", "创建者。服务端节点", "")]
    public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "NodeID" => _NodeID,
            "Name" => _Name,
            "AvailableMemory" => _AvailableMemory,
            "AvailableFreeSpace" => _AvailableFreeSpace,
            "CpuRate" => _CpuRate,
            "Temperature" => _Temperature,
            "Battery" => _Battery,
            "Signal" => _Signal,
            "UplinkSpeed" => _UplinkSpeed,
            "DownlinkSpeed" => _DownlinkSpeed,
            "ProcessCount" => _ProcessCount,
            "TcpConnections" => _TcpConnections,
            "TcpTimeWait" => _TcpTimeWait,
            "TcpCloseWait" => _TcpCloseWait,
            "IntranetScore" => _IntranetScore,
            "InternetScore" => _InternetScore,
            "Delay" => _Delay,
            "Offset" => _Offset,
            "LocalTime" => _LocalTime,
            "Uptime" => _Uptime,
            "Creator" => _Creator,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "NodeID": _NodeID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "AvailableMemory": _AvailableMemory = value.ToInt(); break;
                case "AvailableFreeSpace": _AvailableFreeSpace = value.ToInt(); break;
                case "CpuRate": _CpuRate = value.ToDouble(); break;
                case "Temperature": _Temperature = value.ToDouble(); break;
                case "Battery": _Battery = value.ToDouble(); break;
                case "Signal": _Signal = value.ToInt(); break;
                case "UplinkSpeed": _UplinkSpeed = value.ToLong(); break;
                case "DownlinkSpeed": _DownlinkSpeed = value.ToLong(); break;
                case "ProcessCount": _ProcessCount = value.ToInt(); break;
                case "TcpConnections": _TcpConnections = value.ToInt(); break;
                case "TcpTimeWait": _TcpTimeWait = value.ToInt(); break;
                case "TcpCloseWait": _TcpCloseWait = value.ToInt(); break;
                case "IntranetScore": _IntranetScore = value.ToDouble(); break;
                case "InternetScore": _InternetScore = value.ToDouble(); break;
                case "Delay": _Delay = value.ToInt(); break;
                case "Offset": _Offset = value.ToInt(); break;
                case "LocalTime": _LocalTime = value.ToDateTime(); break;
                case "Uptime": _Uptime = value.ToInt(); break;
                case "Creator": _Creator = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeData FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow));
    }
    #endregion

    #region 字段名
    /// <summary>取得节点数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>节点</summary>
        public static readonly Field NodeID = FindByName("NodeID");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>可用内存。单位M</summary>
        public static readonly Field AvailableMemory = FindByName("AvailableMemory");

        /// <summary>可用磁盘。应用所在盘，单位M</summary>
        public static readonly Field AvailableFreeSpace = FindByName("AvailableFreeSpace");

        /// <summary>CPU率。占用率</summary>
        public static readonly Field CpuRate = FindByName("CpuRate");

        /// <summary>温度</summary>
        public static readonly Field Temperature = FindByName("Temperature");

        /// <summary>电量</summary>
        public static readonly Field Battery = FindByName("Battery");

        /// <summary>信号。信号强度，WiFi/4G</summary>
        public static readonly Field Signal = FindByName("Signal");

        /// <summary>上行速度。网络发送速度，字节每秒</summary>
        public static readonly Field UplinkSpeed = FindByName("UplinkSpeed");

        /// <summary>下行速度。网络接收速度，字节每秒</summary>
        public static readonly Field DownlinkSpeed = FindByName("DownlinkSpeed");

        /// <summary>进程数</summary>
        public static readonly Field ProcessCount = FindByName("ProcessCount");

        /// <summary>连接数。传输数据Established的Tcp网络连接数</summary>
        public static readonly Field TcpConnections = FindByName("TcpConnections");

        /// <summary>主动关闭。主动关闭后TimeWait的Tcp网络连接数，等待2MSL，确保四次挥手的最后一个ACK能够发出，下一步Closed</summary>
        public static readonly Field TcpTimeWait = FindByName("TcpTimeWait");

        /// <summary>被动关闭。作为客户端，收到服务器FIN后进入CloseWait的Tcp网络连接数，还没发送自己的FIN，主要原因是我方太忙</summary>
        public static readonly Field TcpCloseWait = FindByName("TcpCloseWait");

        /// <summary>内网质量。评估到网关的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
        public static readonly Field IntranetScore = FindByName("IntranetScore");

        /// <summary>外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
        public static readonly Field InternetScore = FindByName("InternetScore");

        /// <summary>延迟。网络延迟，客户端最近一次心跳耗时的一半，单位ms</summary>
        public static readonly Field Delay = FindByName("Delay");

        /// <summary>偏移。客户端UTC时间加上一半延迟再减服务端UTC时间，单位ms</summary>
        public static readonly Field Offset = FindByName("Offset");

        /// <summary>本地时间</summary>
        public static readonly Field LocalTime = FindByName("LocalTime");

        /// <summary>开机时间。单位s</summary>
        public static readonly Field Uptime = FindByName("Uptime");

        /// <summary>创建者。服务端节点</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得节点数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>节点</summary>
        public const String NodeID = "NodeID";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>可用内存。单位M</summary>
        public const String AvailableMemory = "AvailableMemory";

        /// <summary>可用磁盘。应用所在盘，单位M</summary>
        public const String AvailableFreeSpace = "AvailableFreeSpace";

        /// <summary>CPU率。占用率</summary>
        public const String CpuRate = "CpuRate";

        /// <summary>温度</summary>
        public const String Temperature = "Temperature";

        /// <summary>电量</summary>
        public const String Battery = "Battery";

        /// <summary>信号。信号强度，WiFi/4G</summary>
        public const String Signal = "Signal";

        /// <summary>上行速度。网络发送速度，字节每秒</summary>
        public const String UplinkSpeed = "UplinkSpeed";

        /// <summary>下行速度。网络接收速度，字节每秒</summary>
        public const String DownlinkSpeed = "DownlinkSpeed";

        /// <summary>进程数</summary>
        public const String ProcessCount = "ProcessCount";

        /// <summary>连接数。传输数据Established的Tcp网络连接数</summary>
        public const String TcpConnections = "TcpConnections";

        /// <summary>主动关闭。主动关闭后TimeWait的Tcp网络连接数，等待2MSL，确保四次挥手的最后一个ACK能够发出，下一步Closed</summary>
        public const String TcpTimeWait = "TcpTimeWait";

        /// <summary>被动关闭。作为客户端，收到服务器FIN后进入CloseWait的Tcp网络连接数，还没发送自己的FIN，主要原因是我方太忙</summary>
        public const String TcpCloseWait = "TcpCloseWait";

        /// <summary>内网质量。评估到网关的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
        public const String IntranetScore = "IntranetScore";

        /// <summary>外网质量。综合评估到DNS和星尘服务器的心跳延迟和丢包率，1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%</summary>
        public const String InternetScore = "InternetScore";

        /// <summary>延迟。网络延迟，客户端最近一次心跳耗时的一半，单位ms</summary>
        public const String Delay = "Delay";

        /// <summary>偏移。客户端UTC时间加上一半延迟再减服务端UTC时间，单位ms</summary>
        public const String Offset = "Offset";

        /// <summary>本地时间</summary>
        public const String LocalTime = "LocalTime";

        /// <summary>开机时间。单位s</summary>
        public const String Uptime = "Uptime";

        /// <summary>创建者。服务端节点</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
