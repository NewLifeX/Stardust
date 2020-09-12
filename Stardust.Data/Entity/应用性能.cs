using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data
{
    /// <summary>应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等</summary>
    [Serializable]
    [DataObject]
    [Description("应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等")]
    [BindIndex("IX_AppMeter_AppId_Id", false, "AppId,Id")]
    [BindTable("AppMeter", Description = "应用性能。保存应用上报的性能数据，如CPU、内存、线程、句柄等", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppMeter
    {
        #region 属性
        private Int64 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private Int32 _AppId;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private Int32 _Memory;
        /// <summary>内存。单位M</summary>
        [DisplayName("内存")]
        [Description("内存。单位M")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Memory", "内存。单位M", "")]
        public Int32 Memory { get => _Memory; set { if (OnPropertyChanging("Memory", value)) { _Memory = value; OnPropertyChanged("Memory"); } } }

        private Int32 _ProcessorTime;
        /// <summary>处理器。处理器时间，单位ms</summary>
        [DisplayName("处理器")]
        [Description("处理器。处理器时间，单位ms")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ProcessorTime", "处理器。处理器时间，单位ms", "")]
        public Int32 ProcessorTime { get => _ProcessorTime; set { if (OnPropertyChanging("ProcessorTime", value)) { _ProcessorTime = value; OnPropertyChanged("ProcessorTime"); } } }

        private Int32 _Threads;
        /// <summary>线程数</summary>
        [DisplayName("线程数")]
        [Description("线程数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Threads", "线程数", "")]
        public Int32 Threads { get => _Threads; set { if (OnPropertyChanging("Threads", value)) { _Threads = value; OnPropertyChanged("Threads"); } } }

        private Int32 _Handles;
        /// <summary>句柄数</summary>
        [DisplayName("句柄数")]
        [Description("句柄数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Handles", "句柄数", "")]
        public Int32 Handles { get => _Handles; set { if (OnPropertyChanging("Handles", value)) { _Handles = value; OnPropertyChanged("Handles"); } } }

        private Int32 _Connects;
        /// <summary>连接数</summary>
        [DisplayName("连接数")]
        [Description("连接数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Connects", "连接数", "")]
        public Int32 Connects { get => _Connects; set { if (OnPropertyChanging("Connects", value)) { _Connects = value; OnPropertyChanged("Connects"); } } }

        private String _Data;
        /// <summary>数据</summary>
        [DisplayName("数据")]
        [Description("数据")]
        [DataObjectField(false, false, true, -1)]
        [BindColumn("Data", "数据", "")]
        public String Data { get => _Data; set { if (OnPropertyChanging("Data", value)) { _Data = value; OnPropertyChanged("Data"); } } }

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
                    case "Id": return _Id;
                    case "AppId": return _AppId;
                    case "Name": return _Name;
                    case "Memory": return _Memory;
                    case "ProcessorTime": return _ProcessorTime;
                    case "Threads": return _Threads;
                    case "Handles": return _Handles;
                    case "Connects": return _Connects;
                    case "Data": return _Data;
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
                    case "Id": _Id = value.ToLong(); break;
                    case "AppId": _AppId = value.ToInt(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Memory": _Memory = value.ToInt(); break;
                    case "ProcessorTime": _ProcessorTime = value.ToInt(); break;
                    case "Threads": _Threads = value.ToInt(); break;
                    case "Handles": _Handles = value.ToInt(); break;
                    case "Connects": _Connects = value.ToInt(); break;
                    case "Data": _Data = Convert.ToString(value); break;
                    case "Creator": _Creator = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
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

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>内存。单位M</summary>
            public static readonly Field Memory = FindByName("Memory");

            /// <summary>处理器。处理器时间，单位ms</summary>
            public static readonly Field ProcessorTime = FindByName("ProcessorTime");

            /// <summary>线程数</summary>
            public static readonly Field Threads = FindByName("Threads");

            /// <summary>句柄数</summary>
            public static readonly Field Handles = FindByName("Handles");

            /// <summary>连接数</summary>
            public static readonly Field Connects = FindByName("Connects");

            /// <summary>数据</summary>
            public static readonly Field Data = FindByName("Data");

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

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>内存。单位M</summary>
            public const String Memory = "Memory";

            /// <summary>处理器。处理器时间，单位ms</summary>
            public const String ProcessorTime = "ProcessorTime";

            /// <summary>线程数</summary>
            public const String Threads = "Threads";

            /// <summary>句柄数</summary>
            public const String Handles = "Handles";

            /// <summary>连接数</summary>
            public const String Connects = "Connects";

            /// <summary>数据</summary>
            public const String Data = "Data";

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