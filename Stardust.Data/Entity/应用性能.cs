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

namespace Stardust.Data;

/// <summary>应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等</summary>
[Serializable]
[DataObject]
[Description("应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等")]
[BindIndex("IX_AppMeter_AppId_ClientId_Id", false, "AppId,ClientId,Id")]
[BindTable("AppMeter", Description = "应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class AppMeter
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _ClientId;
    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    [DisplayName("实例")]
    [Description("实例。应用可能多实例部署，ip@proccessid")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ClientId", "实例。应用可能多实例部署，ip@proccessid", "")]
    public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private String _Source;
    /// <summary>来源。数据来源，应用心跳、监控数据上报携带、远程发布后由StarAgent上报</summary>
    [DisplayName("来源")]
    [Description("来源。数据来源，应用心跳、监控数据上报携带、远程发布后由StarAgent上报")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源。数据来源，应用心跳、监控数据上报携带、远程发布后由StarAgent上报", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int32 _Memory;
    /// <summary>内存。单位M</summary>
    [DisplayName("内存")]
    [Description("内存。单位M")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Memory", "内存。单位M", "")]
    public Int32 Memory { get => _Memory; set { if (OnPropertyChanging("Memory", value)) { _Memory = value; OnPropertyChanged("Memory"); } } }

    private Int32 _ProcessorTime;
    /// <summary>处理器。处理器时间，单位s</summary>
    [DisplayName("处理器")]
    [Description("处理器。处理器时间，单位s")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProcessorTime", "处理器。处理器时间，单位s", "", ItemType = "TimeSpan")]
    public Int32 ProcessorTime { get => _ProcessorTime; set { if (OnPropertyChanging("ProcessorTime", value)) { _ProcessorTime = value; OnPropertyChanged("ProcessorTime"); } } }

    private Double _CpuUsage;
    /// <summary>CPU负载。处理器时间除以物理时间的占比</summary>
    [DisplayName("CPU负载")]
    [Description("CPU负载。处理器时间除以物理时间的占比")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CpuUsage", "CPU负载。处理器时间除以物理时间的占比", "", ItemType = "percent")]
    public Double CpuUsage { get => _CpuUsage; set { if (OnPropertyChanging("CpuUsage", value)) { _CpuUsage = value; OnPropertyChanged("CpuUsage"); } } }

    private Int32 _Threads;
    /// <summary>线程数</summary>
    [DisplayName("线程数")]
    [Description("线程数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Threads", "线程数", "")]
    public Int32 Threads { get => _Threads; set { if (OnPropertyChanging("Threads", value)) { _Threads = value; OnPropertyChanged("Threads"); } } }

    private Int32 _WorkerThreads;
    /// <summary>工作线程。线程池可用工作线程数，主要是Task使用</summary>
    [DisplayName("工作线程")]
    [Description("工作线程。线程池可用工作线程数，主要是Task使用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("WorkerThreads", "工作线程。线程池可用工作线程数，主要是Task使用", "")]
    public Int32 WorkerThreads { get => _WorkerThreads; set { if (OnPropertyChanging("WorkerThreads", value)) { _WorkerThreads = value; OnPropertyChanged("WorkerThreads"); } } }

    private Int32 _IOThreads;
    /// <summary>IO线程。线程池可用IO线程数，主要是网络接收所用</summary>
    [DisplayName("IO线程")]
    [Description("IO线程。线程池可用IO线程数，主要是网络接收所用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IOThreads", "IO线程。线程池可用IO线程数，主要是网络接收所用", "")]
    public Int32 IOThreads { get => _IOThreads; set { if (OnPropertyChanging("IOThreads", value)) { _IOThreads = value; OnPropertyChanged("IOThreads"); } } }

    private Int32 _AvailableThreads;
    /// <summary>活跃线程。线程池活跃线程数，辅助分析线程饥渴问题</summary>
    [DisplayName("活跃线程")]
    [Description("活跃线程。线程池活跃线程数，辅助分析线程饥渴问题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AvailableThreads", "活跃线程。线程池活跃线程数，辅助分析线程饥渴问题", "")]
    public Int32 AvailableThreads { get => _AvailableThreads; set { if (OnPropertyChanging("AvailableThreads", value)) { _AvailableThreads = value; OnPropertyChanged("AvailableThreads"); } } }

    private Int64 _PendingItems;
    /// <summary>挂起任务。线程池挂起任务数，辅助分析线程饥渴问题</summary>
    [DisplayName("挂起任务")]
    [Description("挂起任务。线程池挂起任务数，辅助分析线程饥渴问题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PendingItems", "挂起任务。线程池挂起任务数，辅助分析线程饥渴问题", "")]
    public Int64 PendingItems { get => _PendingItems; set { if (OnPropertyChanging("PendingItems", value)) { _PendingItems = value; OnPropertyChanged("PendingItems"); } } }

    private Int64 _CompletedItems;
    /// <summary>完成任务。线程池已完成任务数，辅助分析线程饥渴问题</summary>
    [DisplayName("完成任务")]
    [Description("完成任务。线程池已完成任务数，辅助分析线程饥渴问题")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CompletedItems", "完成任务。线程池已完成任务数，辅助分析线程饥渴问题", "")]
    public Int64 CompletedItems { get => _CompletedItems; set { if (OnPropertyChanging("CompletedItems", value)) { _CompletedItems = value; OnPropertyChanged("CompletedItems"); } } }

    private Int32 _Handles;
    /// <summary>句柄数</summary>
    [DisplayName("句柄数")]
    [Description("句柄数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Handles", "句柄数", "")]
    public Int32 Handles { get => _Handles; set { if (OnPropertyChanging("Handles", value)) { _Handles = value; OnPropertyChanged("Handles"); } } }

    private Int32 _Connections;
    /// <summary>连接数</summary>
    [DisplayName("连接数")]
    [Description("连接数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Connections", "连接数", "")]
    public Int32 Connections { get => _Connections; set { if (OnPropertyChanging("Connections", value)) { _Connections = value; OnPropertyChanged("Connections"); } } }

    private Int32 _GCCount;
    /// <summary>GC次数。周期时间内发生GC的次数</summary>
    [DisplayName("GC次数")]
    [Description("GC次数。周期时间内发生GC的次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GCCount", "GC次数。周期时间内发生GC的次数", "")]
    public Int32 GCCount { get => _GCCount; set { if (OnPropertyChanging("GCCount", value)) { _GCCount = value; OnPropertyChanged("GCCount"); } } }

    private Int32 _HeapSize;
    /// <summary>堆内存。单位M</summary>
    [DisplayName("堆内存")]
    [Description("堆内存。单位M")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HeapSize", "堆内存。单位M", "")]
    public Int32 HeapSize { get => _HeapSize; set { if (OnPropertyChanging("HeapSize", value)) { _HeapSize = value; OnPropertyChanged("HeapSize"); } } }

    private DateTime _Time;
    /// <summary>采集时间</summary>
    [DisplayName("采集时间")]
    [Description("采集时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Time", "采集时间", "")]
    public DateTime Time { get => _Time; set { if (OnPropertyChanging("Time", value)) { _Time = value; OnPropertyChanged("Time"); } } }

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
            "AppId" => _AppId,
            "ClientId" => _ClientId,
            "Source" => _Source,
            "Memory" => _Memory,
            "ProcessorTime" => _ProcessorTime,
            "CpuUsage" => _CpuUsage,
            "Threads" => _Threads,
            "WorkerThreads" => _WorkerThreads,
            "IOThreads" => _IOThreads,
            "AvailableThreads" => _AvailableThreads,
            "PendingItems" => _PendingItems,
            "CompletedItems" => _CompletedItems,
            "Handles" => _Handles,
            "Connections" => _Connections,
            "GCCount" => _GCCount,
            "HeapSize" => _HeapSize,
            "Time" => _Time,
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
                case "AppId": _AppId = value.ToInt(); break;
                case "ClientId": _ClientId = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Memory": _Memory = value.ToInt(); break;
                case "ProcessorTime": _ProcessorTime = value.ToInt(); break;
                case "CpuUsage": _CpuUsage = value.ToDouble(); break;
                case "Threads": _Threads = value.ToInt(); break;
                case "WorkerThreads": _WorkerThreads = value.ToInt(); break;
                case "IOThreads": _IOThreads = value.ToInt(); break;
                case "AvailableThreads": _AvailableThreads = value.ToInt(); break;
                case "PendingItems": _PendingItems = value.ToLong(); break;
                case "CompletedItems": _CompletedItems = value.ToLong(); break;
                case "Handles": _Handles = value.ToInt(); break;
                case "Connections": _Connections = value.ToInt(); break;
                case "GCCount": _GCCount = value.ToInt(); break;
                case "HeapSize": _HeapSize = value.ToInt(); break;
                case "Time": _Time = value.ToDateTime(); break;
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
    /// <summary>取得应用性能字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public static readonly Field ClientId = FindByName("ClientId");

        /// <summary>来源。数据来源，应用心跳、监控数据上报携带、远程发布后由StarAgent上报</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>内存。单位M</summary>
        public static readonly Field Memory = FindByName("Memory");

        /// <summary>处理器。处理器时间，单位s</summary>
        public static readonly Field ProcessorTime = FindByName("ProcessorTime");

        /// <summary>CPU负载。处理器时间除以物理时间的占比</summary>
        public static readonly Field CpuUsage = FindByName("CpuUsage");

        /// <summary>线程数</summary>
        public static readonly Field Threads = FindByName("Threads");

        /// <summary>工作线程。线程池可用工作线程数，主要是Task使用</summary>
        public static readonly Field WorkerThreads = FindByName("WorkerThreads");

        /// <summary>IO线程。线程池可用IO线程数，主要是网络接收所用</summary>
        public static readonly Field IOThreads = FindByName("IOThreads");

        /// <summary>活跃线程。线程池活跃线程数，辅助分析线程饥渴问题</summary>
        public static readonly Field AvailableThreads = FindByName("AvailableThreads");

        /// <summary>挂起任务。线程池挂起任务数，辅助分析线程饥渴问题</summary>
        public static readonly Field PendingItems = FindByName("PendingItems");

        /// <summary>完成任务。线程池已完成任务数，辅助分析线程饥渴问题</summary>
        public static readonly Field CompletedItems = FindByName("CompletedItems");

        /// <summary>句柄数</summary>
        public static readonly Field Handles = FindByName("Handles");

        /// <summary>连接数</summary>
        public static readonly Field Connections = FindByName("Connections");

        /// <summary>GC次数。周期时间内发生GC的次数</summary>
        public static readonly Field GCCount = FindByName("GCCount");

        /// <summary>堆内存。单位M</summary>
        public static readonly Field HeapSize = FindByName("HeapSize");

        /// <summary>采集时间</summary>
        public static readonly Field Time = FindByName("Time");

        /// <summary>创建者。服务端节点</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用性能字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public const String ClientId = "ClientId";

        /// <summary>来源。数据来源，应用心跳、监控数据上报携带、远程发布后由StarAgent上报</summary>
        public const String Source = "Source";

        /// <summary>内存。单位M</summary>
        public const String Memory = "Memory";

        /// <summary>处理器。处理器时间，单位s</summary>
        public const String ProcessorTime = "ProcessorTime";

        /// <summary>CPU负载。处理器时间除以物理时间的占比</summary>
        public const String CpuUsage = "CpuUsage";

        /// <summary>线程数</summary>
        public const String Threads = "Threads";

        /// <summary>工作线程。线程池可用工作线程数，主要是Task使用</summary>
        public const String WorkerThreads = "WorkerThreads";

        /// <summary>IO线程。线程池可用IO线程数，主要是网络接收所用</summary>
        public const String IOThreads = "IOThreads";

        /// <summary>活跃线程。线程池活跃线程数，辅助分析线程饥渴问题</summary>
        public const String AvailableThreads = "AvailableThreads";

        /// <summary>挂起任务。线程池挂起任务数，辅助分析线程饥渴问题</summary>
        public const String PendingItems = "PendingItems";

        /// <summary>完成任务。线程池已完成任务数，辅助分析线程饥渴问题</summary>
        public const String CompletedItems = "CompletedItems";

        /// <summary>句柄数</summary>
        public const String Handles = "Handles";

        /// <summary>连接数</summary>
        public const String Connections = "Connections";

        /// <summary>GC次数。周期时间内发生GC的次数</summary>
        public const String GCCount = "GCCount";

        /// <summary>堆内存。单位M</summary>
        public const String HeapSize = "HeapSize";

        /// <summary>采集时间</summary>
        public const String Time = "Time";

        /// <summary>创建者。服务端节点</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
