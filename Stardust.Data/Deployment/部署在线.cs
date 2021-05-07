using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Deployment
{
    /// <summary>部署在线。应用已部署的在运行中进程的在线记录</summary>
    [Serializable]
    [DataObject]
    [Description("部署在线。应用已部署的在运行中进程的在线记录")]
    [BindIndex("IX_AppDeployOnline_AppId_NodeId", false, "AppId,NodeId")]
    [BindIndex("IX_AppDeployOnline_AppId", false, "AppId")]
    [BindIndex("IX_AppDeployOnline_NodeId", false, "NodeId")]
    [BindTable("AppDeployOnline", Description = "部署在线。应用已部署的在运行中进程的在线记录", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppDeployOnline
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

        private Int32 _NodeId;
        /// <summary>节点。节点服务器</summary>
        [DisplayName("节点")]
        [Description("节点。节点服务器")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("NodeId", "节点。节点服务器", "")]
        public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

        private String _Environment;
        /// <summary>环境。prod/test/dev/uat等</summary>
        [DisplayName("环境")]
        [Description("环境。prod/test/dev/uat等")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Environment", "环境。prod/test/dev/uat等", "")]
        public String Environment { get => _Environment; set { if (OnPropertyChanging("Environment", value)) { _Environment = value; OnPropertyChanged("Environment"); } } }

        private String _IP;
        /// <summary>IP地址。节点本地IP地址</summary>
        [DisplayName("IP地址")]
        [Description("IP地址。节点本地IP地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("IP", "IP地址。节点本地IP地址", "")]
        public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

        private Int32 _ProcessId;
        /// <summary>进程。应用在该节点上的进程</summary>
        [DisplayName("进程")]
        [Description("进程。应用在该节点上的进程")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ProcessId", "进程。应用在该节点上的进程", "")]
        public Int32 ProcessId { get => _ProcessId; set { if (OnPropertyChanging("ProcessId", value)) { _ProcessId = value; OnPropertyChanged("ProcessId"); } } }

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
                    case "NodeId": return _NodeId;
                    case "Environment": return _Environment;
                    case "IP": return _IP;
                    case "ProcessId": return _ProcessId;
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
                    case "NodeId": _NodeId = value.ToInt(); break;
                    case "Environment": _Environment = Convert.ToString(value); break;
                    case "IP": _IP = Convert.ToString(value); break;
                    case "ProcessId": _ProcessId = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得部署在线字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>节点。节点服务器</summary>
            public static readonly Field NodeId = FindByName("NodeId");

            /// <summary>环境。prod/test/dev/uat等</summary>
            public static readonly Field Environment = FindByName("Environment");

            /// <summary>IP地址。节点本地IP地址</summary>
            public static readonly Field IP = FindByName("IP");

            /// <summary>进程。应用在该节点上的进程</summary>
            public static readonly Field ProcessId = FindByName("ProcessId");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得部署在线字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>节点。节点服务器</summary>
            public const String NodeId = "NodeId";

            /// <summary>环境。prod/test/dev/uat等</summary>
            public const String Environment = "Environment";

            /// <summary>IP地址。节点本地IP地址</summary>
            public const String IP = "IP";

            /// <summary>进程。应用在该节点上的进程</summary>
            public const String ProcessId = "ProcessId";

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