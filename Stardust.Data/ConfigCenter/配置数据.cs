using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.ConfigCenter
{
    /// <summary>配置数据</summary>
    [Serializable]
    [DataObject]
    [Description("配置数据")]
    [BindIndex("IU_ConfigData_AppId_Name_Scope", true, "AppId,Name,Scope")]
    [BindTable("ConfigData", Description = "配置数据", ConnName = "ConfigCenter", DbType = DatabaseType.None)]
    public partial class ConfigData
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

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private String _Scope;
        /// <summary>作用域</summary>
        [DisplayName("作用域")]
        [Description("作用域")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Scope", "作用域", "")]
        public String Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

        private String _Value;
        /// <summary>数值</summary>
        [DisplayName("数值")]
        [Description("数值")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Value", "数值", "")]
        public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Boolean _Internal;
        /// <summary>内部。内部的配置只能用于内嵌，而不能直接请求使用</summary>
        [DisplayName("内部")]
        [Description("内部。内部的配置只能用于内嵌，而不能直接请求使用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Internal", "内部。内部的配置只能用于内嵌，而不能直接请求使用", "")]
        public Boolean Internal { get => _Internal; set { if (OnPropertyChanging("Internal", value)) { _Internal = value; OnPropertyChanged("Internal"); } } }

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
                    case "Name": return _Name;
                    case "Scope": return _Scope;
                    case "Value": return _Value;
                    case "Enable": return _Enable;
                    case "Internal": return _Internal;
                    case "CreateUserID": return _CreateUserID;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
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
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Scope": _Scope = Convert.ToString(value); break;
                    case "Value": _Value = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Internal": _Internal = value.ToBoolean(); break;
                    case "CreateUserID": _CreateUserID = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
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
        /// <summary>取得配置数据字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>作用域</summary>
            public static readonly Field Scope = FindByName("Scope");

            /// <summary>数值</summary>
            public static readonly Field Value = FindByName("Value");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>内部。内部的配置只能用于内嵌，而不能直接请求使用</summary>
            public static readonly Field Internal = FindByName("Internal");

            /// <summary>创建者</summary>
            public static readonly Field CreateUserID = FindByName("CreateUserID");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新者</summary>
            public static readonly Field UpdateUserID = FindByName("UpdateUserID");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName("UpdateIP");

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName("Remark");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得配置数据字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>作用域</summary>
            public const String Scope = "Scope";

            /// <summary>数值</summary>
            public const String Value = "Value";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>内部。内部的配置只能用于内嵌，而不能直接请求使用</summary>
            public const String Internal = "Internal";

            /// <summary>创建者</summary>
            public const String CreateUserID = "CreateUserID";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>更新者</summary>
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