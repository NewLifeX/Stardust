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
    /// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
    [Serializable]
    [DataObject]
    [Description("应用在线。一个应用有多个部署，每个在线会话对应一个服务地址")]
    [BindIndex("IU_AppOnline_Session", true, "Session")]
    [BindIndex("IX_AppOnline_Client", false, "Client")]
    [BindIndex("IX_AppOnline_AppID", false, "AppID")]
    [BindTable("AppOnline", Description = "应用在线。一个应用有多个部署，每个在线会话对应一个服务地址", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppOnline
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

        private Int32 _AppID;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppID", "应用", "")]
        public Int32 AppID { get => _AppID; set { if (OnPropertyChanging("AppID", value)) { _AppID = value; OnPropertyChanged("AppID"); } } }

        private String _Session;
        /// <summary>实例。IP加端口</summary>
        [DisplayName("实例")]
        [Description("实例。IP加端口")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Session", "实例。IP加端口", "")]
        public String Session { get => _Session; set { if (OnPropertyChanging("Session", value)) { _Session = value; OnPropertyChanged("Session"); } } }

        private String _Client;
        /// <summary>客户端。IP加进程</summary>
        [DisplayName("客户端")]
        [Description("客户端。IP加进程")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Client", "客户端。IP加进程", "")]
        public String Client { get => _Client; set { if (OnPropertyChanging("Client", value)) { _Client = value; OnPropertyChanged("Client"); } } }

        private String _Name;
        /// <summary>名称。机器名称</summary>
        [DisplayName("名称")]
        [Description("名称。机器名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称。机器名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private Int32 _ProcessId;
        /// <summary>进程Id</summary>
        [DisplayName("进程Id")]
        [Description("进程Id")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ProcessId", "进程Id", "")]
        public Int32 ProcessId { get => _ProcessId; set { if (OnPropertyChanging("ProcessId", value)) { _ProcessId = value; OnPropertyChanged("ProcessId"); } } }

        private String _ProcessName;
        /// <summary>进程名称</summary>
        [DisplayName("进程名称")]
        [Description("进程名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ProcessName", "进程名称", "")]
        public String ProcessName { get => _ProcessName; set { if (OnPropertyChanging("ProcessName", value)) { _ProcessName = value; OnPropertyChanged("ProcessName"); } } }

        private DateTime _StartTime;
        /// <summary>进程时间</summary>
        [DisplayName("进程时间")]
        [Description("进程时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("StartTime", "进程时间", "")]
        public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

        private String _Version;
        /// <summary>版本。客户端</summary>
        [DisplayName("版本")]
        [Description("版本。客户端")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本。客户端", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private DateTime _Compile;
        /// <summary>编译时间。客户端</summary>
        [DisplayName("编译时间")]
        [Description("编译时间。客户端")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("Compile", "编译时间。客户端", "")]
        public DateTime Compile { get => _Compile; set { if (OnPropertyChanging("Compile", value)) { _Compile = value; OnPropertyChanged("Compile"); } } }

        private String _Server;
        /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
        [DisplayName("服务端")]
        [Description("服务端。客户端登录到哪个服务端，IP加端口")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Server", "服务端。客户端登录到哪个服务端，IP加端口", "")]
        public String Server { get => _Server; set { if (OnPropertyChanging("Server", value)) { _Server = value; OnPropertyChanged("Server"); } } }

        private Boolean _Active;
        /// <summary>激活。只有激活的应用，才提供服务</summary>
        [DisplayName("激活")]
        [Description("激活。只有激活的应用，才提供服务")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Active", "激活。只有激活的应用，才提供服务", "")]
        public Boolean Active { get => _Active; set { if (OnPropertyChanging("Active", value)) { _Active = value; OnPropertyChanged("Active"); } } }

        private String _Address;
        /// <summary>服务地址。tcp://ip:port</summary>
        [DisplayName("服务地址")]
        [Description("服务地址。tcp://ip:port")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Address", "服务地址。tcp://ip:port", "")]
        public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

        private Int32 _Services;
        /// <summary>服务数。该应用提供的服务数</summary>
        [DisplayName("服务数")]
        [Description("服务数。该应用提供的服务数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Services", "服务数。该应用提供的服务数", "")]
        public Int32 Services { get => _Services; set { if (OnPropertyChanging("Services", value)) { _Services = value; OnPropertyChanged("Services"); } } }

        private String _Actions;
        /// <summary>功能列表</summary>
        [DisplayName("功能列表")]
        [Description("功能列表")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Actions", "功能列表", "")]
        public String Actions { get => _Actions; set { if (OnPropertyChanging("Actions", value)) { _Actions = value; OnPropertyChanged("Actions"); } } }

        private Int32 _Clients;
        /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
        [DisplayName("客户端数")]
        [Description("客户端数。服务提供者当前服务的客户端数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Clients", "客户端数。服务提供者当前服务的客户端数", "")]
        public Int32 Clients { get => _Clients; set { if (OnPropertyChanging("Clients", value)) { _Clients = value; OnPropertyChanged("Clients"); } } }

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
                    case "ID": return _ID;
                    case "AppID": return _AppID;
                    case "Session": return _Session;
                    case "Client": return _Client;
                    case "Name": return _Name;
                    case "ProcessId": return _ProcessId;
                    case "ProcessName": return _ProcessName;
                    case "StartTime": return _StartTime;
                    case "Version": return _Version;
                    case "Compile": return _Compile;
                    case "Server": return _Server;
                    case "Active": return _Active;
                    case "Address": return _Address;
                    case "Services": return _Services;
                    case "Actions": return _Actions;
                    case "Clients": return _Clients;
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
                    case "ID": _ID = value.ToInt(); break;
                    case "AppID": _AppID = value.ToInt(); break;
                    case "Session": _Session = Convert.ToString(value); break;
                    case "Client": _Client = Convert.ToString(value); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "ProcessId": _ProcessId = value.ToInt(); break;
                    case "ProcessName": _ProcessName = Convert.ToString(value); break;
                    case "StartTime": _StartTime = value.ToDateTime(); break;
                    case "Version": _Version = Convert.ToString(value); break;
                    case "Compile": _Compile = value.ToDateTime(); break;
                    case "Server": _Server = Convert.ToString(value); break;
                    case "Active": _Active = value.ToBoolean(); break;
                    case "Address": _Address = Convert.ToString(value); break;
                    case "Services": _Services = value.ToInt(); break;
                    case "Actions": _Actions = Convert.ToString(value); break;
                    case "Clients": _Clients = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得应用在线字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>应用</summary>
            public static readonly Field AppID = FindByName("AppID");

            /// <summary>实例。IP加端口</summary>
            public static readonly Field Session = FindByName("Session");

            /// <summary>客户端。IP加进程</summary>
            public static readonly Field Client = FindByName("Client");

            /// <summary>名称。机器名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>进程Id</summary>
            public static readonly Field ProcessId = FindByName("ProcessId");

            /// <summary>进程名称</summary>
            public static readonly Field ProcessName = FindByName("ProcessName");

            /// <summary>进程时间</summary>
            public static readonly Field StartTime = FindByName("StartTime");

            /// <summary>版本。客户端</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>编译时间。客户端</summary>
            public static readonly Field Compile = FindByName("Compile");

            /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
            public static readonly Field Server = FindByName("Server");

            /// <summary>激活。只有激活的应用，才提供服务</summary>
            public static readonly Field Active = FindByName("Active");

            /// <summary>服务地址。tcp://ip:port</summary>
            public static readonly Field Address = FindByName("Address");

            /// <summary>服务数。该应用提供的服务数</summary>
            public static readonly Field Services = FindByName("Services");

            /// <summary>功能列表</summary>
            public static readonly Field Actions = FindByName("Actions");

            /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
            public static readonly Field Clients = FindByName("Clients");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得应用在线字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>应用</summary>
            public const String AppID = "AppID";

            /// <summary>实例。IP加端口</summary>
            public const String Session = "Session";

            /// <summary>客户端。IP加进程</summary>
            public const String Client = "Client";

            /// <summary>名称。机器名称</summary>
            public const String Name = "Name";

            /// <summary>进程Id</summary>
            public const String ProcessId = "ProcessId";

            /// <summary>进程名称</summary>
            public const String ProcessName = "ProcessName";

            /// <summary>进程时间</summary>
            public const String StartTime = "StartTime";

            /// <summary>版本。客户端</summary>
            public const String Version = "Version";

            /// <summary>编译时间。客户端</summary>
            public const String Compile = "Compile";

            /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
            public const String Server = "Server";

            /// <summary>激活。只有激活的应用，才提供服务</summary>
            public const String Active = "Active";

            /// <summary>服务地址。tcp://ip:port</summary>
            public const String Address = "Address";

            /// <summary>服务数。该应用提供的服务数</summary>
            public const String Services = "Services";

            /// <summary>功能列表</summary>
            public const String Actions = "Actions";

            /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
            public const String Clients = "Clients";

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