using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Configs
{
    /// <summary>应用配置。需要管理配置的应用系统列表</summary>
    [Serializable]
    [DataObject]
    [Description("应用配置。需要管理配置的应用系统列表")]
    [BindIndex("IU_AppConfig_Name", true, "Name")]
    [BindTable("AppConfig", Description = "应用配置。需要管理配置的应用系统列表", ConnName = "ConfigCenter", DbType = DatabaseType.None)]
    public partial class AppConfig
    {
        #region 属性
        private Int32 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private String _Category;
        /// <summary>类别</summary>
        [DisplayName("类别")]
        [Description("类别")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Category", "类别", "")]
        public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Int32 _Version;
        /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
        [DisplayName("版本")]
        [Description("版本。应用正在使用的版本号，返回小于等于该版本的配置")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Version", "版本。应用正在使用的版本号，返回小于等于该版本的配置", "")]
        public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private Int32 _NextVersion;
        /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
        [DisplayName("下一版本")]
        [Description("下一版本。下一个将要发布的版本，发布后两者相同")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("NextVersion", "下一版本。下一个将要发布的版本，发布后两者相同", "")]
        public Int32 NextVersion { get => _NextVersion; set { if (OnPropertyChanging("NextVersion", value)) { _NextVersion = value; OnPropertyChanged("NextVersion"); } } }

        private DateTime _PublishTime;
        /// <summary>定时发布。在指定时间自动发布新版本</summary>
        [DisplayName("定时发布")]
        [Description("定时发布。在指定时间自动发布新版本")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("PublishTime", "定时发布。在指定时间自动发布新版本", "")]
        public DateTime PublishTime { get => _PublishTime; set { if (OnPropertyChanging("PublishTime", value)) { _PublishTime = value; OnPropertyChanged("PublishTime"); } } }

        private Boolean _CanBeQuoted;
        /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
        [DisplayName("可被依赖")]
        [Description("可被依赖。打开后，才能被其它应用依赖")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CanBeQuoted", "可被依赖。打开后，才能被其它应用依赖", "")]
        public Boolean CanBeQuoted { get => _CanBeQuoted; set { if (OnPropertyChanging("CanBeQuoted", value)) { _CanBeQuoted = value; OnPropertyChanged("CanBeQuoted"); } } }

        private String _Quotes;
        /// <summary>依赖应用。所依赖应用的集合</summary>
        [DisplayName("依赖应用")]
        [Description("依赖应用。所依赖应用的集合")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Quotes", "依赖应用。所依赖应用的集合", "")]
        public String Quotes { get => _Quotes; set { if (OnPropertyChanging("Quotes", value)) { _Quotes = value; OnPropertyChanged("Quotes"); } } }

        private Boolean _IsGlobal;
        /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
        [DisplayName("全局")]
        [Description("全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("IsGlobal", "全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回", "")]
        public Boolean IsGlobal { get => _IsGlobal; set { if (OnPropertyChanging("IsGlobal", value)) { _IsGlobal = value; OnPropertyChanged("IsGlobal"); } } }

        private Boolean _EnableApollo;
        /// <summary>阿波罗</summary>
        [DisplayName("阿波罗")]
        [Description("阿波罗")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("EnableApollo", "阿波罗", "")]
        public Boolean EnableApollo { get => _EnableApollo; set { if (OnPropertyChanging("EnableApollo", value)) { _EnableApollo = value; OnPropertyChanged("EnableApollo"); } } }

        private String _ApolloMetaServer;
        /// <summary>阿波罗地址</summary>
        [DisplayName("阿波罗地址")]
        [Description("阿波罗地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ApolloMetaServer", "阿波罗地址", "")]
        public String ApolloMetaServer { get => _ApolloMetaServer; set { if (OnPropertyChanging("ApolloMetaServer", value)) { _ApolloMetaServer = value; OnPropertyChanged("ApolloMetaServer"); } } }

        private String _ApolloAppId;
        /// <summary>阿波罗账号</summary>
        [DisplayName("阿波罗账号")]
        [Description("阿波罗账号")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ApolloAppId", "阿波罗账号", "")]
        public String ApolloAppId { get => _ApolloAppId; set { if (OnPropertyChanging("ApolloAppId", value)) { _ApolloAppId = value; OnPropertyChanged("ApolloAppId"); } } }

        private String _ApolloNameSpace;
        /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
        [DisplayName("阿波罗命名空间")]
        [Description("阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ApolloNameSpace", "阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。", "")]
        public String ApolloNameSpace { get => _ApolloNameSpace; set { if (OnPropertyChanging("ApolloNameSpace", value)) { _ApolloNameSpace = value; OnPropertyChanged("ApolloNameSpace"); } } }

        private String _UsedKeys;
        /// <summary>已使用。用过的配置项</summary>
        [DisplayName("已使用")]
        [Description("已使用。用过的配置项")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("UsedKeys", "已使用。用过的配置项", "")]
        public String UsedKeys { get => _UsedKeys; set { if (OnPropertyChanging("UsedKeys", value)) { _UsedKeys = value; OnPropertyChanged("UsedKeys"); } } }

        private String _MissedKeys;
        /// <summary>缺失键。没有读取到的配置项</summary>
        [DisplayName("缺失键")]
        [Description("缺失键。没有读取到的配置项")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("MissedKeys", "缺失键。没有读取到的配置项", "")]
        public String MissedKeys { get => _MissedKeys; set { if (OnPropertyChanging("MissedKeys", value)) { _MissedKeys = value; OnPropertyChanged("MissedKeys"); } } }

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
                    case "Category": return _Category;
                    case "Name": return _Name;
                    case "Enable": return _Enable;
                    case "Version": return _Version;
                    case "NextVersion": return _NextVersion;
                    case "PublishTime": return _PublishTime;
                    case "CanBeQuoted": return _CanBeQuoted;
                    case "Quotes": return _Quotes;
                    case "IsGlobal": return _IsGlobal;
                    case "EnableApollo": return _EnableApollo;
                    case "ApolloMetaServer": return _ApolloMetaServer;
                    case "ApolloAppId": return _ApolloAppId;
                    case "ApolloNameSpace": return _ApolloNameSpace;
                    case "UsedKeys": return _UsedKeys;
                    case "MissedKeys": return _MissedKeys;
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
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Version": _Version = value.ToInt(); break;
                    case "NextVersion": _NextVersion = value.ToInt(); break;
                    case "PublishTime": _PublishTime = value.ToDateTime(); break;
                    case "CanBeQuoted": _CanBeQuoted = value.ToBoolean(); break;
                    case "Quotes": _Quotes = Convert.ToString(value); break;
                    case "IsGlobal": _IsGlobal = value.ToBoolean(); break;
                    case "EnableApollo": _EnableApollo = value.ToBoolean(); break;
                    case "ApolloMetaServer": _ApolloMetaServer = Convert.ToString(value); break;
                    case "ApolloAppId": _ApolloAppId = Convert.ToString(value); break;
                    case "ApolloNameSpace": _ApolloNameSpace = Convert.ToString(value); break;
                    case "UsedKeys": _UsedKeys = Convert.ToString(value); break;
                    case "MissedKeys": _MissedKeys = Convert.ToString(value); break;
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
        /// <summary>取得应用配置字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>类别</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
            public static readonly Field NextVersion = FindByName("NextVersion");

            /// <summary>定时发布。在指定时间自动发布新版本</summary>
            public static readonly Field PublishTime = FindByName("PublishTime");

            /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
            public static readonly Field CanBeQuoted = FindByName("CanBeQuoted");

            /// <summary>依赖应用。所依赖应用的集合</summary>
            public static readonly Field Quotes = FindByName("Quotes");

            /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
            public static readonly Field IsGlobal = FindByName("IsGlobal");

            /// <summary>阿波罗</summary>
            public static readonly Field EnableApollo = FindByName("EnableApollo");

            /// <summary>阿波罗地址</summary>
            public static readonly Field ApolloMetaServer = FindByName("ApolloMetaServer");

            /// <summary>阿波罗账号</summary>
            public static readonly Field ApolloAppId = FindByName("ApolloAppId");

            /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
            public static readonly Field ApolloNameSpace = FindByName("ApolloNameSpace");

            /// <summary>已使用。用过的配置项</summary>
            public static readonly Field UsedKeys = FindByName("UsedKeys");

            /// <summary>缺失键。没有读取到的配置项</summary>
            public static readonly Field MissedKeys = FindByName("MissedKeys");

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

        /// <summary>取得应用配置字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>类别</summary>
            public const String Category = "Category";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
            public const String Version = "Version";

            /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
            public const String NextVersion = "NextVersion";

            /// <summary>定时发布。在指定时间自动发布新版本</summary>
            public const String PublishTime = "PublishTime";

            /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
            public const String CanBeQuoted = "CanBeQuoted";

            /// <summary>依赖应用。所依赖应用的集合</summary>
            public const String Quotes = "Quotes";

            /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
            public const String IsGlobal = "IsGlobal";

            /// <summary>阿波罗</summary>
            public const String EnableApollo = "EnableApollo";

            /// <summary>阿波罗地址</summary>
            public const String ApolloMetaServer = "ApolloMetaServer";

            /// <summary>阿波罗账号</summary>
            public const String ApolloAppId = "ApolloAppId";

            /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
            public const String ApolloNameSpace = "ApolloNameSpace";

            /// <summary>已使用。用过的配置项</summary>
            public const String UsedKeys = "UsedKeys";

            /// <summary>缺失键。没有读取到的配置项</summary>
            public const String MissedKeys = "MissedKeys";

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