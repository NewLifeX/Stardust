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
    /// <summary>应用部署。关联多个版本，关联多个节点服务器</summary>
    [Serializable]
    [DataObject]
    [Description("应用部署。关联多个版本，关联多个节点服务器")]
    [BindIndex("IU_AppDeploy_Name", true, "Name")]
    [BindTable("AppDeploy", Description = "应用部署。关联多个版本，关联多个节点服务器", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppDeploy
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
        /// <summary>应用。对应StarApp</summary>
        [DisplayName("应用")]
        [Description("应用。对应StarApp")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用。对应StarApp", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private String _Category;
        /// <summary>类别</summary>
        [DisplayName("类别")]
        [Description("类别")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Category", "类别", "")]
        public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

        private String _Name;
        /// <summary>名称。应用名</summary>
        [DisplayName("名称")]
        [Description("名称。应用名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称。应用名", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private Int32 _Nodes;
        /// <summary>节点。该应用部署集所拥有的节点数</summary>
        [DisplayName("节点")]
        [Description("节点。该应用部署集所拥有的节点数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Nodes", "节点。该应用部署集所拥有的节点数", "")]
        public Int32 Nodes { get => _Nodes; set { if (OnPropertyChanging("Nodes", value)) { _Nodes = value; OnPropertyChanged("Nodes"); } } }

        private String _Version;
        /// <summary>版本。应用正在使用的版本号</summary>
        [DisplayName("版本")]
        [Description("版本。应用正在使用的版本号")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本。应用正在使用的版本号", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private String _FileName;
        /// <summary>文件。应用启动文件，Zip应用包使用ZipDeploy</summary>
        [Category("参数")]
        [DisplayName("文件")]
        [Description("文件。应用启动文件，Zip应用包使用ZipDeploy")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("FileName", "文件。应用启动文件，Zip应用包使用ZipDeploy", "")]
        public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

        private String _Arguments;
        /// <summary>参数。启动应用的参数</summary>
        [Category("参数")]
        [DisplayName("参数")]
        [Description("参数。启动应用的参数")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Arguments", "参数。启动应用的参数", "")]
        public String Arguments { get => _Arguments; set { if (OnPropertyChanging("Arguments", value)) { _Arguments = value; OnPropertyChanged("Arguments"); } } }

        private String _WorkingDirectory;
        /// <summary>工作目录。应用根目录</summary>
        [Category("参数")]
        [DisplayName("工作目录")]
        [Description("工作目录。应用根目录")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("WorkingDirectory", "工作目录。应用根目录", "")]
        public String WorkingDirectory { get => _WorkingDirectory; set { if (OnPropertyChanging("WorkingDirectory", value)) { _WorkingDirectory = value; OnPropertyChanged("WorkingDirectory"); } } }

        private Boolean _AutoStart;
        /// <summary>自动启动。系统重启时，或应用退出后，自动拉起应用</summary>
        [Category("参数")]
        [DisplayName("自动启动")]
        [Description("自动启动。系统重启时，或应用退出后，自动拉起应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AutoStart", "自动启动。系统重启时，或应用退出后，自动拉起应用", "")]
        public Boolean AutoStart { get => _AutoStart; set { if (OnPropertyChanging("AutoStart", value)) { _AutoStart = value; OnPropertyChanged("AutoStart"); } } }

        private Boolean _AutoStop;
        /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
        [Category("参数")]
        [DisplayName("自动停止")]
        [Description("自动停止。随着宿主的退出，同时停止该应用进程")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AutoStop", "自动停止。随着宿主的退出，同时停止该应用进程", "")]
        public Boolean AutoStop { get => _AutoStop; set { if (OnPropertyChanging("AutoStop", value)) { _AutoStop = value; OnPropertyChanged("AutoStop"); } } }

        private Int32 _MaxMemory;
        /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
        [Category("参数")]
        [DisplayName("最大内存")]
        [Description("最大内存。单位M，超过上限时自动重启应用，默认0不限制")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxMemory", "最大内存。单位M，超过上限时自动重启应用，默认0不限制", "")]
        public Int32 MaxMemory { get => _MaxMemory; set { if (OnPropertyChanging("MaxMemory", value)) { _MaxMemory = value; OnPropertyChanged("MaxMemory"); } } }

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
                    case "Category": return _Category;
                    case "Name": return _Name;
                    case "Enable": return _Enable;
                    case "Nodes": return _Nodes;
                    case "Version": return _Version;
                    case "FileName": return _FileName;
                    case "Arguments": return _Arguments;
                    case "WorkingDirectory": return _WorkingDirectory;
                    case "AutoStart": return _AutoStart;
                    case "AutoStop": return _AutoStop;
                    case "MaxMemory": return _MaxMemory;
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
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "Nodes": _Nodes = value.ToInt(); break;
                    case "Version": _Version = Convert.ToString(value); break;
                    case "FileName": _FileName = Convert.ToString(value); break;
                    case "Arguments": _Arguments = Convert.ToString(value); break;
                    case "WorkingDirectory": _WorkingDirectory = Convert.ToString(value); break;
                    case "AutoStart": _AutoStart = value.ToBoolean(); break;
                    case "AutoStop": _AutoStop = value.ToBoolean(); break;
                    case "MaxMemory": _MaxMemory = value.ToInt(); break;
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
        /// <summary>取得应用部署字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用。对应StarApp</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>类别</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>名称。应用名</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>节点。该应用部署集所拥有的节点数</summary>
            public static readonly Field Nodes = FindByName("Nodes");

            /// <summary>版本。应用正在使用的版本号</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>文件。应用启动文件，Zip应用包使用ZipDeploy</summary>
            public static readonly Field FileName = FindByName("FileName");

            /// <summary>参数。启动应用的参数</summary>
            public static readonly Field Arguments = FindByName("Arguments");

            /// <summary>工作目录。应用根目录</summary>
            public static readonly Field WorkingDirectory = FindByName("WorkingDirectory");

            /// <summary>自动启动。系统重启时，或应用退出后，自动拉起应用</summary>
            public static readonly Field AutoStart = FindByName("AutoStart");

            /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
            public static readonly Field AutoStop = FindByName("AutoStop");

            /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
            public static readonly Field MaxMemory = FindByName("MaxMemory");

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

        /// <summary>取得应用部署字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用。对应StarApp</summary>
            public const String AppId = "AppId";

            /// <summary>类别</summary>
            public const String Category = "Category";

            /// <summary>名称。应用名</summary>
            public const String Name = "Name";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>节点。该应用部署集所拥有的节点数</summary>
            public const String Nodes = "Nodes";

            /// <summary>版本。应用正在使用的版本号</summary>
            public const String Version = "Version";

            /// <summary>文件。应用启动文件，Zip应用包使用ZipDeploy</summary>
            public const String FileName = "FileName";

            /// <summary>参数。启动应用的参数</summary>
            public const String Arguments = "Arguments";

            /// <summary>工作目录。应用根目录</summary>
            public const String WorkingDirectory = "WorkingDirectory";

            /// <summary>自动启动。系统重启时，或应用退出后，自动拉起应用</summary>
            public const String AutoStart = "AutoStart";

            /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
            public const String AutoStop = "AutoStop";

            /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
            public const String MaxMemory = "MaxMemory";

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