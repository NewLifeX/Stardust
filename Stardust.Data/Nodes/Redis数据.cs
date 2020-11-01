using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes
{
    /// <summary>Redis数据。Redis监控</summary>
    [Serializable]
    [DataObject]
    [Description("Redis数据。Redis监控")]
    [BindIndex("IX_RedisData_RedisId_Id", false, "RedisId,Id")]
    [BindTable("RedisData", Description = "Redis数据。Redis监控", ConnName = "NodeLog", DbType = DatabaseType.None)]
    public partial class RedisData
    {
        #region 属性
        private Int64 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("Id", "编号", "")]
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

        private Int32 _InputKbps;
        /// <summary>入流量。单位kbps</summary>
        [DisplayName("入流量")]
        [Description("入流量。单位kbps")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("InputKbps", "入流量。单位kbps", "")]
        public Int32 InputKbps { get => _InputKbps; set { if (OnPropertyChanging("InputKbps", value)) { _InputKbps = value; OnPropertyChanged("InputKbps"); } } }

        private Int32 _OutputKbps;
        /// <summary>出流量。单位kbps</summary>
        [DisplayName("出流量")]
        [Description("出流量。单位kbps")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("OutputKbps", "出流量。单位kbps", "")]
        public Int32 OutputKbps { get => _OutputKbps; set { if (OnPropertyChanging("OutputKbps", value)) { _OutputKbps = value; OnPropertyChanged("OutputKbps"); } } }

        private Int32 _Uptime;
        /// <summary>开始时间。单位秒</summary>
        [DisplayName("开始时间")]
        [Description("开始时间。单位秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Uptime", "开始时间。单位秒", "")]
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

        private Int32 _AvgTtl;
        /// <summary>平均过期。平均过期时间，单位秒</summary>
        [DisplayName("平均过期")]
        [Description("平均过期。平均过期时间，单位秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AvgTtl", "平均过期。平均过期时间，单位秒", "")]
        public Int32 AvgTtl { get => _AvgTtl; set { if (OnPropertyChanging("AvgTtl", value)) { _AvgTtl = value; OnPropertyChanged("AvgTtl"); } } }

        private String _TopCommand;
        /// <summary>最忙命令</summary>
        [DisplayName("最忙命令")]
        [Description("最忙命令")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("TopCommand", "最忙命令", "")]
        public String TopCommand { get => _TopCommand; set { if (OnPropertyChanging("TopCommand", value)) { _TopCommand = value; OnPropertyChanged("TopCommand"); } } }

        private Int64 _Db0Keys;
        /// <summary>db0个数</summary>
        [DisplayName("db0个数")]
        [Description("db0个数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db0Keys", "db0个数", "")]
        public Int64 Db0Keys { get => _Db0Keys; set { if (OnPropertyChanging("Db0Keys", value)) { _Db0Keys = value; OnPropertyChanged("Db0Keys"); } } }

        private Int64 _Db0Expires;
        /// <summary>db0过期</summary>
        [DisplayName("db0过期")]
        [Description("db0过期")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db0Expires", "db0过期", "")]
        public Int64 Db0Expires { get => _Db0Expires; set { if (OnPropertyChanging("Db0Expires", value)) { _Db0Expires = value; OnPropertyChanged("Db0Expires"); } } }

        private Int64 _Db1Keys;
        /// <summary>db1个数</summary>
        [DisplayName("db1个数")]
        [Description("db1个数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db1Keys", "db1个数", "")]
        public Int64 Db1Keys { get => _Db1Keys; set { if (OnPropertyChanging("Db1Keys", value)) { _Db1Keys = value; OnPropertyChanged("Db1Keys"); } } }

        private Int64 _Db1Expires;
        /// <summary>db1过期</summary>
        [DisplayName("db1过期")]
        [Description("db1过期")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db1Expires", "db1过期", "")]
        public Int64 Db1Expires { get => _Db1Expires; set { if (OnPropertyChanging("Db1Expires", value)) { _Db1Expires = value; OnPropertyChanged("Db1Expires"); } } }

        private Int64 _Db2Keys;
        /// <summary>db2个数</summary>
        [DisplayName("db2个数")]
        [Description("db2个数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db2Keys", "db2个数", "")]
        public Int64 Db2Keys { get => _Db2Keys; set { if (OnPropertyChanging("Db2Keys", value)) { _Db2Keys = value; OnPropertyChanged("Db2Keys"); } } }

        private Int64 _Db2Expires;
        /// <summary>db2过期</summary>
        [DisplayName("db2过期")]
        [Description("db2过期")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db2Expires", "db2过期", "")]
        public Int64 Db2Expires { get => _Db2Expires; set { if (OnPropertyChanging("Db2Expires", value)) { _Db2Expires = value; OnPropertyChanged("Db2Expires"); } } }

        private Int64 _Db3Keys;
        /// <summary>db3个数</summary>
        [DisplayName("db3个数")]
        [Description("db3个数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db3Keys", "db3个数", "")]
        public Int64 Db3Keys { get => _Db3Keys; set { if (OnPropertyChanging("Db3Keys", value)) { _Db3Keys = value; OnPropertyChanged("Db3Keys"); } } }

        private Int64 _Db3Expires;
        /// <summary>db3过期</summary>
        [DisplayName("db3过期")]
        [Description("db3过期")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Db3Expires", "db3过期", "")]
        public Int64 Db3Expires { get => _Db3Expires; set { if (OnPropertyChanging("Db3Expires", value)) { _Db3Expires = value; OnPropertyChanged("Db3Expires"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

        private String _Remark;
        /// <summary>备注</summary>
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
            get
            {
                switch (name)
                {
                    case "Id": return _Id;
                    case "RedisId": return _RedisId;
                    case "Name": return _Name;
                    case "Speed": return _Speed;
                    case "InputKbps": return _InputKbps;
                    case "OutputKbps": return _OutputKbps;
                    case "Uptime": return _Uptime;
                    case "ConnectedClients": return _ConnectedClients;
                    case "UsedMemory": return _UsedMemory;
                    case "FragmentationRatio": return _FragmentationRatio;
                    case "Keys": return _Keys;
                    case "ExpiredKeys": return _ExpiredKeys;
                    case "EvictedKeys": return _EvictedKeys;
                    case "KeySpaceHits": return _KeySpaceHits;
                    case "KeySpaceMisses": return _KeySpaceMisses;
                    case "Commands": return _Commands;
                    case "Reads": return _Reads;
                    case "Writes": return _Writes;
                    case "AvgTtl": return _AvgTtl;
                    case "TopCommand": return _TopCommand;
                    case "Db0Keys": return _Db0Keys;
                    case "Db0Expires": return _Db0Expires;
                    case "Db1Keys": return _Db1Keys;
                    case "Db1Expires": return _Db1Expires;
                    case "Db2Keys": return _Db2Keys;
                    case "Db2Expires": return _Db2Expires;
                    case "Db3Keys": return _Db3Keys;
                    case "Db3Expires": return _Db3Expires;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    case "Remark": return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "Id": _Id = value.ToLong(); break;
                    case "RedisId": _RedisId = value.ToInt(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Speed": _Speed = value.ToInt(); break;
                    case "InputKbps": _InputKbps = value.ToInt(); break;
                    case "OutputKbps": _OutputKbps = value.ToInt(); break;
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
                    case "AvgTtl": _AvgTtl = value.ToInt(); break;
                    case "TopCommand": _TopCommand = Convert.ToString(value); break;
                    case "Db0Keys": _Db0Keys = value.ToLong(); break;
                    case "Db0Expires": _Db0Expires = value.ToLong(); break;
                    case "Db1Keys": _Db1Keys = value.ToLong(); break;
                    case "Db1Expires": _Db1Expires = value.ToLong(); break;
                    case "Db2Keys": _Db2Keys = value.ToLong(); break;
                    case "Db2Expires": _Db2Expires = value.ToLong(); break;
                    case "Db3Keys": _Db3Keys = value.ToLong(); break;
                    case "Db3Expires": _Db3Expires = value.ToLong(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "Remark": _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
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

            /// <summary>开始时间。单位秒</summary>
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

            /// <summary>平均过期。平均过期时间，单位秒</summary>
            public static readonly Field AvgTtl = FindByName("AvgTtl");

            /// <summary>最忙命令</summary>
            public static readonly Field TopCommand = FindByName("TopCommand");

            /// <summary>db0个数</summary>
            public static readonly Field Db0Keys = FindByName("Db0Keys");

            /// <summary>db0过期</summary>
            public static readonly Field Db0Expires = FindByName("Db0Expires");

            /// <summary>db1个数</summary>
            public static readonly Field Db1Keys = FindByName("Db1Keys");

            /// <summary>db1过期</summary>
            public static readonly Field Db1Expires = FindByName("Db1Expires");

            /// <summary>db2个数</summary>
            public static readonly Field Db2Keys = FindByName("Db2Keys");

            /// <summary>db2过期</summary>
            public static readonly Field Db2Expires = FindByName("Db2Expires");

            /// <summary>db3个数</summary>
            public static readonly Field Db3Keys = FindByName("Db3Keys");

            /// <summary>db3过期</summary>
            public static readonly Field Db3Expires = FindByName("Db3Expires");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

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

            /// <summary>开始时间。单位秒</summary>
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

            /// <summary>平均过期。平均过期时间，单位秒</summary>
            public const String AvgTtl = "AvgTtl";

            /// <summary>最忙命令</summary>
            public const String TopCommand = "TopCommand";

            /// <summary>db0个数</summary>
            public const String Db0Keys = "Db0Keys";

            /// <summary>db0过期</summary>
            public const String Db0Expires = "Db0Expires";

            /// <summary>db1个数</summary>
            public const String Db1Keys = "Db1Keys";

            /// <summary>db1过期</summary>
            public const String Db1Expires = "Db1Expires";

            /// <summary>db2个数</summary>
            public const String Db2Keys = "Db2Keys";

            /// <summary>db2过期</summary>
            public const String Db2Expires = "Db2Expires";

            /// <summary>db3个数</summary>
            public const String Db3Keys = "Db3Keys";

            /// <summary>db3过期</summary>
            public const String Db3Expires = "Db3Expires";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>备注</summary>
            public const String Remark = "Remark";
        }
        #endregion
    }
}