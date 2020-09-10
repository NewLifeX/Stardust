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
    /// <summary>应用系统。服务提供者和消费者</summary>
    [Serializable]
    [DataObject]
    [Description("应用系统。服务提供者和消费者")]
    [BindIndex("IU_App_Name", true, "Name")]
    [BindTable("App", Description = "应用系统。服务提供者和消费者", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class App
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

        private String _Secret;
        /// <summary>密钥</summary>
        [DisplayName("密钥")]
        [Description("密钥")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Secret", "密钥", "")]
        public String Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

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

        private Boolean _AutoActive;
        /// <summary>自动激活。新登录应用是否自动激活，只有激活的应用，才提供服务</summary>
        [DisplayName("自动激活")]
        [Description("自动激活。新登录应用是否自动激活，只有激活的应用，才提供服务")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AutoActive", "自动激活。新登录应用是否自动激活，只有激活的应用，才提供服务", "")]
        public Boolean AutoActive { get => _AutoActive; set { if (OnPropertyChanging("AutoActive", value)) { _AutoActive = value; OnPropertyChanged("AutoActive"); } } }

        private String _Namespace;
        /// <summary>命名空间。限制该应用只能发布指定命名空间的服务，多个用逗号分隔</summary>
        [DisplayName("命名空间")]
        [Description("命名空间。限制该应用只能发布指定命名空间的服务，多个用逗号分隔")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Namespace", "命名空间。限制该应用只能发布指定命名空间的服务，多个用逗号分隔", "")]
        public String Namespace { get => _Namespace; set { if (OnPropertyChanging("Namespace", value)) { _Namespace = value; OnPropertyChanged("Namespace"); } } }

        private Int32 _Services;
        /// <summary>服务数。该应用提供的服务数</summary>
        [DisplayName("服务数")]
        [Description("服务数。该应用提供的服务数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Services", "服务数。该应用提供的服务数", "")]
        public Int32 Services { get => _Services; set { if (OnPropertyChanging("Services", value)) { _Services = value; OnPropertyChanged("Services"); } } }

        private Int32 _Actions;
        /// <summary>功能数。该应用提供的功能数</summary>
        [DisplayName("功能数")]
        [Description("功能数。该应用提供的功能数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Actions", "功能数。该应用提供的功能数", "")]
        public Int32 Actions { get => _Actions; set { if (OnPropertyChanging("Actions", value)) { _Actions = value; OnPropertyChanged("Actions"); } } }

        private DateTime _LastLogin;
        /// <summary>最后登录</summary>
        [DisplayName("最后登录")]
        [Description("最后登录")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("LastLogin", "最后登录", "")]
        public DateTime LastLogin { get => _LastLogin; set { if (OnPropertyChanging("LastLogin", value)) { _LastLogin = value; OnPropertyChanged("LastLogin"); } } }

        private String _LastIP;
        /// <summary>最后IP</summary>
        [DisplayName("最后IP")]
        [Description("最后IP")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastIP", "最后IP", "")]
        public String LastIP { get => _LastIP; set { if (OnPropertyChanging("LastIP", value)) { _LastIP = value; OnPropertyChanged("LastIP"); } } }

        private String _Remark;
        /// <summary>内容</summary>
        [DisplayName("内容")]
        [Description("内容")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Remark", "内容", "")]
        public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

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
                    case "Secret": return _Secret;
                    case "Category": return _Category;
                    case "Enable": return _Enable;
                    case "AutoActive": return _AutoActive;
                    case "Namespace": return _Namespace;
                    case "Services": return _Services;
                    case "Actions": return _Actions;
                    case "LastLogin": return _LastLogin;
                    case "LastIP": return _LastIP;
                    case "Remark": return _Remark;
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
                    case "Secret": _Secret = Convert.ToString(value); break;
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "AutoActive": _AutoActive = value.ToBoolean(); break;
                    case "Namespace": _Namespace = Convert.ToString(value); break;
                    case "Services": _Services = value.ToInt(); break;
                    case "Actions": _Actions = value.ToInt(); break;
                    case "LastLogin": _LastLogin = value.ToDateTime(); break;
                    case "LastIP": _LastIP = Convert.ToString(value); break;
                    case "Remark": _Remark = Convert.ToString(value); break;
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
        /// <summary>取得应用系统字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>显示名</summary>
            public static readonly Field DisplayName = FindByName("DisplayName");

            /// <summary>密钥</summary>
            public static readonly Field Secret = FindByName("Secret");

            /// <summary>类别</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>自动激活。新登录应用是否自动激活，只有激活的应用，才提供服务</summary>
            public static readonly Field AutoActive = FindByName("AutoActive");

            /// <summary>命名空间。限制该应用只能发布指定命名空间的服务，多个用逗号分隔</summary>
            public static readonly Field Namespace = FindByName("Namespace");

            /// <summary>服务数。该应用提供的服务数</summary>
            public static readonly Field Services = FindByName("Services");

            /// <summary>功能数。该应用提供的功能数</summary>
            public static readonly Field Actions = FindByName("Actions");

            /// <summary>最后登录</summary>
            public static readonly Field LastLogin = FindByName("LastLogin");

            /// <summary>最后IP</summary>
            public static readonly Field LastIP = FindByName("LastIP");

            /// <summary>内容</summary>
            public static readonly Field Remark = FindByName("Remark");

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

        /// <summary>取得应用系统字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>显示名</summary>
            public const String DisplayName = "DisplayName";

            /// <summary>密钥</summary>
            public const String Secret = "Secret";

            /// <summary>类别</summary>
            public const String Category = "Category";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>自动激活。新登录应用是否自动激活，只有激活的应用，才提供服务</summary>
            public const String AutoActive = "AutoActive";

            /// <summary>命名空间。限制该应用只能发布指定命名空间的服务，多个用逗号分隔</summary>
            public const String Namespace = "Namespace";

            /// <summary>服务数。该应用提供的服务数</summary>
            public const String Services = "Services";

            /// <summary>功能数。该应用提供的功能数</summary>
            public const String Actions = "Actions";

            /// <summary>最后登录</summary>
            public const String LastLogin = "LastLogin";

            /// <summary>最后IP</summary>
            public const String LastIP = "LastIP";

            /// <summary>内容</summary>
            public const String Remark = "Remark";

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
}