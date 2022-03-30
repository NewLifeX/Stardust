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
    /// <summary>跟踪项。应用下的多个埋点</summary>
    [Serializable]
    [DataObject]
    [Description("跟踪项。应用下的多个埋点")]
    [BindIndex("IU_TraceItem_AppId_Name", true, "AppId,Name")]
    [BindTable("TraceItem", Description = "跟踪项。应用下的多个埋点", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class TraceItem
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

        private String _Kind;
        /// <summary>种类</summary>
        [DisplayName("种类")]
        [Description("种类")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Kind", "种类", "")]
        public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

        private String _Name;
        /// <summary>操作名。接口名或埋点名</summary>
        [DisplayName("操作名")]
        [Description("操作名。接口名或埋点名")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Name", "操作名。接口名或埋点名", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private String _DisplayName;
        /// <summary>显示名</summary>
        [DisplayName("显示名")]
        [Description("显示名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("DisplayName", "显示名", "")]
        public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

        private String _Rules;
        /// <summary>规则。支持多个埋点操作名按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开。</summary>
        [DisplayName("规则")]
        [Description("规则。支持多个埋点操作名按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开。")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Rules", "规则。支持多个埋点操作名按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开。", "")]
        public String Rules { get => _Rules; set { if (OnPropertyChanging("Rules", value)) { _Rules = value; OnPropertyChanged("Rules"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Int32 _Timeout;
        /// <summary>超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时</summary>
        [DisplayName("超时时间")]
        [Description("超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Timeout", "超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时", "")]
        public Int32 Timeout { get => _Timeout; set { if (OnPropertyChanging("Timeout", value)) { _Timeout = value; OnPropertyChanged("Timeout"); } } }

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

        private Int32 _Errors;
        /// <summary>错误数</summary>
        [DisplayName("错误数")]
        [Description("错误数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Errors", "错误数", "")]
        public Int32 Errors { get => _Errors; set { if (OnPropertyChanging("Errors", value)) { _Errors = value; OnPropertyChanged("Errors"); } } }

        private Int32 _Cost;
        /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
        [DisplayName("平均耗时")]
        [Description("平均耗时。总耗时除以总次数，单位毫秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Cost", "平均耗时。总耗时除以总次数，单位毫秒", "")]
        public Int32 Cost { get => _Cost; set { if (OnPropertyChanging("Cost", value)) { _Cost = value; OnPropertyChanged("Cost"); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

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
                    case "AppId": return _AppId;
                    case "Kind": return _Kind;
                    case "Name": return _Name;
                    case "DisplayName": return _DisplayName;
                    case "Rules": return _Rules;
                    case "Enable": return _Enable;
                    case "Timeout": return _Timeout;
                    case "Days": return _Days;
                    case "Total": return _Total;
                    case "Errors": return _Errors;
                    case "Cost": return _Cost;
                    case "CreateIP": return _CreateIP;
                    case "CreateTime": return _CreateTime;
                    case "UpdateUser": return _UpdateUser;
                    case "UpdateUserID": return _UpdateUserID;
                    case "UpdateTime": return _UpdateTime;
                    case "UpdateIP": return _UpdateIP;
                    case "Remark": return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "Id": _Id = value.ToInt(); break;
                    case "AppId": _AppId = value.ToInt(); break;
                    case "Kind": _Kind = Convert.ToString(value); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "DisplayName": _DisplayName = Convert.ToString(value); break;
                    case "Rules": _Rules = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Timeout": _Timeout = value.ToInt(); break;
                    case "Days": _Days = value.ToInt(); break;
                    case "Total": _Total = value.ToLong(); break;
                    case "Errors": _Errors = value.ToInt(); break;
                    case "Cost": _Cost = value.ToInt(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                    case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                    case "Remark": _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得跟踪项字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>种类</summary>
            public static readonly Field Kind = FindByName("Kind");

            /// <summary>操作名。接口名或埋点名</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>显示名</summary>
            public static readonly Field DisplayName = FindByName("DisplayName");

            /// <summary>规则。支持多个埋点操作名按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开。</summary>
            public static readonly Field Rules = FindByName("Rules");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时</summary>
            public static readonly Field Timeout = FindByName("Timeout");

            /// <summary>天数。共统计了多少天</summary>
            public static readonly Field Days = FindByName("Days");

            /// <summary>总次数。累计埋点采样次数</summary>
            public static readonly Field Total = FindByName("Total");

            /// <summary>错误数</summary>
            public static readonly Field Errors = FindByName("Errors");

            /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
            public static readonly Field Cost = FindByName("Cost");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>更新者</summary>
            public static readonly Field UpdateUser = FindByName("UpdateUser");

            /// <summary>更新人</summary>
            public static readonly Field UpdateUserID = FindByName("UpdateUserID");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName("UpdateIP");

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName("Remark");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得跟踪项字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>种类</summary>
            public const String Kind = "Kind";

            /// <summary>操作名。接口名或埋点名</summary>
            public const String Name = "Name";

            /// <summary>显示名</summary>
            public const String DisplayName = "DisplayName";

            /// <summary>规则。支持多个埋点操作名按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开。</summary>
            public const String Rules = "Rules";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时</summary>
            public const String Timeout = "Timeout";

            /// <summary>天数。共统计了多少天</summary>
            public const String Days = "Days";

            /// <summary>总次数。累计埋点采样次数</summary>
            public const String Total = "Total";

            /// <summary>错误数</summary>
            public const String Errors = "Errors";

            /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
            public const String Cost = "Cost";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>更新者</summary>
            public const String UpdateUser = "UpdateUser";

            /// <summary>更新人</summary>
            public const String UpdateUserID = "UpdateUserID";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";

            /// <summary>更新地址</summary>
            public const String UpdateIP = "UpdateIP";

            /// <summary>备注</summary>
            public const String Remark = "Remark";
        }
        #endregion
    }
}