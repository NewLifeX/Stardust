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
    /// <summary>应用服务。应用提供的服务</summary>
    [Serializable]
    [DataObject]
    [Description("应用服务。应用提供的服务")]
    [BindIndex("IX_AppService_AppId", false, "AppId")]
    [BindIndex("IX_AppService_ServiceName", false, "ServiceName")]
    [BindTable("AppService", Description = "应用服务。应用提供的服务", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppService
    {
        #region 属性
        private Int32 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private Int32 _AppId;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private String _ServiceName;
        /// <summary>服务名</summary>
        [DisplayName("服务名")]
        [Description("服务名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ServiceName", "服务名", "")]
        public String ServiceName { get => _ServiceName; set { if (OnPropertyChanging("ServiceName", value)) { _ServiceName = value; OnPropertyChanged("ServiceName"); } } }

        private String _Client;
        /// <summary>客户端。IP加进程</summary>
        [DisplayName("客户端")]
        [Description("客户端。IP加进程")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Client", "客户端。IP加进程", "")]
        public String Client { get => _Client; set { if (OnPropertyChanging("Client", value)) { _Client = value; OnPropertyChanged("Client"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Int32 _PingCount;
        /// <summary>心跳</summary>
        [DisplayName("心跳")]
        [Description("心跳")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("PingCount", "心跳", "")]
        public Int32 PingCount { get => _PingCount; set { if (OnPropertyChanging("PingCount", value)) { _PingCount = value; OnPropertyChanged("PingCount"); } } }

        private String _Version;
        /// <summary>版本</summary>
        [DisplayName("版本")]
        [Description("版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private String _Address;
        /// <summary>地址。服务地址，如http://127.0.0.1:1234</summary>
        [DisplayName("地址")]
        [Description("地址。服务地址，如http://127.0.0.1:1234")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Address", "地址。服务地址，如http://127.0.0.1:1234", "")]
        public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

        private String _HealthCheck;
        /// <summary>健康监测</summary>
        [DisplayName("健康监测")]
        [Description("健康监测")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("HealthCheck", "健康监测", "")]
        public String HealthCheck { get => _HealthCheck; set { if (OnPropertyChanging("HealthCheck", value)) { _HealthCheck = value; OnPropertyChanged("HealthCheck"); } } }

        private Int32 _Weight;
        /// <summary>权重</summary>
        [DisplayName("权重")]
        [Description("权重")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Weight", "权重", "")]
        public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

        private String _Tag;
        /// <summary>标签。带有指定特性，逗号分隔</summary>
        [DisplayName("标签")]
        [Description("标签。带有指定特性，逗号分隔")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Tag", "标签。带有指定特性，逗号分隔", "")]
        public String Tag { get => _Tag; set { if (OnPropertyChanging("Tag", value)) { _Tag = value; OnPropertyChanged("Tag"); } } }

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

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
                    case "ServiceName": return _ServiceName;
                    case "Client": return _Client;
                    case "Enable": return _Enable;
                    case "PingCount": return _PingCount;
                    case "Version": return _Version;
                    case "Address": return _Address;
                    case "HealthCheck": return _HealthCheck;
                    case "Weight": return _Weight;
                    case "Tag": return _Tag;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    case "UpdateTime": return _UpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "Id": _Id = value.ToInt(); break;
                    case "AppId": _AppId = value.ToInt(); break;
                    case "ServiceName": _ServiceName = Convert.ToString(value); break;
                    case "Client": _Client = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "PingCount": _PingCount = value.ToInt(); break;
                    case "Version": _Version = Convert.ToString(value); break;
                    case "Address": _Address = Convert.ToString(value); break;
                    case "HealthCheck": _HealthCheck = Convert.ToString(value); break;
                    case "Weight": _Weight = value.ToInt(); break;
                    case "Tag": _Tag = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得应用服务字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>服务名</summary>
            public static readonly Field ServiceName = FindByName("ServiceName");

            /// <summary>客户端。IP加进程</summary>
            public static readonly Field Client = FindByName("Client");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>心跳</summary>
            public static readonly Field PingCount = FindByName("PingCount");

            /// <summary>版本</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>地址。服务地址，如http://127.0.0.1:1234</summary>
            public static readonly Field Address = FindByName("Address");

            /// <summary>健康监测</summary>
            public static readonly Field HealthCheck = FindByName("HealthCheck");

            /// <summary>权重</summary>
            public static readonly Field Weight = FindByName("Weight");

            /// <summary>标签。带有指定特性，逗号分隔</summary>
            public static readonly Field Tag = FindByName("Tag");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得应用服务字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>服务名</summary>
            public const String ServiceName = "ServiceName";

            /// <summary>客户端。IP加进程</summary>
            public const String Client = "Client";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>心跳</summary>
            public const String PingCount = "PingCount";

            /// <summary>版本</summary>
            public const String Version = "Version";

            /// <summary>地址。服务地址，如http://127.0.0.1:1234</summary>
            public const String Address = "Address";

            /// <summary>健康监测</summary>
            public const String HealthCheck = "HealthCheck";

            /// <summary>权重</summary>
            public const String Weight = "Weight";

            /// <summary>标签。带有指定特性，逗号分隔</summary>
            public const String Tag = "Tag";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";
        }
        #endregion
    }
}