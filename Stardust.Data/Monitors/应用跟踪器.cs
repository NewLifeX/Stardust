using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Monitors
{
    /// <summary>应用跟踪器。负责追踪的应用管理和参数设置</summary>
    [Serializable]
    [DataObject]
    [Description("应用跟踪器。负责追踪的应用管理和参数设置")]
    [BindIndex("IU_AppTracer_Name", true, "Name")]
    [BindIndex("IX_AppTracer_AppId", false, "AppId")]
    [BindTable("AppTracer", Description = "应用跟踪器。负责追踪的应用管理和参数设置", ConnName = "Monitor", DbType = DatabaseType.None)]
    public partial class AppTracer
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

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
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private String _DisplayName;
        /// <summary>显示名</summary>
        [DisplayName("显示名")]
        [Description("显示名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("DisplayName", "显示名", "")]
        public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

        private String _Category;
        /// <summary>类别</summary>
        [DisplayName("类别")]
        [Description("类别")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Category", "类别", "")]
        public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

        private Int32 _ItemCount;
        /// <summary>跟踪项。共有多少个埋点</summary>
        [DisplayName("跟踪项")]
        [Description("跟踪项。共有多少个埋点")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ItemCount", "跟踪项。共有多少个埋点", "")]
        public Int32 ItemCount { get => _ItemCount; set { if (OnPropertyChanging("ItemCount", value)) { _ItemCount = value; OnPropertyChanged("ItemCount"); } } }

        private Int32 _Days;
        /// <summary>天数。共统计了多少天</summary>
        [DisplayName("天数")]
        [Description("天数。共统计了多少天")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Days", "天数。共统计了多少天", "")]
        public Int32 Days { get => _Days; set { if (OnPropertyChanging("Days", value)) { _Days = value; OnPropertyChanged("Days"); } } }

        private Int64 _Total;
        /// <summary>总次数。累计埋点采样次数</summary>
        [DisplayName("总次数")]
        [Description("总次数。累计埋点采样次数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Total", "总次数。累计埋点采样次数", "")]
        public Int64 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private TraceModes _Mode;
        /// <summary>跟踪模式。仅针对api类型，过滤被扫描的数据</summary>
        [DisplayName("跟踪模式")]
        [Description("跟踪模式。仅针对api类型，过滤被扫描的数据")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Mode", "跟踪模式。仅针对api类型，过滤被扫描的数据", "")]
        public TraceModes Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

        private Int32 _Period;
        /// <summary>采样周期。单位秒</summary>
        [DisplayName("采样周期")]
        [Description("采样周期。单位秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Period", "采样周期。单位秒", "")]
        public Int32 Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

        private Int32 _MaxSamples;
        /// <summary>正常数。最大正常采样数，采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
        [DisplayName("正常数")]
        [Description("正常数。最大正常采样数，采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxSamples", "正常数。最大正常采样数，采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系", "")]
        public Int32 MaxSamples { get => _MaxSamples; set { if (OnPropertyChanging("MaxSamples", value)) { _MaxSamples = value; OnPropertyChanged("MaxSamples"); } } }

        private Int32 _MaxErrors;
        /// <summary>异常数。最大异常采样数，采样周期内，最多只记录指定数量的异常事件，默认10</summary>
        [DisplayName("异常数")]
        [Description("异常数。最大异常采样数，采样周期内，最多只记录指定数量的异常事件，默认10")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxErrors", "异常数。最大异常采样数，采样周期内，最多只记录指定数量的异常事件，默认10", "")]
        public Int32 MaxErrors { get => _MaxErrors; set { if (OnPropertyChanging("MaxErrors", value)) { _MaxErrors = value; OnPropertyChanged("MaxErrors"); } } }

        private String _Excludes;
        /// <summary>排除项。要排除的操作名，支持*模糊匹配</summary>
        [DisplayName("排除项")]
        [Description("排除项。要排除的操作名，支持*模糊匹配")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Excludes", "排除项。要排除的操作名，支持*模糊匹配", "")]
        public String Excludes { get => _Excludes; set { if (OnPropertyChanging("Excludes", value)) { _Excludes = value; OnPropertyChanged("Excludes"); } } }

        private Int32 _Timeout;
        /// <summary>超时时间。超过该时间时强制采样，默认5000毫秒</summary>
        [DisplayName("超时时间")]
        [Description("超时时间。超过该时间时强制采样，默认5000毫秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Timeout", "超时时间。超过该时间时强制采样，默认5000毫秒", "")]
        public Int32 Timeout { get => _Timeout; set { if (OnPropertyChanging("Timeout", value)) { _Timeout = value; OnPropertyChanged("Timeout"); } } }

        private String _TimeoutExcludes;
        /// <summary>超时排除项。不判断超时的操作名，支持*模糊匹配</summary>
        [DisplayName("超时排除项")]
        [Description("超时排除项。不判断超时的操作名，支持*模糊匹配")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("TimeoutExcludes", "超时排除项。不判断超时的操作名，支持*模糊匹配", "")]
        public String TimeoutExcludes { get => _TimeoutExcludes; set { if (OnPropertyChanging("TimeoutExcludes", value)) { _TimeoutExcludes = value; OnPropertyChanged("TimeoutExcludes"); } } }

        private String _VipClients;
        /// <summary>Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配</summary>
        [DisplayName("Vip客户端")]
        [Description("Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("VipClients", "Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配", "")]
        public String VipClients { get => _VipClients; set { if (OnPropertyChanging("VipClients", value)) { _VipClients = value; OnPropertyChanged("VipClients"); } } }

        private String _Nodes;
        /// <summary>节点集。该应用最近一段时间所涉及的来源地址</summary>
        [DisplayName("节点集")]
        [Description("节点集。该应用最近一段时间所涉及的来源地址")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Nodes", "节点集。该应用最近一段时间所涉及的来源地址", "")]
        public String Nodes { get => _Nodes; set { if (OnPropertyChanging("Nodes", value)) { _Nodes = value; OnPropertyChanged("Nodes"); } } }

        private Int32 _AlarmThreshold;
        /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用</summary>
        [DisplayName("告警阈值")]
        [Description("告警阈值。错误数达到该值时触发告警，0表示不启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmThreshold", "告警阈值。错误数达到该值时触发告警，0表示不启用", "")]
        public Int32 AlarmThreshold { get => _AlarmThreshold; set { if (OnPropertyChanging("AlarmThreshold", value)) { _AlarmThreshold = value; OnPropertyChanged("AlarmThreshold"); } } }

        private String _AlarmRobot;
        /// <summary>告警机器人。钉钉、企业微信等</summary>
        [DisplayName("告警机器人")]
        [Description("告警机器人。钉钉、企业微信等")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("AlarmRobot", "告警机器人。钉钉、企业微信等", "")]
        public String AlarmRobot { get => _AlarmRobot; set { if (OnPropertyChanging("AlarmRobot", value)) { _AlarmRobot = value; OnPropertyChanged("AlarmRobot"); } } }

        private String _CreateUser;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateUser", "创建者", "")]
        public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

        private Int32 _CreateUserID;
        /// <summary>创建人</summary>
        [DisplayName("创建人")]
        [Description("创建人")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserID", "创建人", "")]
        public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

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

        private String _UpdateUser;
        /// <summary>更新者</summary>
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateUser", "更新者", "")]
        public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

        private Int32 _UpdateUserID;
        /// <summary>更新人</summary>
        [DisplayName("更新人")]
        [Description("更新人")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("UpdateUserID", "更新人", "")]
        public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

        private String _UpdateIP;
        /// <summary>更新地址</summary>
        [DisplayName("更新地址")]
        [Description("更新地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateIP", "更新地址", "")]
        public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }
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
                    case "AppId": return _AppId;
                    case "Name": return _Name;
                    case "DisplayName": return _DisplayName;
                    case "Category": return _Category;
                    case "ItemCount": return _ItemCount;
                    case "Days": return _Days;
                    case "Total": return _Total;
                    case "Enable": return _Enable;
                    case "Mode": return _Mode;
                    case "Period": return _Period;
                    case "MaxSamples": return _MaxSamples;
                    case "MaxErrors": return _MaxErrors;
                    case "Excludes": return _Excludes;
                    case "Timeout": return _Timeout;
                    case "TimeoutExcludes": return _TimeoutExcludes;
                    case "VipClients": return _VipClients;
                    case "Nodes": return _Nodes;
                    case "AlarmThreshold": return _AlarmThreshold;
                    case "AlarmRobot": return _AlarmRobot;
                    case "CreateUser": return _CreateUser;
                    case "CreateUserID": return _CreateUserID;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    case "UpdateUser": return _UpdateUser;
                    case "UpdateUserID": return _UpdateUserID;
                    case "UpdateTime": return _UpdateTime;
                    case "UpdateIP": return _UpdateIP;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToInt(); break;
                    case "AppId": _AppId = value.ToInt(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "DisplayName": _DisplayName = Convert.ToString(value); break;
                    case "Category": _Category = Convert.ToString(value); break;
                    case "ItemCount": _ItemCount = value.ToInt(); break;
                    case "Days": _Days = value.ToInt(); break;
                    case "Total": _Total = value.ToLong(); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Mode": _Mode = (TraceModes)value.ToInt(); break;
                    case "Period": _Period = value.ToInt(); break;
                    case "MaxSamples": _MaxSamples = value.ToInt(); break;
                    case "MaxErrors": _MaxErrors = value.ToInt(); break;
                    case "Excludes": _Excludes = Convert.ToString(value); break;
                    case "Timeout": _Timeout = value.ToInt(); break;
                    case "TimeoutExcludes": _TimeoutExcludes = Convert.ToString(value); break;
                    case "VipClients": _VipClients = Convert.ToString(value); break;
                    case "Nodes": _Nodes = Convert.ToString(value); break;
                    case "AlarmThreshold": _AlarmThreshold = value.ToInt(); break;
                    case "AlarmRobot": _AlarmRobot = Convert.ToString(value); break;
                    case "CreateUser": _CreateUser = Convert.ToString(value); break;
                    case "CreateUserID": _CreateUserID = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                    case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得应用跟踪器字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>显示名</summary>
            public static readonly Field DisplayName = FindByName("DisplayName");

            /// <summary>类别</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>跟踪项。共有多少个埋点</summary>
            public static readonly Field ItemCount = FindByName("ItemCount");

            /// <summary>天数。共统计了多少天</summary>
            public static readonly Field Days = FindByName("Days");

            /// <summary>总次数。累计埋点采样次数</summary>
            public static readonly Field Total = FindByName("Total");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>跟踪模式。仅针对api类型，过滤被扫描的数据</summary>
            public static readonly Field Mode = FindByName("Mode");

            /// <summary>采样周期。单位秒</summary>
            public static readonly Field Period = FindByName("Period");

            /// <summary>正常数。最大正常采样数，采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
            public static readonly Field MaxSamples = FindByName("MaxSamples");

            /// <summary>异常数。最大异常采样数，采样周期内，最多只记录指定数量的异常事件，默认10</summary>
            public static readonly Field MaxErrors = FindByName("MaxErrors");

            /// <summary>排除项。要排除的操作名，支持*模糊匹配</summary>
            public static readonly Field Excludes = FindByName("Excludes");

            /// <summary>超时时间。超过该时间时强制采样，默认5000毫秒</summary>
            public static readonly Field Timeout = FindByName("Timeout");

            /// <summary>超时排除项。不判断超时的操作名，支持*模糊匹配</summary>
            public static readonly Field TimeoutExcludes = FindByName("TimeoutExcludes");

            /// <summary>Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配</summary>
            public static readonly Field VipClients = FindByName("VipClients");

            /// <summary>节点集。该应用最近一段时间所涉及的来源地址</summary>
            public static readonly Field Nodes = FindByName("Nodes");

            /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用</summary>
            public static readonly Field AlarmThreshold = FindByName("AlarmThreshold");

            /// <summary>告警机器人。钉钉、企业微信等</summary>
            public static readonly Field AlarmRobot = FindByName("AlarmRobot");

            /// <summary>创建者</summary>
            public static readonly Field CreateUser = FindByName("CreateUser");

            /// <summary>创建人</summary>
            public static readonly Field CreateUserID = FindByName("CreateUserID");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新者</summary>
            public static readonly Field UpdateUser = FindByName("UpdateUser");

            /// <summary>更新人</summary>
            public static readonly Field UpdateUserID = FindByName("UpdateUserID");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName("UpdateIP");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得应用跟踪器字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>显示名</summary>
            public const String DisplayName = "DisplayName";

            /// <summary>类别</summary>
            public const String Category = "Category";

            /// <summary>跟踪项。共有多少个埋点</summary>
            public const String ItemCount = "ItemCount";

            /// <summary>天数。共统计了多少天</summary>
            public const String Days = "Days";

            /// <summary>总次数。累计埋点采样次数</summary>
            public const String Total = "Total";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>跟踪模式。仅针对api类型，过滤被扫描的数据</summary>
            public const String Mode = "Mode";

            /// <summary>采样周期。单位秒</summary>
            public const String Period = "Period";

            /// <summary>正常数。最大正常采样数，采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
            public const String MaxSamples = "MaxSamples";

            /// <summary>异常数。最大异常采样数，采样周期内，最多只记录指定数量的异常事件，默认10</summary>
            public const String MaxErrors = "MaxErrors";

            /// <summary>排除项。要排除的操作名，支持*模糊匹配</summary>
            public const String Excludes = "Excludes";

            /// <summary>超时时间。超过该时间时强制采样，默认5000毫秒</summary>
            public const String Timeout = "Timeout";

            /// <summary>超时排除项。不判断超时的操作名，支持*模糊匹配</summary>
            public const String TimeoutExcludes = "TimeoutExcludes";

            /// <summary>Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配</summary>
            public const String VipClients = "VipClients";

            /// <summary>节点集。该应用最近一段时间所涉及的来源地址</summary>
            public const String Nodes = "Nodes";

            /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用</summary>
            public const String AlarmThreshold = "AlarmThreshold";

            /// <summary>告警机器人。钉钉、企业微信等</summary>
            public const String AlarmRobot = "AlarmRobot";

            /// <summary>创建者</summary>
            public const String CreateUser = "CreateUser";

            /// <summary>创建人</summary>
            public const String CreateUserID = "CreateUserID";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>更新者</summary>
            public const String UpdateUser = "UpdateUser";

            /// <summary>更新人</summary>
            public const String UpdateUserID = "UpdateUserID";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";

            /// <summary>更新地址</summary>
            public const String UpdateIP = "UpdateIP";
        }
        #endregion
    }
}