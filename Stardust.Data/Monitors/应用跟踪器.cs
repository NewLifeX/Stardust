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
    /// <summary>应用跟踪器。负责跟踪的应用管理</summary>
    [Serializable]
    [DataObject]
    [Description("应用跟踪器。负责跟踪的应用管理")]
    [BindIndex("IU_AppTracer_Name", true, "Name")]
    [BindTable("AppTracer", Description = "应用跟踪器。负责跟踪的应用管理", ConnName = "Monitor", DbType = DatabaseType.None)]
    public partial class AppTracer : IAppTracer
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

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

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Int32 _Period;
        /// <summary>采样周期。单位秒</summary>
        [DisplayName("采样周期")]
        [Description("采样周期。单位秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Period", "采样周期。单位秒", "")]
        public Int32 Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

        private Int32 _MaxSamples;
        /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
        [DisplayName("最大正常采样数")]
        [Description("最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxSamples", "最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系", "")]
        public Int32 MaxSamples { get => _MaxSamples; set { if (OnPropertyChanging("MaxSamples", value)) { _MaxSamples = value; OnPropertyChanged("MaxSamples"); } } }

        private Int32 _MaxErrors;
        /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
        [DisplayName("最大异常采样数")]
        [Description("最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxErrors", "最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10", "")]
        public Int32 MaxErrors { get => _MaxErrors; set { if (OnPropertyChanging("MaxErrors", value)) { _MaxErrors = value; OnPropertyChanged("MaxErrors"); } } }

        private String _Excludes;
        /// <summary>排除项。要排除的操作名</summary>
        [DisplayName("排除项")]
        [Description("排除项。要排除的操作名")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Excludes", "排除项。要排除的操作名", "")]
        public String Excludes { get => _Excludes; set { if (OnPropertyChanging("Excludes", value)) { _Excludes = value; OnPropertyChanged("Excludes"); } } }

        private Int32 _Timeout;
        /// <summary>超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒</summary>
        [DisplayName("超时时间")]
        [Description("超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Timeout", "超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒", "")]
        public Int32 Timeout { get => _Timeout; set { if (OnPropertyChanging("Timeout", value)) { _Timeout = value; OnPropertyChanged("Timeout"); } } }

        private String _CreateUser;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateUser", "创建者", "")]
        public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

        private Int32 _CreateUserID;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserID", "创建者", "")]
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
        /// <summary>更新者</summary>
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("UpdateUserID", "更新者", "")]
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
                    case "Name": return _Name;
                    case "DisplayName": return _DisplayName;
                    case "Category": return _Category;
                    case "Enable": return _Enable;
                    case "Period": return _Period;
                    case "MaxSamples": return _MaxSamples;
                    case "MaxErrors": return _MaxErrors;
                    case "Excludes": return _Excludes;
                    case "Timeout": return _Timeout;
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
                    case "Name": _Name = Convert.ToString(value); break;
                    case "DisplayName": _DisplayName = Convert.ToString(value); break;
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Period": _Period = value.ToInt(); break;
                    case "MaxSamples": _MaxSamples = value.ToInt(); break;
                    case "MaxErrors": _MaxErrors = value.ToInt(); break;
                    case "Excludes": _Excludes = Convert.ToString(value); break;
                    case "Timeout": _Timeout = value.ToInt(); break;
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

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>显示名</summary>
            public static readonly Field DisplayName = FindByName("DisplayName");

            /// <summary>类别</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>采样周期。单位秒</summary>
            public static readonly Field Period = FindByName("Period");

            /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
            public static readonly Field MaxSamples = FindByName("MaxSamples");

            /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
            public static readonly Field MaxErrors = FindByName("MaxErrors");

            /// <summary>排除项。要排除的操作名</summary>
            public static readonly Field Excludes = FindByName("Excludes");

            /// <summary>超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒</summary>
            public static readonly Field Timeout = FindByName("Timeout");

            /// <summary>创建者</summary>
            public static readonly Field CreateUser = FindByName("CreateUser");

            /// <summary>创建者</summary>
            public static readonly Field CreateUserID = FindByName("CreateUserID");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新者</summary>
            public static readonly Field UpdateUser = FindByName("UpdateUser");

            /// <summary>更新者</summary>
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

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>显示名</summary>
            public const String DisplayName = "DisplayName";

            /// <summary>类别</summary>
            public const String Category = "Category";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>采样周期。单位秒</summary>
            public const String Period = "Period";

            /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
            public const String MaxSamples = "MaxSamples";

            /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
            public const String MaxErrors = "MaxErrors";

            /// <summary>排除项。要排除的操作名</summary>
            public const String Excludes = "Excludes";

            /// <summary>超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒</summary>
            public const String Timeout = "Timeout";

            /// <summary>创建者</summary>
            public const String CreateUser = "CreateUser";

            /// <summary>创建者</summary>
            public const String CreateUserID = "CreateUserID";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>更新者</summary>
            public const String UpdateUser = "UpdateUser";

            /// <summary>更新者</summary>
            public const String UpdateUserID = "UpdateUserID";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";

            /// <summary>更新地址</summary>
            public const String UpdateIP = "UpdateIP";
        }
        #endregion
    }

    /// <summary>应用跟踪器。负责跟踪的应用管理接口</summary>
    public partial interface IAppTracer
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>名称</summary>
        String Name { get; set; }

        /// <summary>显示名</summary>
        String DisplayName { get; set; }

        /// <summary>类别</summary>
        String Category { get; set; }

        /// <summary>启用</summary>
        Boolean Enable { get; set; }

        /// <summary>采样周期。单位秒</summary>
        Int32 Period { get; set; }

        /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
        Int32 MaxSamples { get; set; }

        /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
        Int32 MaxErrors { get; set; }

        /// <summary>排除项。要排除的操作名</summary>
        String Excludes { get; set; }

        /// <summary>超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒</summary>
        Int32 Timeout { get; set; }

        /// <summary>创建者</summary>
        String CreateUser { get; set; }

        /// <summary>创建者</summary>
        Int32 CreateUserID { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>创建地址</summary>
        String CreateIP { get; set; }

        /// <summary>更新者</summary>
        String UpdateUser { get; set; }

        /// <summary>更新者</summary>
        Int32 UpdateUserID { get; set; }

        /// <summary>更新时间</summary>
        DateTime UpdateTime { get; set; }

        /// <summary>更新地址</summary>
        String UpdateIP { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}