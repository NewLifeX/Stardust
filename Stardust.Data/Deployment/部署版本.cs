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
    /// <summary>部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本</summary>
    [Serializable]
    [DataObject]
    [Description("部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本")]
    [BindIndex("IU_AppDeployVersion_AppId_Version", true, "AppId,Version")]
    [BindTable("AppDeployVersion", Description = "部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppDeployVersion
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
        /// <summary>应用部署集。对应AppDeploy</summary>
        [DisplayName("应用部署集")]
        [Description("应用部署集。对应AppDeploy")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用部署集。对应AppDeploy", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private String _Version;
        /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
        [DisplayName("版本")]
        [Description("版本。如2.3.2022.0911，字符串比较大小")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Version", "版本。如2.3.2022.0911，字符串比较大小", "", Master = true)]
        public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private String _Url;
        /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
        [DisplayName("资源地址")]
        [Description("资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Url", "资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖", "", ItemType = "file")]
        public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

        private Int64 _Size;
        /// <summary>文件大小</summary>
        [DisplayName("文件大小")]
        [Description("文件大小")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
        public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

        private String _Hash;
        /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
        [DisplayName("文件哈希")]
        [Description("文件哈希。MD5散列，避免下载的文件有缺失")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Hash", "文件哈希。MD5散列，避免下载的文件有缺失", "")]
        public String Hash { get => _Hash; set { if (OnPropertyChanging("Hash", value)) { _Hash = value; OnPropertyChanged("Hash"); } } }

        private Int32 _CreateUserId;
        /// <summary>创建者</summary>
        [Category("扩展")]
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserId", "创建者", "")]
        public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [Category("扩展")]
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [Category("扩展")]
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

        private Int32 _UpdateUserId;
        /// <summary>更新者</summary>
        [Category("扩展")]
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("UpdateUserId", "更新者", "")]
        public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [Category("扩展")]
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

        private String _UpdateIP;
        /// <summary>更新地址</summary>
        [Category("扩展")]
        [DisplayName("更新地址")]
        [Description("更新地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateIP", "更新地址", "")]
        public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

        private String _Remark;
        /// <summary>备注</summary>
        [Category("扩展")]
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
                    case "Version": return _Version;
                    case "Enable": return _Enable;
                    case "Url": return _Url;
                    case "Size": return _Size;
                    case "Hash": return _Hash;
                    case "CreateUserId": return _CreateUserId;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    case "UpdateUserId": return _UpdateUserId;
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
                    case "Version": _Version = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Url": _Url = Convert.ToString(value); break;
                    case "Size": _Size = value.ToLong(); break;
                    case "Hash": _Hash = Convert.ToString(value); break;
                    case "CreateUserId": _CreateUserId = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                    case "Remark": _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得部署版本字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用部署集。对应AppDeploy</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
            public static readonly Field Url = FindByName("Url");

            /// <summary>文件大小</summary>
            public static readonly Field Size = FindByName("Size");

            /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
            public static readonly Field Hash = FindByName("Hash");

            /// <summary>创建者</summary>
            public static readonly Field CreateUserId = FindByName("CreateUserId");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            /// <summary>更新者</summary>
            public static readonly Field UpdateUserId = FindByName("UpdateUserId");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName("UpdateIP");

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName("Remark");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得部署版本字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用部署集。对应AppDeploy</summary>
            public const String AppId = "AppId";

            /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
            public const String Version = "Version";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
            public const String Url = "Url";

            /// <summary>文件大小</summary>
            public const String Size = "Size";

            /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
            public const String Hash = "Hash";

            /// <summary>创建者</summary>
            public const String CreateUserId = "CreateUserId";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>更新者</summary>
            public const String UpdateUserId = "UpdateUserId";

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