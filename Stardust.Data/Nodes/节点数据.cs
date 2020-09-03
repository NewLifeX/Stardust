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
    /// <summary>节点数据。保存设备上来的一些数据，如心跳状态</summary>
    [Serializable]
    [DataObject]
    [Description("节点数据。保存设备上来的一些数据，如心跳状态")]
    [BindIndex("IX_NodeData_NodeID", false, "NodeID")]
    [BindTable("NodeData", Description = "节点数据。保存设备上来的一些数据，如心跳状态", ConnName = "NodeLog", DbType = DatabaseType.None)]
    public partial class NodeData
    {
        #region 属性
        private Int64 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int64 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

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
        [BindColumn("CpuRate", "CPU率。占用率", "")]
        public Double CpuRate { get => _CpuRate; set { if (OnPropertyChanging("CpuRate", value)) { _CpuRate = value; OnPropertyChanged("CpuRate"); } } }

        private Double _Temperature;
        /// <summary>温度</summary>
        [DisplayName("温度")]
        [Description("温度")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Temperature", "温度", "")]
        public Double Temperature { get => _Temperature; set { if (OnPropertyChanging("Temperature", value)) { _Temperature = value; OnPropertyChanged("Temperature"); } } }

        private Int32 _Delay;
        /// <summary>延迟。网络延迟，单位ms</summary>
        [DisplayName("延迟")]
        [Description("延迟。网络延迟，单位ms")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Delay", "延迟。网络延迟，单位ms", "")]
        public Int32 Delay { get => _Delay; set { if (OnPropertyChanging("Delay", value)) { _Delay = value; OnPropertyChanged("Delay"); } } }

        private Int32 _Offset;
        /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
        [DisplayName("偏移")]
        [Description("偏移。客户端时间减服务端时间，单位s")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Offset", "偏移。客户端时间减服务端时间，单位s", "")]
        public Int32 Offset { get => _Offset; set { if (OnPropertyChanging("Offset", value)) { _Offset = value; OnPropertyChanged("Offset"); } } }

        private DateTime _LocalTime;
        /// <summary>本地时间</summary>
        [DisplayName("本地时间")]
        [Description("本地时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("LocalTime", "本地时间", "")]
        public DateTime LocalTime { get => _LocalTime; set { if (OnPropertyChanging("LocalTime", value)) { _LocalTime = value; OnPropertyChanged("LocalTime"); } } }

        private Int32 _Uptime;
        /// <summary>开机时间。单位ms</summary>
        [DisplayName("开机时间")]
        [Description("开机时间。单位ms")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Uptime", "开机时间。单位ms", "")]
        public Int32 Uptime { get => _Uptime; set { if (OnPropertyChanging("Uptime", value)) { _Uptime = value; OnPropertyChanged("Uptime"); } } }

        private String _Data;
        /// <summary>数据</summary>
        [DisplayName("数据")]
        [Description("数据")]
        [DataObjectField(false, false, true, -1)]
        [BindColumn("Data", "数据", "")]
        public String Data { get => _Data; set { if (OnPropertyChanging("Data", value)) { _Data = value; OnPropertyChanged("Data"); } } }

        private DateTime _CompileTime;
        /// <summary>编译时间</summary>
        [DisplayName("编译时间")]
        [Description("编译时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CompileTime", "编译时间", "")]
        public DateTime CompileTime { get => _CompileTime; set { if (OnPropertyChanging("CompileTime", value)) { _CompileTime = value; OnPropertyChanged("CompileTime"); } } }

        private String _Creator;
        /// <summary>创建者。服务端节点</summary>
        [DisplayName("创建者")]
        [Description("创建者。服务端节点")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Creator", "创建者。服务端节点", "")]
        public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

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
                    case "ID": return _ID;
                    case "NodeID": return _NodeID;
                    case "Name": return _Name;
                    case "AvailableMemory": return _AvailableMemory;
                    case "AvailableFreeSpace": return _AvailableFreeSpace;
                    case "CpuRate": return _CpuRate;
                    case "Temperature": return _Temperature;
                    case "Delay": return _Delay;
                    case "Offset": return _Offset;
                    case "LocalTime": return _LocalTime;
                    case "Uptime": return _Uptime;
                    case "Data": return _Data;
                    case "CompileTime": return _CompileTime;
                    case "Creator": return _Creator;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToLong(); break;
                    case "NodeID": _NodeID = value.ToInt(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "AvailableMemory": _AvailableMemory = value.ToInt(); break;
                    case "AvailableFreeSpace": _AvailableFreeSpace = value.ToInt(); break;
                    case "CpuRate": _CpuRate = value.ToDouble(); break;
                    case "Temperature": _Temperature = value.ToDouble(); break;
                    case "Delay": _Delay = value.ToInt(); break;
                    case "Offset": _Offset = value.ToInt(); break;
                    case "LocalTime": _LocalTime = value.ToDateTime(); break;
                    case "Uptime": _Uptime = value.ToInt(); break;
                    case "Data": _Data = Convert.ToString(value); break;
                    case "CompileTime": _CompileTime = value.ToDateTime(); break;
                    case "Creator": _Creator = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得节点数据字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

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

            /// <summary>延迟。网络延迟，单位ms</summary>
            public static readonly Field Delay = FindByName("Delay");

            /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
            public static readonly Field Offset = FindByName("Offset");

            /// <summary>本地时间</summary>
            public static readonly Field LocalTime = FindByName("LocalTime");

            /// <summary>开机时间。单位ms</summary>
            public static readonly Field Uptime = FindByName("Uptime");

            /// <summary>数据</summary>
            public static readonly Field Data = FindByName("Data");

            /// <summary>编译时间</summary>
            public static readonly Field CompileTime = FindByName("CompileTime");

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
            public const String ID = "ID";

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

            /// <summary>延迟。网络延迟，单位ms</summary>
            public const String Delay = "Delay";

            /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
            public const String Offset = "Offset";

            /// <summary>本地时间</summary>
            public const String LocalTime = "LocalTime";

            /// <summary>开机时间。单位ms</summary>
            public const String Uptime = "Uptime";

            /// <summary>数据</summary>
            public const String Data = "Data";

            /// <summary>编译时间</summary>
            public const String CompileTime = "CompileTime";

            /// <summary>创建者。服务端节点</summary>
            public const String Creator = "Creator";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";
        }
        #endregion
    }
}