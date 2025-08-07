using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Deployment;

/// <summary>应用部署。应用部署配置，单应用可有多个部署集合，例如分别是arm和x64。发布时新增版本，拉取代码编译并上传zip应用包，再到部署节点里发布</summary>
[Serializable]
[DataObject]
[Description("应用部署。应用部署配置，单应用可有多个部署集合，例如分别是arm和x64。发布时新增版本，拉取代码编译并上传zip应用包，再到部署节点里发布")]
[BindIndex("IU_AppDeploy_Name", true, "Name")]
[BindIndex("IX_AppDeploy_ProjectId", false, "ProjectId")]
[BindTable("AppDeploy", Description = "应用部署。应用部署配置，单应用可有多个部署集合，例如分别是arm和x64。发布时新增版本，拉取代码编译并上传zip应用包，再到部署节点里发布", ConnName = "Stardust", DbType = DatabaseType.None)]
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

    private Int32 _ProjectId;
    /// <summary>项目。资源归属的团队</summary>
    [DisplayName("项目")]
    [Description("项目。资源归属的团队")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。资源归属的团队", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

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

    private Boolean _MultiVersion;
    /// <summary>多版本。支持多运行时版本，此时只认可部署版本中符合运行时要求的最新可用版本</summary>
    [DisplayName("多版本")]
    [Description("多版本。支持多运行时版本，此时只认可部署版本中符合运行时要求的最新可用版本")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MultiVersion", "多版本。支持多运行时版本，此时只认可部署版本中符合运行时要求的最新可用版本", "")]
    public Boolean MultiVersion { get => _MultiVersion; set { if (OnPropertyChanging("MultiVersion", value)) { _MultiVersion = value; OnPropertyChanged("MultiVersion"); } } }

    private Boolean _AutoPublish;
    /// <summary>自动发布。应用版本后自动发布到启用节点，加快发布速度</summary>
    [DisplayName("自动发布")]
    [Description("自动发布。应用版本后自动发布到启用节点，加快发布速度")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AutoPublish", "自动发布。应用版本后自动发布到启用节点，加快发布速度", "")]
    public Boolean AutoPublish { get => _AutoPublish; set { if (OnPropertyChanging("AutoPublish", value)) { _AutoPublish = value; OnPropertyChanged("AutoPublish"); } } }

    private String _PackageName;
    /// <summary>包名。用于判断上传包名是否正确，避免错误上传其它应用包，支持*模糊匹配</summary>
    [DisplayName("包名")]
    [Description("包名。用于判断上传包名是否正确，避免错误上传其它应用包，支持*模糊匹配")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PackageName", "包名。用于判断上传包名是否正确，避免错误上传其它应用包，支持*模糊匹配", "")]
    public String PackageName { get => _PackageName; set { if (OnPropertyChanging("PackageName", value)) { _PackageName = value; OnPropertyChanged("PackageName"); } } }

    private Int32 _Port;
    /// <summary>监听端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
    [DisplayName("监听端口")]
    [Description("监听端口。应用自身监听的端口，如果是dotnet应用会增加urls参数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Port", "监听端口。应用自身监听的端口，如果是dotnet应用会增加urls参数", "")]
    public Int32 Port { get => _Port; set { if (OnPropertyChanging("Port", value)) { _Port = value; OnPropertyChanged("Port"); } } }

    private String _Urls;
    /// <summary>服务地址。对外提供服务的域名端口地址，自动生成nginx配置，如https://sso.newlifex.com</summary>
    [DisplayName("服务地址")]
    [Description("服务地址。对外提供服务的域名端口地址，自动生成nginx配置，如https://sso.newlifex.com")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Urls", "服务地址。对外提供服务的域名端口地址，自动生成nginx配置，如https://sso.newlifex.com", "")]
    public String Urls { get => _Urls; set { if (OnPropertyChanging("Urls", value)) { _Urls = value; OnPropertyChanged("Urls"); } } }

    private String _Repository;
    /// <summary>代码库。下载代码的位置</summary>
    [Category("编译参数")]
    [DisplayName("代码库")]
    [Description("代码库。下载代码的位置")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Repository", "代码库。下载代码的位置", "", ItemType = "url")]
    public String Repository { get => _Repository; set { if (OnPropertyChanging("Repository", value)) { _Repository = value; OnPropertyChanged("Repository"); } } }

    private String _Branch;
    /// <summary>分支。默认master</summary>
    [Category("编译参数")]
    [DisplayName("分支")]
    [Description("分支。默认master")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Branch", "分支。默认master", "", DefaultValue = "master")]
    public String Branch { get => _Branch; set { if (OnPropertyChanging("Branch", value)) { _Branch = value; OnPropertyChanged("Branch"); } } }

    private String _ProjectPath;
    /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
    [Category("编译参数")]
    [DisplayName("项目路径")]
    [Description("项目路径。需要编译的项目路径，相对于代码库根目录")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ProjectPath", "项目路径。需要编译的项目路径，相对于代码库根目录", "")]
    public String ProjectPath { get => _ProjectPath; set { if (OnPropertyChanging("ProjectPath", value)) { _ProjectPath = value; OnPropertyChanged("ProjectPath"); } } }

    private Stardust.Models.ProjectKinds _ProjectKind;
    /// <summary>项目类型。默认dotnet</summary>
    [Category("编译参数")]
    [DisplayName("项目类型")]
    [Description("项目类型。默认dotnet")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectKind", "项目类型。默认dotnet", "", DefaultValue = "1")]
    public Stardust.Models.ProjectKinds ProjectKind { get => _ProjectKind; set { if (OnPropertyChanging("ProjectKind", value)) { _ProjectKind = value; OnPropertyChanged("ProjectKind"); } } }

    private String _BuildArgs;
    /// <summary>编译参数。编译项目时所需参数</summary>
    [Category("编译参数")]
    [DisplayName("编译参数")]
    [Description("编译参数。编译项目时所需参数")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("BuildArgs", "编译参数。编译项目时所需参数", "")]
    public String BuildArgs { get => _BuildArgs; set { if (OnPropertyChanging("BuildArgs", value)) { _BuildArgs = value; OnPropertyChanged("BuildArgs"); } } }

    private String _PackageFilters;
    /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
    [Category("编译参数")]
    [DisplayName("打包过滤器")]
    [Description("打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PackageFilters", "打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开", "")]
    public String PackageFilters { get => _PackageFilters; set { if (OnPropertyChanging("PackageFilters", value)) { _PackageFilters = value; OnPropertyChanged("PackageFilters"); } } }

    private String _FileName;
    /// <summary>文件。应用启动文件，可直接使用zip包</summary>
    [Category("发布参数")]
    [DisplayName("文件")]
    [Description("文件。应用启动文件，可直接使用zip包")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FileName", "文件。应用启动文件，可直接使用zip包", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _Arguments;
    /// <summary>参数。启动应用的参数</summary>
    [Category("发布参数")]
    [DisplayName("参数")]
    [Description("参数。启动应用的参数")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Arguments", "参数。启动应用的参数", "")]
    public String Arguments { get => _Arguments; set { if (OnPropertyChanging("Arguments", value)) { _Arguments = value; OnPropertyChanged("Arguments"); } } }

    private String _WorkingDirectory;
    /// <summary>工作目录。应用根目录</summary>
    [Category("发布参数")]
    [DisplayName("工作目录")]
    [Description("工作目录。应用根目录")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("WorkingDirectory", "工作目录。应用根目录", "")]
    public String WorkingDirectory { get => _WorkingDirectory; set { if (OnPropertyChanging("WorkingDirectory", value)) { _WorkingDirectory = value; OnPropertyChanged("WorkingDirectory"); } } }

    private String _UserName;
    /// <summary>用户名。以该用户执行应用</summary>
    [Category("发布参数")]
    [DisplayName("用户名")]
    [Description("用户名。以该用户执行应用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名。以该用户执行应用", "")]
    public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String _Environments;
    /// <summary>环境变量。启动应用前设置的环境变量</summary>
    [Category("发布参数")]
    [DisplayName("环境变量")]
    [Description("环境变量。启动应用前设置的环境变量")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Environments", "环境变量。启动应用前设置的环境变量", "")]
    public String Environments { get => _Environments; set { if (OnPropertyChanging("Environments", value)) { _Environments = value; OnPropertyChanged("Environments"); } } }

    private Int32 _MaxMemory;
    /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
    [Category("发布参数")]
    [DisplayName("最大内存")]
    [Description("最大内存。单位M，超过上限时自动重启应用，默认0不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxMemory", "最大内存。单位M，超过上限时自动重启应用，默认0不限制", "")]
    public Int32 MaxMemory { get => _MaxMemory; set { if (OnPropertyChanging("MaxMemory", value)) { _MaxMemory = value; OnPropertyChanged("MaxMemory"); } } }

    private Stardust.Models.ProcessPriority _Priority;
    /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
    [Category("发布参数")]
    [DisplayName("优先级")]
    [Description("优先级。表示应用程序中任务或操作的优先级级别")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "优先级。表示应用程序中任务或操作的优先级级别", "")]
    public Stardust.Models.ProcessPriority Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private Stardust.Models.ServiceModes _Mode;
    /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip</summary>
    [Category("发布参数")]
    [DisplayName("工作模式")]
    [Description("工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Mode", "工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip", "")]
    public Stardust.Models.ServiceModes Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private Boolean _AutoStop;
    /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
    [Category("发布参数")]
    [DisplayName("自动停止")]
    [Description("自动停止。随着宿主的退出，同时停止该应用进程")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AutoStop", "自动停止。随着宿主的退出，同时停止该应用进程", "")]
    public Boolean AutoStop { get => _AutoStop; set { if (OnPropertyChanging("AutoStop", value)) { _AutoStop = value; OnPropertyChanged("AutoStop"); } } }

    private Boolean _ReloadOnChange;
    /// <summary>检测变动。当文件发生改变时，自动重启应用</summary>
    [Category("发布参数")]
    [DisplayName("检测变动")]
    [Description("检测变动。当文件发生改变时，自动重启应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ReloadOnChange", "检测变动。当文件发生改变时，自动重启应用", "")]
    public Boolean ReloadOnChange { get => _ReloadOnChange; set { if (OnPropertyChanging("ReloadOnChange", value)) { _ReloadOnChange = value; OnPropertyChanged("ReloadOnChange"); } } }

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
        get => name switch
        {
            "Id" => _Id,
            "ProjectId" => _ProjectId,
            "AppId" => _AppId,
            "Category" => _Category,
            "Name" => _Name,
            "Enable" => _Enable,
            "Nodes" => _Nodes,
            "Version" => _Version,
            "MultiVersion" => _MultiVersion,
            "AutoPublish" => _AutoPublish,
            "PackageName" => _PackageName,
            "Port" => _Port,
            "Urls" => _Urls,
            "Repository" => _Repository,
            "Branch" => _Branch,
            "ProjectPath" => _ProjectPath,
            "ProjectKind" => _ProjectKind,
            "BuildArgs" => _BuildArgs,
            "PackageFilters" => _PackageFilters,
            "FileName" => _FileName,
            "Arguments" => _Arguments,
            "WorkingDirectory" => _WorkingDirectory,
            "UserName" => _UserName,
            "Environments" => _Environments,
            "MaxMemory" => _MaxMemory,
            "Priority" => _Priority,
            "Mode" => _Mode,
            "AutoStop" => _AutoStop,
            "ReloadOnChange" => _ReloadOnChange,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserId" => _UpdateUserId,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Nodes": _Nodes = value.ToInt(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "MultiVersion": _MultiVersion = value.ToBoolean(); break;
                case "AutoPublish": _AutoPublish = value.ToBoolean(); break;
                case "PackageName": _PackageName = Convert.ToString(value); break;
                case "Port": _Port = value.ToInt(); break;
                case "Urls": _Urls = Convert.ToString(value); break;
                case "Repository": _Repository = Convert.ToString(value); break;
                case "Branch": _Branch = Convert.ToString(value); break;
                case "ProjectPath": _ProjectPath = Convert.ToString(value); break;
                case "ProjectKind": _ProjectKind = (Stardust.Models.ProjectKinds)value.ToInt(); break;
                case "BuildArgs": _BuildArgs = Convert.ToString(value); break;
                case "PackageFilters": _PackageFilters = Convert.ToString(value); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "Arguments": _Arguments = Convert.ToString(value); break;
                case "WorkingDirectory": _WorkingDirectory = Convert.ToString(value); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "Environments": _Environments = Convert.ToString(value); break;
                case "MaxMemory": _MaxMemory = value.ToInt(); break;
                case "Priority": _Priority = (Stardust.Models.ProcessPriority)value.ToInt(); break;
                case "Mode": _Mode = (Stardust.Models.ServiceModes)value.ToInt(); break;
                case "AutoStop": _AutoStop = value.ToBoolean(); break;
                case "ReloadOnChange": _ReloadOnChange = value.ToBoolean(); break;
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

    #region 关联映射
    /// <summary>项目</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Platform.GalaxyProject Project => Extends.Get(nameof(Project), k => Stardust.Data.Platform.GalaxyProject.FindById(ProjectId));

    /// <summary>项目</summary>
    [Map(nameof(ProjectId), typeof(Stardust.Data.Platform.GalaxyProject), "Id")]
    public String ProjectName => Project?.Name;

    #endregion

    #region 扩展查询
    #endregion

    #region 字段名
    /// <summary>取得应用部署字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

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

        /// <summary>多版本。支持多运行时版本，此时只认可部署版本中符合运行时要求的最新可用版本</summary>
        public static readonly Field MultiVersion = FindByName("MultiVersion");

        /// <summary>自动发布。应用版本后自动发布到启用节点，加快发布速度</summary>
        public static readonly Field AutoPublish = FindByName("AutoPublish");

        /// <summary>包名。用于判断上传包名是否正确，避免错误上传其它应用包，支持*模糊匹配</summary>
        public static readonly Field PackageName = FindByName("PackageName");

        /// <summary>监听端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
        public static readonly Field Port = FindByName("Port");

        /// <summary>服务地址。对外提供服务的域名端口地址，自动生成nginx配置，如https://sso.newlifex.com</summary>
        public static readonly Field Urls = FindByName("Urls");

        /// <summary>代码库。下载代码的位置</summary>
        public static readonly Field Repository = FindByName("Repository");

        /// <summary>分支。默认master</summary>
        public static readonly Field Branch = FindByName("Branch");

        /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
        public static readonly Field ProjectPath = FindByName("ProjectPath");

        /// <summary>项目类型。默认dotnet</summary>
        public static readonly Field ProjectKind = FindByName("ProjectKind");

        /// <summary>编译参数。编译项目时所需参数</summary>
        public static readonly Field BuildArgs = FindByName("BuildArgs");

        /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
        public static readonly Field PackageFilters = FindByName("PackageFilters");

        /// <summary>文件。应用启动文件，可直接使用zip包</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>参数。启动应用的参数</summary>
        public static readonly Field Arguments = FindByName("Arguments");

        /// <summary>工作目录。应用根目录</summary>
        public static readonly Field WorkingDirectory = FindByName("WorkingDirectory");

        /// <summary>用户名。以该用户执行应用</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>环境变量。启动应用前设置的环境变量</summary>
        public static readonly Field Environments = FindByName("Environments");

        /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
        public static readonly Field MaxMemory = FindByName("MaxMemory");

        /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
        public static readonly Field AutoStop = FindByName("AutoStop");

        /// <summary>检测变动。当文件发生改变时，自动重启应用</summary>
        public static readonly Field ReloadOnChange = FindByName("ReloadOnChange");

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

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

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

        /// <summary>多版本。支持多运行时版本，此时只认可部署版本中符合运行时要求的最新可用版本</summary>
        public const String MultiVersion = "MultiVersion";

        /// <summary>自动发布。应用版本后自动发布到启用节点，加快发布速度</summary>
        public const String AutoPublish = "AutoPublish";

        /// <summary>包名。用于判断上传包名是否正确，避免错误上传其它应用包，支持*模糊匹配</summary>
        public const String PackageName = "PackageName";

        /// <summary>监听端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
        public const String Port = "Port";

        /// <summary>服务地址。对外提供服务的域名端口地址，自动生成nginx配置，如https://sso.newlifex.com</summary>
        public const String Urls = "Urls";

        /// <summary>代码库。下载代码的位置</summary>
        public const String Repository = "Repository";

        /// <summary>分支。默认master</summary>
        public const String Branch = "Branch";

        /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
        public const String ProjectPath = "ProjectPath";

        /// <summary>项目类型。默认dotnet</summary>
        public const String ProjectKind = "ProjectKind";

        /// <summary>编译参数。编译项目时所需参数</summary>
        public const String BuildArgs = "BuildArgs";

        /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
        public const String PackageFilters = "PackageFilters";

        /// <summary>文件。应用启动文件，可直接使用zip包</summary>
        public const String FileName = "FileName";

        /// <summary>参数。启动应用的参数</summary>
        public const String Arguments = "Arguments";

        /// <summary>工作目录。应用根目录</summary>
        public const String WorkingDirectory = "WorkingDirectory";

        /// <summary>用户名。以该用户执行应用</summary>
        public const String UserName = "UserName";

        /// <summary>环境变量。启动应用前设置的环境变量</summary>
        public const String Environments = "Environments";

        /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
        public const String MaxMemory = "MaxMemory";

        /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
        public const String Priority = "Priority";

        /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip</summary>
        public const String Mode = "Mode";

        /// <summary>自动停止。随着宿主的退出，同时停止该应用进程</summary>
        public const String AutoStop = "AutoStop";

        /// <summary>检测变动。当文件发生改变时，自动重启应用</summary>
        public const String ReloadOnChange = "ReloadOnChange";

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
