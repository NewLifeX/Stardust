using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data
{
    /// <summary>服务。服务提供者发布的服务</summary>
    [Serializable]
    [DataObject]
    [Description("服务。服务提供者发布的服务")]
    [BindIndex("IU_Service_Name", true, "Name")]
    [BindTable("Service", Description = "服务。服务提供者发布的服务", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class Service : IService
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get { return _ID; } set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private String _Name;
        /// <summary>名称。调用Api的字符串：Data/GetSite中的Data</summary>
        [DisplayName("名称")]
        [Description("名称。调用Api的字符串：Data/GetSite中的Data")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Name", "名称。调用Api的字符串：Data/GetSite中的Data", "", Master = true)]
        public String Name { get { return _Name; } set { if (OnPropertyChanging(__.Name, value)) { _Name = value; OnPropertyChanged(__.Name); } } }

        private String _ServiceType;
        /// <summary>服务类型。带命名空间的全名</summary>
        [DisplayName("服务类型")]
        [Description("服务类型。带命名空间的全名")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("ServiceType", "服务类型。带命名空间的全名", "", Master = true)]
        public String ServiceType { get { return _ServiceType; } set { if (OnPropertyChanging(__.ServiceType, value)) { _ServiceType = value; OnPropertyChanged(__.ServiceType); } } }

        private String _DisplayName;
        /// <summary>显示名</summary>
        [DisplayName("显示名")]
        [Description("显示名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("DisplayName", "显示名", "")]
        public String DisplayName { get { return _DisplayName; } set { if (OnPropertyChanging(__.DisplayName, value)) { _DisplayName = value; OnPropertyChanged(__.DisplayName); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get { return _Enable; } set { if (OnPropertyChanging(__.Enable, value)) { _Enable = value; OnPropertyChanged(__.Enable); } } }

        private Boolean _Anonymous;
        /// <summary>匿名</summary>
        [DisplayName("匿名")]
        [Description("匿名")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Anonymous", "匿名", "")]
        public Boolean Anonymous { get { return _Anonymous; } set { if (OnPropertyChanging(__.Anonymous, value)) { _Anonymous = value; OnPropertyChanged(__.Anonymous); } } }

        private String _Actions;
        /// <summary>功能列表</summary>
        [DisplayName("功能列表")]
        [Description("功能列表")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Actions", "功能列表", "")]
        public String Actions { get { return _Actions; } set { if (OnPropertyChanging(__.Actions, value)) { _Actions = value; OnPropertyChanged(__.Actions); } } }

        private String _Apps;
        /// <summary>应用。提供该服务的应用列表</summary>
        [DisplayName("应用")]
        [Description("应用。提供该服务的应用列表")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Apps", "应用。提供该服务的应用列表", "")]
        public String Apps { get { return _Apps; } set { if (OnPropertyChanging(__.Apps, value)) { _Apps = value; OnPropertyChanged(__.Apps); } } }

        private String _Remark;
        /// <summary>内容</summary>
        [DisplayName("内容")]
        [Description("内容")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Remark", "内容", "")]
        public String Remark { get { return _Remark; } set { if (OnPropertyChanging(__.Remark, value)) { _Remark = value; OnPropertyChanged(__.Remark); } } }

        private String _CreateUser;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateUser", "创建者", "")]
        public String CreateUser { get { return _CreateUser; } set { if (OnPropertyChanging(__.CreateUser, value)) { _CreateUser = value; OnPropertyChanged(__.CreateUser); } } }

        private Int32 _CreateUserID;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserID", "创建者", "")]
        public Int32 CreateUserID { get { return _CreateUserID; } set { if (OnPropertyChanging(__.CreateUserID, value)) { _CreateUserID = value; OnPropertyChanged(__.CreateUserID); } } }

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

        private String _UpdateUser;
        /// <summary>更新者</summary>
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateUser", "更新者", "")]
        public String UpdateUser { get { return _UpdateUser; } set { if (OnPropertyChanging(__.UpdateUser, value)) { _UpdateUser = value; OnPropertyChanged(__.UpdateUser); } } }

        private Int32 _UpdateUserID;
        /// <summary>更新者</summary>
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("UpdateUserID", "更新者", "")]
        public Int32 UpdateUserID { get { return _UpdateUserID; } set { if (OnPropertyChanging(__.UpdateUserID, value)) { _UpdateUserID = value; OnPropertyChanged(__.UpdateUserID); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get { return _UpdateTime; } set { if (OnPropertyChanging(__.UpdateTime, value)) { _UpdateTime = value; OnPropertyChanged(__.UpdateTime); } } }

        private String _UpdateIP;
        /// <summary>更新地址</summary>
        [DisplayName("更新地址")]
        [Description("更新地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateIP", "更新地址", "")]
        public String UpdateIP { get { return _UpdateIP; } set { if (OnPropertyChanging(__.UpdateIP, value)) { _UpdateIP = value; OnPropertyChanged(__.UpdateIP); } } }
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
                    case __.Name : return _Name;
                    case __.ServiceType : return _ServiceType;
                    case __.DisplayName : return _DisplayName;
                    case __.Enable : return _Enable;
                    case __.Anonymous : return _Anonymous;
                    case __.Actions : return _Actions;
                    case __.Apps : return _Apps;
                    case __.Remark : return _Remark;
                    case __.CreateUser : return _CreateUser;
                    case __.CreateUserID : return _CreateUserID;
                    case __.CreateTime : return _CreateTime;
                    case __.CreateIP : return _CreateIP;
                    case __.UpdateUser : return _UpdateUser;
                    case __.UpdateUserID : return _UpdateUserID;
                    case __.UpdateTime : return _UpdateTime;
                    case __.UpdateIP : return _UpdateIP;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID : _ID = Convert.ToInt32(value); break;
                    case __.Name : _Name = Convert.ToString(value); break;
                    case __.ServiceType : _ServiceType = Convert.ToString(value); break;
                    case __.DisplayName : _DisplayName = Convert.ToString(value); break;
                    case __.Enable : _Enable = Convert.ToBoolean(value); break;
                    case __.Anonymous : _Anonymous = Convert.ToBoolean(value); break;
                    case __.Actions : _Actions = Convert.ToString(value); break;
                    case __.Apps : _Apps = Convert.ToString(value); break;
                    case __.Remark : _Remark = Convert.ToString(value); break;
                    case __.CreateUser : _CreateUser = Convert.ToString(value); break;
                    case __.CreateUserID : _CreateUserID = Convert.ToInt32(value); break;
                    case __.CreateTime : _CreateTime = Convert.ToDateTime(value); break;
                    case __.CreateIP : _CreateIP = Convert.ToString(value); break;
                    case __.UpdateUser : _UpdateUser = Convert.ToString(value); break;
                    case __.UpdateUserID : _UpdateUserID = Convert.ToInt32(value); break;
                    case __.UpdateTime : _UpdateTime = Convert.ToDateTime(value); break;
                    case __.UpdateIP : _UpdateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得服务字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>名称。调用Api的字符串：Data/GetSite中的Data</summary>
            public static readonly Field Name = FindByName(__.Name);

            /// <summary>服务类型。带命名空间的全名</summary>
            public static readonly Field ServiceType = FindByName(__.ServiceType);

            /// <summary>显示名</summary>
            public static readonly Field DisplayName = FindByName(__.DisplayName);

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName(__.Enable);

            /// <summary>匿名</summary>
            public static readonly Field Anonymous = FindByName(__.Anonymous);

            /// <summary>功能列表</summary>
            public static readonly Field Actions = FindByName(__.Actions);

            /// <summary>应用。提供该服务的应用列表</summary>
            public static readonly Field Apps = FindByName(__.Apps);

            /// <summary>内容</summary>
            public static readonly Field Remark = FindByName(__.Remark);

            /// <summary>创建者</summary>
            public static readonly Field CreateUser = FindByName(__.CreateUser);

            /// <summary>创建者</summary>
            public static readonly Field CreateUserID = FindByName(__.CreateUserID);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName(__.CreateIP);

            /// <summary>更新者</summary>
            public static readonly Field UpdateUser = FindByName(__.UpdateUser);

            /// <summary>更新者</summary>
            public static readonly Field UpdateUserID = FindByName(__.UpdateUserID);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName(__.UpdateIP);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得服务字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>名称。调用Api的字符串：Data/GetSite中的Data</summary>
            public const String Name = "Name";

            /// <summary>服务类型。带命名空间的全名</summary>
            public const String ServiceType = "ServiceType";

            /// <summary>显示名</summary>
            public const String DisplayName = "DisplayName";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>匿名</summary>
            public const String Anonymous = "Anonymous";

            /// <summary>功能列表</summary>
            public const String Actions = "Actions";

            /// <summary>应用。提供该服务的应用列表</summary>
            public const String Apps = "Apps";

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

    /// <summary>服务。服务提供者发布的服务接口</summary>
    public partial interface IService
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>名称。调用Api的字符串：Data/GetSite中的Data</summary>
        String Name { get; set; }

        /// <summary>服务类型。带命名空间的全名</summary>
        String ServiceType { get; set; }

        /// <summary>显示名</summary>
        String DisplayName { get; set; }

        /// <summary>启用</summary>
        Boolean Enable { get; set; }

        /// <summary>匿名</summary>
        Boolean Anonymous { get; set; }

        /// <summary>功能列表</summary>
        String Actions { get; set; }

        /// <summary>应用。提供该服务的应用列表</summary>
        String Apps { get; set; }

        /// <summary>内容</summary>
        String Remark { get; set; }

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