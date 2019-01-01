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
    [BindIndex("IU_AppOnline_AppID_Instance", true, "AppID,Instance")]
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

        private String _Instance;
        /// <summary>实例。IP@进程</summary>
        [DisplayName("实例")]
        [Description("实例。IP@进程")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Instance", "实例。IP@进程", "")]
        public String Instance { get { return _Instance; } set { if (OnPropertyChanging(__.Instance, value)) { _Instance = value; OnPropertyChanged(__.Instance); } } }

        private String _Session;
        /// <summary>会话。tcp://ip:port</summary>
        [DisplayName("会话")]
        [Description("会话。tcp://ip:port")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Session", "会话。tcp://ip:port", "")]
        public String Session { get { return _Session; } set { if (OnPropertyChanging(__.Session, value)) { _Session = value; OnPropertyChanged(__.Session); } } }

        private String _Address;
        /// <summary>服务地址。tcp://ip:port</summary>
        [DisplayName("服务地址")]
        [Description("服务地址。tcp://ip:port")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Address", "服务地址。tcp://ip:port", "")]
        public String Address { get { return _Address; } set { if (OnPropertyChanging(__.Address, value)) { _Address = value; OnPropertyChanged(__.Address); } } }

        private String _Version;
        /// <summary>版本</summary>
        [DisplayName("版本")]
        [Description("版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本", "")]
        public String Version { get { return _Version; } set { if (OnPropertyChanging(__.Version, value)) { _Version = value; OnPropertyChanged(__.Version); } } }

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
                    case __.Instance : return _Instance;
                    case __.Session : return _Session;
                    case __.Address : return _Address;
                    case __.Version : return _Version;
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
                    case __.Instance : _Instance = Convert.ToString(value); break;
                    case __.Session : _Session = Convert.ToString(value); break;
                    case __.Address : _Address = Convert.ToString(value); break;
                    case __.Version : _Version = Convert.ToString(value); break;
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

            /// <summary>实例。IP@进程</summary>
            public static readonly Field Instance = FindByName(__.Instance);

            /// <summary>会话。tcp://ip:port</summary>
            public static readonly Field Session = FindByName(__.Session);

            /// <summary>服务地址。tcp://ip:port</summary>
            public static readonly Field Address = FindByName(__.Address);

            /// <summary>版本</summary>
            public static readonly Field Version = FindByName(__.Version);

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

            /// <summary>实例。IP@进程</summary>
            public const String Instance = "Instance";

            /// <summary>会话。tcp://ip:port</summary>
            public const String Session = "Session";

            /// <summary>服务地址。tcp://ip:port</summary>
            public const String Address = "Address";

            /// <summary>版本</summary>
            public const String Version = "Version";

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

        /// <summary>实例。IP@进程</summary>
        String Instance { get; set; }

        /// <summary>会话。tcp://ip:port</summary>
        String Session { get; set; }

        /// <summary>服务地址。tcp://ip:port</summary>
        String Address { get; set; }

        /// <summary>版本</summary>
        String Version { get; set; }

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