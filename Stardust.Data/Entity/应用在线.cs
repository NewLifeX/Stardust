using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    [BindTable("AppOnline", Description = "应用在线。一个应用有多个部署，每个在线会话对应一个服务地址", ConnName = "Registry", DbType = DatabaseType.None)]
    public partial class AppOnline : IAppOnline
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get { return _ID; } set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private Int32 _AppID;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppID", "应用", "")]
        public Int32 AppID { get { return _AppID; } set { if (OnPropertyChanging(__.AppID, value)) { _AppID = value; OnPropertyChanged(__.AppID); } } }

        private String _Session;
        /// <summary>实例。IP加端口</summary>
        [DisplayName("实例")]
        [Description("实例。IP加端口")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Session", "实例。IP加端口", "")]
        public String Session { get { return _Session; } set { if (OnPropertyChanging(__.Session, value)) { _Session = value; OnPropertyChanged(__.Session); } } }

        private String _Client;
        /// <summary>客户端。IP加进程</summary>
        [DisplayName("客户端")]
        [Description("客户端。IP加进程")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Client", "客户端。IP加进程", "")]
        public String Client { get { return _Client; } set { if (OnPropertyChanging(__.Client, value)) { _Client = value; OnPropertyChanged(__.Client); } } }

        private String _Name;
        /// <summary>名称。机器名称</summary>
        [DisplayName("名称")]
        [Description("名称。机器名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称。机器名称", "", Master = true)]
        public String Name { get { return _Name; } set { if (OnPropertyChanging(__.Name, value)) { _Name = value; OnPropertyChanged(__.Name); } } }

        private String _Version;
        /// <summary>版本。客户端</summary>
        [DisplayName("版本")]
        [Description("版本。客户端")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本。客户端", "")]
        public String Version { get { return _Version; } set { if (OnPropertyChanging(__.Version, value)) { _Version = value; OnPropertyChanged(__.Version); } } }

        private DateTime _Compile;
        /// <summary>编译时间。客户端</summary>
        [DisplayName("编译时间")]
        [Description("编译时间。客户端")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("Compile", "编译时间。客户端", "")]
        public DateTime Compile { get { return _Compile; } set { if (OnPropertyChanging(__.Compile, value)) { _Compile = value; OnPropertyChanged(__.Compile); } } }

        private String _Server;
        /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
        [DisplayName("服务端")]
        [Description("服务端。客户端登录到哪个服务端，IP加端口")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Server", "服务端。客户端登录到哪个服务端，IP加端口", "")]
        public String Server { get { return _Server; } set { if (OnPropertyChanging(__.Server, value)) { _Server = value; OnPropertyChanged(__.Server); } } }

        private String _Address;
        /// <summary>服务地址。tcp://ip:port</summary>
        [DisplayName("服务地址")]
        [Description("服务地址。tcp://ip:port")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Address", "服务地址。tcp://ip:port", "")]
        public String Address { get { return _Address; } set { if (OnPropertyChanging(__.Address, value)) { _Address = value; OnPropertyChanged(__.Address); } } }

        private Int32 _Services;
        /// <summary>服务数。该应用提供的服务数</summary>
        [DisplayName("服务数")]
        [Description("服务数。该应用提供的服务数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Services", "服务数。该应用提供的服务数", "")]
        public Int32 Services { get { return _Services; } set { if (OnPropertyChanging(__.Services, value)) { _Services = value; OnPropertyChanged(__.Services); } } }

        private String _Actions;
        /// <summary>功能列表</summary>
        [DisplayName("功能列表")]
        [Description("功能列表")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Actions", "功能列表", "")]
        public String Actions { get { return _Actions; } set { if (OnPropertyChanging(__.Actions, value)) { _Actions = value; OnPropertyChanged(__.Actions); } } }

        private Int32 _Clients;
        /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
        [DisplayName("客户端数")]
        [Description("客户端数。服务提供者当前服务的客户端数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Clients", "客户端数。服务提供者当前服务的客户端数", "")]
        public Int32 Clients { get { return _Clients; } set { if (OnPropertyChanging(__.Clients, value)) { _Clients = value; OnPropertyChanged(__.Clients); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get { return _CreateTime; } set { if (OnPropertyChanging(__.CreateTime, value)) { _CreateTime = value; OnPropertyChanged(__.CreateTime); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get { return _CreateIP; } set { if (OnPropertyChanging(__.CreateIP, value)) { _CreateIP = value; OnPropertyChanged(__.CreateIP); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get { return _UpdateTime; } set { if (OnPropertyChanging(__.UpdateTime, value)) { _UpdateTime = value; OnPropertyChanged(__.UpdateTime); } } }
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
                    case __.ID : return _ID;
                    case __.AppID : return _AppID;
                    case __.Session : return _Session;
                    case __.Client : return _Client;
                    case __.Name : return _Name;
                    case __.Version : return _Version;
                    case __.Compile : return _Compile;
                    case __.Server : return _Server;
                    case __.Address : return _Address;
                    case __.Services : return _Services;
                    case __.Actions : return _Actions;
                    case __.Clients : return _Clients;
                    case __.CreateTime : return _CreateTime;
                    case __.CreateIP : return _CreateIP;
                    case __.UpdateTime : return _UpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID : _ID = Convert.ToInt32(value); break;
                    case __.AppID : _AppID = Convert.ToInt32(value); break;
                    case __.Session : _Session = Convert.ToString(value); break;
                    case __.Client : _Client = Convert.ToString(value); break;
                    case __.Name : _Name = Convert.ToString(value); break;
                    case __.Version : _Version = Convert.ToString(value); break;
                    case __.Compile : _Compile = Convert.ToDateTime(value); break;
                    case __.Server : _Server = Convert.ToString(value); break;
                    case __.Address : _Address = Convert.ToString(value); break;
                    case __.Services : _Services = Convert.ToInt32(value); break;
                    case __.Actions : _Actions = Convert.ToString(value); break;
                    case __.Clients : _Clients = Convert.ToInt32(value); break;
                    case __.CreateTime : _CreateTime = Convert.ToDateTime(value); break;
                    case __.CreateIP : _CreateIP = Convert.ToString(value); break;
                    case __.UpdateTime : _UpdateTime = Convert.ToDateTime(value); break;
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
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>应用</summary>
            public static readonly Field AppID = FindByName(__.AppID);

            /// <summary>实例。IP加端口</summary>
            public static readonly Field Session = FindByName(__.Session);

            /// <summary>客户端。IP加进程</summary>
            public static readonly Field Client = FindByName(__.Client);

            /// <summary>名称。机器名称</summary>
            public static readonly Field Name = FindByName(__.Name);

            /// <summary>版本。客户端</summary>
            public static readonly Field Version = FindByName(__.Version);

            /// <summary>编译时间。客户端</summary>
            public static readonly Field Compile = FindByName(__.Compile);

            /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
            public static readonly Field Server = FindByName(__.Server);

            /// <summary>服务地址。tcp://ip:port</summary>
            public static readonly Field Address = FindByName(__.Address);

            /// <summary>服务数。该应用提供的服务数</summary>
            public static readonly Field Services = FindByName(__.Services);

            /// <summary>功能列表</summary>
            public static readonly Field Actions = FindByName(__.Actions);

            /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
            public static readonly Field Clients = FindByName(__.Clients);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName(__.CreateIP);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
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

            /// <summary>版本。客户端</summary>
            public const String Version = "Version";

            /// <summary>编译时间。客户端</summary>
            public const String Compile = "Compile";

            /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
            public const String Server = "Server";

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

    /// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址接口</summary>
    public partial interface IAppOnline
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>应用</summary>
        Int32 AppID { get; set; }

        /// <summary>实例。IP加端口</summary>
        String Session { get; set; }

        /// <summary>客户端。IP加进程</summary>
        String Client { get; set; }

        /// <summary>名称。机器名称</summary>
        String Name { get; set; }

        /// <summary>版本。客户端</summary>
        String Version { get; set; }

        /// <summary>编译时间。客户端</summary>
        DateTime Compile { get; set; }

        /// <summary>服务端。客户端登录到哪个服务端，IP加端口</summary>
        String Server { get; set; }

        /// <summary>服务地址。tcp://ip:port</summary>
        String Address { get; set; }

        /// <summary>服务数。该应用提供的服务数</summary>
        Int32 Services { get; set; }

        /// <summary>功能列表</summary>
        String Actions { get; set; }

        /// <summary>客户端数。服务提供者当前服务的客户端数</summary>
        Int32 Clients { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>创建地址</summary>
        String CreateIP { get; set; }

        /// <summary>更新时间</summary>
        DateTime UpdateTime { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}