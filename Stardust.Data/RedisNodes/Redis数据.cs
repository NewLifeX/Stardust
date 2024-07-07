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

/// <summary>Redis数据。Redis监控</summary>
[Serializable]
[DataObject]
[Description("Redis数据。Redis监控")]
[BindIndex("IX_RedisData_RedisId_Id", false, "RedisId,Id")]
[BindTable("RedisData", Description = "Redis数据。Redis监控", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class RedisData
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _RedisId;
    /// <summary>Redis节点</summary>
    [DisplayName("Redis节点")]
    [Description("Redis节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RedisId", "Redis节点", "")]
    public Int32 RedisId { get => _RedisId; set { if (OnPropertyChanging("RedisId", value)) { _RedisId = value; OnPropertyChanged("RedisId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _Speed;
    /// <summary>速度。每秒操作数，instantaneous_ops_per_sec</summary>
    [DisplayName("速度")]
    [Description("速度。每秒操作数，instantaneous_ops_per_sec")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Speed", "速度。每秒操作数，instantaneous_ops_per_sec", "")]
    public Int32 Speed { get => _Speed; set { if (OnPropertyChanging("Speed", value)) { _Speed = value; OnPropertyChanged("Speed"); } } }

    private Double _InputKbps;
    /// <summary>入流量。单位kbps</summary>
    [DisplayName("入流量")]
    [Description("入流量。单位kbps")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputKbps", "入流量。单位kbps", "")]
    public Double InputKbps { get => _InputKbps; set { if (OnPropertyChanging("InputKbps", value)) { _InputKbps = value; OnPropertyChanged("InputKbps"); } } }

    private Double _OutputKbps;
    /// <summary>出流量。单位kbps</summary>
    [DisplayName("出流量")]
    [Description("出流量。单位kbps")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputKbps", "出流量。单位kbps", "")]
    public Double OutputKbps { get => _OutputKbps; set { if (OnPropertyChanging("OutputKbps", value)) { _OutputKbps = value; OnPropertyChanged("OutputKbps"); } } }

    private Int32 _Uptime;
    /// <summary>启动时间。单位秒</summary>
    [DisplayName("启动时间")]
    [Description("启动时间。单位秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Uptime", "启动时间。单位秒", "")]
    public Int32 Uptime { get => _Uptime; set { if (OnPropertyChanging("Uptime", value)) { _Uptime = value; OnPropertyChanged("Uptime"); } } }

    private Int32 _ConnectedClients;
    /// <summary>连接数</summary>
    [DisplayName("连接数")]
    [Description("连接数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConnectedClients", "连接数", "")]
    public Int32 ConnectedClients { get => _ConnectedClients; set { if (OnPropertyChanging("ConnectedClients", value)) { _ConnectedClients = value; OnPropertyChanged("ConnectedClients"); } } }

    private Int32 _UsedMemory;
    /// <summary>已用内存。单位MB</summary>
    [DisplayName("已用内存")]
    [Description("已用内存。单位MB")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UsedMemory", "已用内存。单位MB", "")]
    public Int32 UsedMemory { get => _UsedMemory; set { if (OnPropertyChanging("UsedMemory", value)) { _UsedMemory = value; OnPropertyChanged("UsedMemory"); } } }

    private Double _FragmentationRatio;
    /// <summary>碎片率。单位MB</summary>
    [DisplayName("碎片率")]
    [Description("碎片率。单位MB")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FragmentationRatio", "碎片率。单位MB", "")]
    public Double FragmentationRatio { get => _FragmentationRatio; set { if (OnPropertyChanging("FragmentationRatio", value)) { _FragmentationRatio = value; OnPropertyChanged("FragmentationRatio"); } } }

    private Int64 _Keys;
    /// <summary>Keys数</summary>
    [DisplayName("Keys数")]
    [Description("Keys数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Keys", "Keys数", "")]
    public Int64 Keys { get => _Keys; set { if (OnPropertyChanging("Keys", value)) { _Keys = value; OnPropertyChanged("Keys"); } } }

    private Int64 _ExpiredKeys;
    /// <summary>过期Keys</summary>
    [DisplayName("过期Keys")]
    [Description("过期Keys")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExpiredKeys", "过期Keys", "")]
    public Int64 ExpiredKeys { get => _ExpiredKeys; set { if (OnPropertyChanging("ExpiredKeys", value)) { _ExpiredKeys = value; OnPropertyChanged("ExpiredKeys"); } } }

    private Int64 _EvictedKeys;
    /// <summary>驱逐Keys。由于 maxmemory 限制，而被回收内存的 key 的总数</summary>
    [DisplayName("驱逐Keys")]
    [Description("驱逐Keys。由于 maxmemory 限制，而被回收内存的 key 的总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EvictedKeys", "驱逐Keys。由于 maxmemory 限制，而被回收内存的 key 的总数", "")]
    public Int64 EvictedKeys { get => _EvictedKeys; set { if (OnPropertyChanging("EvictedKeys", value)) { _EvictedKeys = value; OnPropertyChanged("EvictedKeys"); } } }

    private Int64 _KeySpaceHits;
    /// <summary>命中数。只读请求命中缓存</summary>
    [DisplayName("命中数")]
    [Description("命中数。只读请求命中缓存")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("KeySpaceHits", "命中数。只读请求命中缓存", "")]
    public Int64 KeySpaceHits { get => _KeySpaceHits; set { if (OnPropertyChanging("KeySpaceHits", value)) { _KeySpaceHits = value; OnPropertyChanged("KeySpaceHits"); } } }

    private Int64 _KeySpaceMisses;
    /// <summary>Miss数。只读请求未命中缓存</summary>
    [DisplayName("Miss数")]
    [Description("Miss数。只读请求未命中缓存")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("KeySpaceMisses", "Miss数。只读请求未命中缓存", "")]
    public Int64 KeySpaceMisses { get => _KeySpaceMisses; set { if (OnPropertyChanging("KeySpaceMisses", value)) { _KeySpaceMisses = value; OnPropertyChanged("KeySpaceMisses"); } } }

    private Int64 _Commands;
    /// <summary>命令数</summary>
    [DisplayName("命令数")]
    [Description("命令数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Commands", "命令数", "")]
    public Int64 Commands { get => _Commands; set { if (OnPropertyChanging("Commands", value)) { _Commands = value; OnPropertyChanged("Commands"); } } }

    private Int64 _Reads;
    /// <summary>读取数</summary>
    [DisplayName("读取数")]
    [Description("读取数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Reads", "读取数", "")]
    public Int64 Reads { get => _Reads; set { if (OnPropertyChanging("Reads", value)) { _Reads = value; OnPropertyChanged("Reads"); } } }

    private Int64 _Writes;
    /// <summary>写入数</summary>
    [DisplayName("写入数")]
    [Description("写入数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Writes", "写入数", "")]
    public Int64 Writes { get => _Writes; set { if (OnPropertyChanging("Writes", value)) { _Writes = value; OnPropertyChanged("Writes"); } } }

    private Int64 _AvgTtl;
    /// <summary>平均过期。平均过期时间，单位毫秒</summary>
    [DisplayName("平均过期")]
    [Description("平均过期。平均过期时间，单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AvgTtl", "平均过期。平均过期时间，单位毫秒", "")]
    public Int64 AvgTtl { get => _AvgTtl; set { if (OnPropertyChanging("AvgTtl", value)) { _AvgTtl = value; OnPropertyChanged("AvgTtl"); } } }

    private String _TopCommand;
    /// <summary>最忙命令</summary>
    [DisplayName("最忙命令")]
    [Description("最忙命令")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TopCommand", "最忙命令", "")]
    public String TopCommand { get => _TopCommand; set { if (OnPropertyChanging("TopCommand", value)) { _TopCommand = value; OnPropertyChanged("TopCommand"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
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
            "RedisId" => _RedisId,
            "Name" => _Name,
            "Speed" => _Speed,
            "InputKbps" => _InputKbps,
            "OutputKbps" => _OutputKbps,
            "Uptime" => _Uptime,
            "ConnectedClients" => _ConnectedClients,
            "UsedMemory" => _UsedMemory,
            "FragmentationRatio" => _FragmentationRatio,
            "Keys" => _Keys,
            "ExpiredKeys" => _ExpiredKeys,
            "EvictedKeys" => _EvictedKeys,
            "KeySpaceHits" => _KeySpaceHits,
            "KeySpaceMisses" => _KeySpaceMisses,
            "Commands" => _Commands,
            "Reads" => _Reads,
            "Writes" => _Writes,
            "AvgTtl" => _AvgTtl,
            "TopCommand" => _TopCommand,
            "CreateTime" => _CreateTime,
            "TraceId" => _TraceId,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "RedisId": _RedisId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Speed": _Speed = value.ToInt(); break;
                case "InputKbps": _InputKbps = value.ToDouble(); break;
                case "OutputKbps": _OutputKbps = value.ToDouble(); break;
                case "Uptime": _Uptime = value.ToInt(); break;
                case "ConnectedClients": _ConnectedClients = value.ToInt(); break;
                case "UsedMemory": _UsedMemory = value.ToInt(); break;
                case "FragmentationRatio": _FragmentationRatio = value.ToDouble(); break;
                case "Keys": _Keys = value.ToLong(); break;
                case "ExpiredKeys": _ExpiredKeys = value.ToLong(); break;
                case "EvictedKeys": _EvictedKeys = value.ToLong(); break;
                case "KeySpaceHits": _KeySpaceHits = value.ToLong(); break;
                case "KeySpaceMisses": _KeySpaceMisses = value.ToLong(); break;
                case "Commands": _Commands = value.ToLong(); break;
                case "Reads": _Reads = value.ToLong(); break;
                case "Writes": _Writes = value.ToLong(); break;
                case "AvgTtl": _AvgTtl = value.ToLong(); break;
                case "TopCommand": _TopCommand = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
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
    /// <summary>取得Redis数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>Redis节点</summary>
        public static readonly Field RedisId = FindByName("RedisId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>速度。每秒操作数，instantaneous_ops_per_sec</summary>
        public static readonly Field Speed = FindByName("Speed");

        /// <summary>入流量。单位kbps</summary>
        public static readonly Field InputKbps = FindByName("InputKbps");

        /// <summary>出流量。单位kbps</summary>
        public static readonly Field OutputKbps = FindByName("OutputKbps");

        /// <summary>启动时间。单位秒</summary>
        public static readonly Field Uptime = FindByName("Uptime");

        /// <summary>连接数</summary>
        public static readonly Field ConnectedClients = FindByName("ConnectedClients");

        /// <summary>已用内存。单位MB</summary>
        public static readonly Field UsedMemory = FindByName("UsedMemory");

        /// <summary>碎片率。单位MB</summary>
        public static readonly Field FragmentationRatio = FindByName("FragmentationRatio");

        /// <summary>Keys数</summary>
        public static readonly Field Keys = FindByName("Keys");

        /// <summary>过期Keys</summary>
        public static readonly Field ExpiredKeys = FindByName("ExpiredKeys");

        /// <summary>驱逐Keys。由于 maxmemory 限制，而被回收内存的 key 的总数</summary>
        public static readonly Field EvictedKeys = FindByName("EvictedKeys");

        /// <summary>命中数。只读请求命中缓存</summary>
        public static readonly Field KeySpaceHits = FindByName("KeySpaceHits");

        /// <summary>Miss数。只读请求未命中缓存</summary>
        public static readonly Field KeySpaceMisses = FindByName("KeySpaceMisses");

        /// <summary>命令数</summary>
        public static readonly Field Commands = FindByName("Commands");

        /// <summary>读取数</summary>
        public static readonly Field Reads = FindByName("Reads");

        /// <summary>写入数</summary>
        public static readonly Field Writes = FindByName("Writes");

        /// <summary>平均过期。平均过期时间，单位毫秒</summary>
        public static readonly Field AvgTtl = FindByName("AvgTtl");

        /// <summary>最忙命令</summary>
        public static readonly Field TopCommand = FindByName("TopCommand");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得Redis数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>Redis节点</summary>
        public const String RedisId = "RedisId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>速度。每秒操作数，instantaneous_ops_per_sec</summary>
        public const String Speed = "Speed";

        /// <summary>入流量。单位kbps</summary>
        public const String InputKbps = "InputKbps";

        /// <summary>出流量。单位kbps</summary>
        public const String OutputKbps = "OutputKbps";

        /// <summary>启动时间。单位秒</summary>
        public const String Uptime = "Uptime";

        /// <summary>连接数</summary>
        public const String ConnectedClients = "ConnectedClients";

        /// <summary>已用内存。单位MB</summary>
        public const String UsedMemory = "UsedMemory";

        /// <summary>碎片率。单位MB</summary>
        public const String FragmentationRatio = "FragmentationRatio";

        /// <summary>Keys数</summary>
        public const String Keys = "Keys";

        /// <summary>过期Keys</summary>
        public const String ExpiredKeys = "ExpiredKeys";

        /// <summary>驱逐Keys。由于 maxmemory 限制，而被回收内存的 key 的总数</summary>
        public const String EvictedKeys = "EvictedKeys";

        /// <summary>命中数。只读请求命中缓存</summary>
        public const String KeySpaceHits = "KeySpaceHits";

        /// <summary>Miss数。只读请求未命中缓存</summary>
        public const String KeySpaceMisses = "KeySpaceMisses";

        /// <summary>命令数</summary>
        public const String Commands = "Commands";

        /// <summary>读取数</summary>
        public const String Reads = "Reads";

        /// <summary>写入数</summary>
        public const String Writes = "Writes";

        /// <summary>平均过期。平均过期时间，单位毫秒</summary>
        public const String AvgTtl = "AvgTtl";

        /// <summary>最忙命令</summary>
        public const String TopCommand = "TopCommand";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
