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

/// <summary>MySql数据。MySql监控</summary>
[Serializable]
[DataObject]
[Description("MySql数据。MySql监控")]
[BindIndex("IX_MySqlData_MySqlId_Id", false, "MySqlId,Id")]
[BindTable("MySqlData", Description = "MySql数据。MySql监控", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class MySqlData
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _MySqlId;
    /// <summary>MySql节点</summary>
    [DisplayName("MySql节点")]
    [Description("MySql节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MySqlId", "MySql节点", "")]
    public Int32 MySqlId { get => _MySqlId; set { if (OnPropertyChanging("MySqlId", value)) { _MySqlId = value; OnPropertyChanged("MySqlId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _Uptime;
    /// <summary>启动时间。单位秒</summary>
    [DisplayName("启动时间")]
    [Description("启动时间。单位秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Uptime", "启动时间。单位秒", "")]
    public Int32 Uptime { get => _Uptime; set { if (OnPropertyChanging("Uptime", value)) { _Uptime = value; OnPropertyChanged("Uptime"); } } }

    private Int32 _Connections;
    /// <summary>当前连接数</summary>
    [DisplayName("当前连接数")]
    [Description("当前连接数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Connections", "当前连接数", "")]
    public Int32 Connections { get => _Connections; set { if (OnPropertyChanging("Connections", value)) { _Connections = value; OnPropertyChanged("Connections"); } } }

    private Int32 _MaxConnections;
    /// <summary>最大连接数</summary>
    [DisplayName("最大连接数")]
    [Description("最大连接数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxConnections", "最大连接数", "")]
    public Int32 MaxConnections { get => _MaxConnections; set { if (OnPropertyChanging("MaxConnections", value)) { _MaxConnections = value; OnPropertyChanged("MaxConnections"); } } }

    private Int32 _Qps;
    /// <summary>每秒查询数</summary>
    [DisplayName("每秒查询数")]
    [Description("每秒查询数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("QPS", "每秒查询数", "")]
    public Int32 Qps { get => _Qps; set { if (OnPropertyChanging("Qps", value)) { _Qps = value; OnPropertyChanged("Qps"); } } }

    private Int32 _Tps;
    /// <summary>每秒事务数</summary>
    [DisplayName("每秒事务数")]
    [Description("每秒事务数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TPS", "每秒事务数", "")]
    public Int32 Tps { get => _Tps; set { if (OnPropertyChanging("Tps", value)) { _Tps = value; OnPropertyChanged("Tps"); } } }

    private Int64 _BytesReceived;
    /// <summary>接收字节数</summary>
    [DisplayName("接收字节数")]
    [Description("接收字节数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BytesReceived", "接收字节数", "")]
    public Int64 BytesReceived { get => _BytesReceived; set { if (OnPropertyChanging("BytesReceived", value)) { _BytesReceived = value; OnPropertyChanged("BytesReceived"); } } }

    private Int64 _BytesSent;
    /// <summary>发送字节数</summary>
    [DisplayName("发送字节数")]
    [Description("发送字节数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BytesSent", "发送字节数", "")]
    public Int64 BytesSent { get => _BytesSent; set { if (OnPropertyChanging("BytesSent", value)) { _BytesSent = value; OnPropertyChanged("BytesSent"); } } }

    private Int64 _InnodbBufferPoolSize;
    /// <summary>InnoDB缓冲池大小。单位MB</summary>
    [DisplayName("InnoDB缓冲池大小")]
    [Description("InnoDB缓冲池大小。单位MB")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InnodbBufferPoolSize", "InnoDB缓冲池大小。单位MB", "")]
    public Int64 InnodbBufferPoolSize { get => _InnodbBufferPoolSize; set { if (OnPropertyChanging("InnodbBufferPoolSize", value)) { _InnodbBufferPoolSize = value; OnPropertyChanged("InnodbBufferPoolSize"); } } }

    private Int32 _InnodbBufferPoolPages;
    /// <summary>InnoDB缓冲池页数</summary>
    [DisplayName("InnoDB缓冲池页数")]
    [Description("InnoDB缓冲池页数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InnodbBufferPoolPages", "InnoDB缓冲池页数", "")]
    public Int32 InnodbBufferPoolPages { get => _InnodbBufferPoolPages; set { if (OnPropertyChanging("InnodbBufferPoolPages", value)) { _InnodbBufferPoolPages = value; OnPropertyChanged("InnodbBufferPoolPages"); } } }

    private Double _InnodbBufferPoolHitRate;
    /// <summary>InnoDB缓冲池命中率</summary>
    [DisplayName("InnoDB缓冲池命中率")]
    [Description("InnoDB缓冲池命中率")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InnodbBufferPoolHitRate", "InnoDB缓冲池命中率", "")]
    public Double InnodbBufferPoolHitRate { get => _InnodbBufferPoolHitRate; set { if (OnPropertyChanging("InnodbBufferPoolHitRate", value)) { _InnodbBufferPoolHitRate = value; OnPropertyChanged("InnodbBufferPoolHitRate"); } } }

    private Double _KeyCacheHitRate;
    /// <summary>键缓存命中率</summary>
    [DisplayName("键缓存命中率")]
    [Description("键缓存命中率")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("KeyCacheHitRate", "键缓存命中率", "")]
    public Double KeyCacheHitRate { get => _KeyCacheHitRate; set { if (OnPropertyChanging("KeyCacheHitRate", value)) { _KeyCacheHitRate = value; OnPropertyChanged("KeyCacheHitRate"); } } }

    private Double _QueryCacheHitRate;
    /// <summary>查询缓存命中率</summary>
    [DisplayName("查询缓存命中率")]
    [Description("查询缓存命中率")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("QueryCacheHitRate", "查询缓存命中率", "")]
    public Double QueryCacheHitRate { get => _QueryCacheHitRate; set { if (OnPropertyChanging("QueryCacheHitRate", value)) { _QueryCacheHitRate = value; OnPropertyChanged("QueryCacheHitRate"); } } }

    private Int64 _SlowQueries;
    /// <summary>慢查询数</summary>
    [DisplayName("慢查询数")]
    [Description("慢查询数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SlowQueries", "慢查询数", "")]
    public Int64 SlowQueries { get => _SlowQueries; set { if (OnPropertyChanging("SlowQueries", value)) { _SlowQueries = value; OnPropertyChanged("SlowQueries"); } } }

    private Int32 _OpenTables;
    /// <summary>打开表数</summary>
    [DisplayName("打开表数")]
    [Description("打开表数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OpenTables", "打开表数", "")]
    public Int32 OpenTables { get => _OpenTables; set { if (OnPropertyChanging("OpenTables", value)) { _OpenTables = value; OnPropertyChanged("OpenTables"); } } }

    private Int64 _TableLocksWaited;
    /// <summary>表锁等待数</summary>
    [DisplayName("表锁等待数")]
    [Description("表锁等待数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TableLocksWaited", "表锁等待数", "")]
    public Int64 TableLocksWaited { get => _TableLocksWaited; set { if (OnPropertyChanging("TableLocksWaited", value)) { _TableLocksWaited = value; OnPropertyChanged("TableLocksWaited"); } } }

    private Int32 _Threads;
    /// <summary>线程数</summary>
    [DisplayName("线程数")]
    [Description("线程数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Threads", "线程数", "")]
    public Int32 Threads { get => _Threads; set { if (OnPropertyChanging("Threads", value)) { _Threads = value; OnPropertyChanged("Threads"); } } }

    private Int32 _ThreadsRunning;
    /// <summary>运行线程数</summary>
    [DisplayName("运行线程数")]
    [Description("运行线程数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThreadsRunning", "运行线程数", "")]
    public Int32 ThreadsRunning { get => _ThreadsRunning; set { if (OnPropertyChanging("ThreadsRunning", value)) { _ThreadsRunning = value; OnPropertyChanged("ThreadsRunning"); } } }

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
            "MySqlId" => _MySqlId,
            "Name" => _Name,
            "Uptime" => _Uptime,
            "Connections" => _Connections,
            "MaxConnections" => _MaxConnections,
            "Qps" => _Qps,
            "Tps" => _Tps,
            "BytesReceived" => _BytesReceived,
            "BytesSent" => _BytesSent,
            "InnodbBufferPoolSize" => _InnodbBufferPoolSize,
            "InnodbBufferPoolPages" => _InnodbBufferPoolPages,
            "InnodbBufferPoolHitRate" => _InnodbBufferPoolHitRate,
            "KeyCacheHitRate" => _KeyCacheHitRate,
            "QueryCacheHitRate" => _QueryCacheHitRate,
            "SlowQueries" => _SlowQueries,
            "OpenTables" => _OpenTables,
            "TableLocksWaited" => _TableLocksWaited,
            "Threads" => _Threads,
            "ThreadsRunning" => _ThreadsRunning,
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
                case "MySqlId": _MySqlId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Uptime": _Uptime = value.ToInt(); break;
                case "Connections": _Connections = value.ToInt(); break;
                case "MaxConnections": _MaxConnections = value.ToInt(); break;
                case "Qps": _Qps = value.ToInt(); break;
                case "Tps": _Tps = value.ToInt(); break;
                case "BytesReceived": _BytesReceived = value.ToLong(); break;
                case "BytesSent": _BytesSent = value.ToLong(); break;
                case "InnodbBufferPoolSize": _InnodbBufferPoolSize = value.ToLong(); break;
                case "InnodbBufferPoolPages": _InnodbBufferPoolPages = value.ToInt(); break;
                case "InnodbBufferPoolHitRate": _InnodbBufferPoolHitRate = value.ToDouble(); break;
                case "KeyCacheHitRate": _KeyCacheHitRate = value.ToDouble(); break;
                case "QueryCacheHitRate": _QueryCacheHitRate = value.ToDouble(); break;
                case "SlowQueries": _SlowQueries = value.ToLong(); break;
                case "OpenTables": _OpenTables = value.ToInt(); break;
                case "TableLocksWaited": _TableLocksWaited = value.ToLong(); break;
                case "Threads": _Threads = value.ToInt(); break;
                case "ThreadsRunning": _ThreadsRunning = value.ToInt(); break;
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
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static MySqlData FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据MySql节点查找</summary>
    /// <param name="mySqlId">MySql节点</param>
    /// <returns>实体列表</returns>
    public static IList<MySqlData> FindAllByMySqlId(Int32 mySqlId)
    {
        if (mySqlId < 0) return [];

        return FindAll(_.MySqlId == mySqlId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="mySqlId">MySql节点</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<MySqlData> Search(Int32 mySqlId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (mySqlId >= 0) exp &= _.MySqlId == mySqlId;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得MySql数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>MySql节点</summary>
        public static readonly Field MySqlId = FindByName("MySqlId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启动时间。单位秒</summary>
        public static readonly Field Uptime = FindByName("Uptime");

        /// <summary>当前连接数</summary>
        public static readonly Field Connections = FindByName("Connections");

        /// <summary>最大连接数</summary>
        public static readonly Field MaxConnections = FindByName("MaxConnections");

        /// <summary>每秒查询数</summary>
        public static readonly Field Qps = FindByName("Qps");

        /// <summary>每秒事务数</summary>
        public static readonly Field Tps = FindByName("Tps");

        /// <summary>接收字节数</summary>
        public static readonly Field BytesReceived = FindByName("BytesReceived");

        /// <summary>发送字节数</summary>
        public static readonly Field BytesSent = FindByName("BytesSent");

        /// <summary>InnoDB缓冲池大小。单位MB</summary>
        public static readonly Field InnodbBufferPoolSize = FindByName("InnodbBufferPoolSize");

        /// <summary>InnoDB缓冲池页数</summary>
        public static readonly Field InnodbBufferPoolPages = FindByName("InnodbBufferPoolPages");

        /// <summary>InnoDB缓冲池命中率</summary>
        public static readonly Field InnodbBufferPoolHitRate = FindByName("InnodbBufferPoolHitRate");

        /// <summary>键缓存命中率</summary>
        public static readonly Field KeyCacheHitRate = FindByName("KeyCacheHitRate");

        /// <summary>查询缓存命中率</summary>
        public static readonly Field QueryCacheHitRate = FindByName("QueryCacheHitRate");

        /// <summary>慢查询数</summary>
        public static readonly Field SlowQueries = FindByName("SlowQueries");

        /// <summary>打开表数</summary>
        public static readonly Field OpenTables = FindByName("OpenTables");

        /// <summary>表锁等待数</summary>
        public static readonly Field TableLocksWaited = FindByName("TableLocksWaited");

        /// <summary>线程数</summary>
        public static readonly Field Threads = FindByName("Threads");

        /// <summary>运行线程数</summary>
        public static readonly Field ThreadsRunning = FindByName("ThreadsRunning");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得MySql数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>MySql节点</summary>
        public const String MySqlId = "MySqlId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>启动时间。单位秒</summary>
        public const String Uptime = "Uptime";

        /// <summary>当前连接数</summary>
        public const String Connections = "Connections";

        /// <summary>最大连接数</summary>
        public const String MaxConnections = "MaxConnections";

        /// <summary>每秒查询数</summary>
        public const String Qps = "Qps";

        /// <summary>每秒事务数</summary>
        public const String Tps = "Tps";

        /// <summary>接收字节数</summary>
        public const String BytesReceived = "BytesReceived";

        /// <summary>发送字节数</summary>
        public const String BytesSent = "BytesSent";

        /// <summary>InnoDB缓冲池大小。单位MB</summary>
        public const String InnodbBufferPoolSize = "InnodbBufferPoolSize";

        /// <summary>InnoDB缓冲池页数</summary>
        public const String InnodbBufferPoolPages = "InnodbBufferPoolPages";

        /// <summary>InnoDB缓冲池命中率</summary>
        public const String InnodbBufferPoolHitRate = "InnodbBufferPoolHitRate";

        /// <summary>键缓存命中率</summary>
        public const String KeyCacheHitRate = "KeyCacheHitRate";

        /// <summary>查询缓存命中率</summary>
        public const String QueryCacheHitRate = "QueryCacheHitRate";

        /// <summary>慢查询数</summary>
        public const String SlowQueries = "SlowQueries";

        /// <summary>打开表数</summary>
        public const String OpenTables = "OpenTables";

        /// <summary>表锁等待数</summary>
        public const String TableLocksWaited = "TableLocksWaited";

        /// <summary>线程数</summary>
        public const String Threads = "Threads";

        /// <summary>运行线程数</summary>
        public const String ThreadsRunning = "ThreadsRunning";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
