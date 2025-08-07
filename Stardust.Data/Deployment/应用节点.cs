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

/// <summary>应用节点。应用部署集和节点服务器的依赖关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包</summary>
[Serializable]
[DataObject]
[Description("应用节点。应用部署集和节点服务器的依赖关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包")]
[BindIndex("IX_AppDeployNode_DeployId", false, "DeployId")]
[BindIndex("IX_AppDeployNode_NodeId", false, "NodeId")]
[BindTable("AppDeployNode", Description = "应用节点。应用部署集和节点服务器的依赖关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppDeployNode
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _DeployName;
    /// <summary>发布名。默认为空，使用部署集上名字。可用于单节点多发布场景</summary>
    [DisplayName("发布名")]
    [Description("发布名。默认为空，使用部署集上名字。可用于单节点多发布场景")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DeployName", "发布名。默认为空，使用部署集上名字。可用于单节点多发布场景", "")]
    public String DeployName { get => _DeployName; set { if (OnPropertyChanging("DeployName", value)) { _DeployName = value; OnPropertyChanged("DeployName"); } } }

    private Int32 _DeployId;
    /// <summary>应用部署集。对应AppDeploy</summary>
    [DisplayName("应用部署集")]
    [Description("应用部署集。对应AppDeploy")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用部署集。对应AppDeploy", "")]
    public Int32 DeployId { get => _DeployId; set { if (OnPropertyChanging("DeployId", value)) { _DeployId = value; OnPropertyChanged("DeployId"); } } }

    private Int32 _NodeId;
    /// <summary>节点。节点服务器</summary>
    [DisplayName("节点")]
    [Description("节点。节点服务器")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。节点服务器", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private String _IP;
    /// <summary>IP地址。节点所在内网IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址。节点所在内网IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IP", "IP地址。节点所在内网IP地址", "")]
    public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Port;
    /// <summary>应用端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
    [DisplayName("应用端口")]
    [Description("应用端口。应用自身监听的端口，如果是dotnet应用会增加urls参数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Port", "应用端口。应用自身监听的端口，如果是dotnet应用会增加urls参数", "")]
    public Int32 Port { get => _Port; set { if (OnPropertyChanging("Port", value)) { _Port = value; OnPropertyChanged("Port"); } } }

    private String _FileName;
    /// <summary>文件。应用启动文件，可直接使用zip包，支持差异定制，为空时使用应用集配置</summary>
    [Category("发布参数")]
    [DisplayName("文件")]
    [Description("文件。应用启动文件，可直接使用zip包，支持差异定制，为空时使用应用集配置")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FileName", "文件。应用启动文件，可直接使用zip包，支持差异定制，为空时使用应用集配置", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _Arguments;
    /// <summary>参数。启动应用的参数，为空时使用应用集配置</summary>
    [Category("发布参数")]
    [DisplayName("参数")]
    [Description("参数。启动应用的参数，为空时使用应用集配置")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Arguments", "参数。启动应用的参数，为空时使用应用集配置", "")]
    public String Arguments { get => _Arguments; set { if (OnPropertyChanging("Arguments", value)) { _Arguments = value; OnPropertyChanged("Arguments"); } } }

    private String _WorkingDirectory;
    /// <summary>工作目录。应用根目录，为空时使用应用集配置</summary>
    [Category("发布参数")]
    [DisplayName("工作目录")]
    [Description("工作目录。应用根目录，为空时使用应用集配置")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("WorkingDirectory", "工作目录。应用根目录，为空时使用应用集配置", "")]
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
    /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip。为空时使用应用集配置</summary>
    [Category("发布参数")]
    [DisplayName("工作模式")]
    [Description("工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip。为空时使用应用集配置")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Mode", "工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip。为空时使用应用集配置", "")]
    public Stardust.Models.ServiceModes Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private Int32 _Delay;
    /// <summary>延迟。批量发布时，需要延迟执行的时间，用于滚动发布，单位秒</summary>
    [Category("发布参数")]
    [DisplayName("延迟")]
    [Description("延迟。批量发布时，需要延迟执行的时间，用于滚动发布，单位秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Delay", "延迟。批量发布时，需要延迟执行的时间，用于滚动发布，单位秒", "")]
    public Int32 Delay { get => _Delay; set { if (OnPropertyChanging("Delay", value)) { _Delay = value; OnPropertyChanged("Delay"); } } }

    private Int32 _ProcessId;
    /// <summary>进程</summary>
    [Category("状态")]
    [DisplayName("进程")]
    [Description("进程")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProcessId", "进程", "")]
    public Int32 ProcessId { get => _ProcessId; set { if (OnPropertyChanging("ProcessId", value)) { _ProcessId = value; OnPropertyChanged("ProcessId"); } } }

    private String _ProcessName;
    /// <summary>进程名称</summary>
    [Category("状态")]
    [DisplayName("进程名称")]
    [Description("进程名称")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ProcessName", "进程名称", "")]
    public String ProcessName { get => _ProcessName; set { if (OnPropertyChanging("ProcessName", value)) { _ProcessName = value; OnPropertyChanged("ProcessName"); } } }

    private String _ProcessUser;
    /// <summary>进程用户。启动该进程的用户名</summary>
    [Category("状态")]
    [DisplayName("进程用户")]
    [Description("进程用户。启动该进程的用户名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ProcessUser", "进程用户。启动该进程的用户名", "")]
    public String ProcessUser { get => _ProcessUser; set { if (OnPropertyChanging("ProcessUser", value)) { _ProcessUser = value; OnPropertyChanged("ProcessUser"); } } }

    private DateTime _StartTime;
    /// <summary>进程时间</summary>
    [Category("状态")]
    [DisplayName("进程时间")]
    [Description("进程时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartTime", "进程时间", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private String _Version;
    /// <summary>版本。客户端</summary>
    [Category("状态")]
    [DisplayName("版本")]
    [Description("版本。客户端")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本。客户端", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private DateTime _Compile;
    /// <summary>编译时间。客户端</summary>
    [Category("状态")]
    [DisplayName("编译时间")]
    [Description("编译时间。客户端")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Compile", "编译时间。客户端", "")]
    public DateTime Compile { get => _Compile; set { if (OnPropertyChanging("Compile", value)) { _Compile = value; OnPropertyChanged("Compile"); } } }

    private String _Listens;
    /// <summary>监听端口。网络端口监听信息</summary>
    [Category("状态")]
    [DisplayName("监听端口")]
    [Description("监听端口。网络端口监听信息")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Listens", "监听端口。网络端口监听信息", "")]
    public String Listens { get => _Listens; set { if (OnPropertyChanging("Listens", value)) { _Listens = value; OnPropertyChanged("Listens"); } } }

    private DateTime _LastActive;
    /// <summary>最后活跃。最后一次上报心跳的时间</summary>
    [Category("状态")]
    [DisplayName("最后活跃")]
    [Description("最后活跃。最后一次上报心跳的时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastActive", "最后活跃。最后一次上报心跳的时间", "")]
    public DateTime LastActive { get => _LastActive; set { if (OnPropertyChanging("LastActive", value)) { _LastActive = value; OnPropertyChanged("LastActive"); } } }

    private DateTime _LastUpload;
    /// <summary>最后上传。最后一次上传客户端配置的时间</summary>
    [Category("状态")]
    [DisplayName("最后上传")]
    [Description("最后上传。最后一次上传客户端配置的时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastUpload", "最后上传。最后一次上传客户端配置的时间", "")]
    public DateTime LastUpload { get => _LastUpload; set { if (OnPropertyChanging("LastUpload", value)) { _LastUpload = value; OnPropertyChanged("LastUpload"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建人</summary>
    [Category("扩展")]
    [DisplayName("创建人")]
    [Description("创建人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建人", "")]
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
            "DeployName" => _DeployName,
            "DeployId" => _DeployId,
            "NodeId" => _NodeId,
            "IP" => _IP,
            "Enable" => _Enable,
            "Port" => _Port,
            "FileName" => _FileName,
            "Arguments" => _Arguments,
            "WorkingDirectory" => _WorkingDirectory,
            "UserName" => _UserName,
            "Environments" => _Environments,
            "MaxMemory" => _MaxMemory,
            "Priority" => _Priority,
            "Mode" => _Mode,
            "Delay" => _Delay,
            "ProcessId" => _ProcessId,
            "ProcessName" => _ProcessName,
            "ProcessUser" => _ProcessUser,
            "StartTime" => _StartTime,
            "Version" => _Version,
            "Compile" => _Compile,
            "Listens" => _Listens,
            "LastActive" => _LastActive,
            "LastUpload" => _LastUpload,
            "TraceId" => _TraceId,
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
                case "DeployName": _DeployName = Convert.ToString(value); break;
                case "DeployId": _DeployId = value.ToInt(); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Port": _Port = value.ToInt(); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "Arguments": _Arguments = Convert.ToString(value); break;
                case "WorkingDirectory": _WorkingDirectory = Convert.ToString(value); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "Environments": _Environments = Convert.ToString(value); break;
                case "MaxMemory": _MaxMemory = value.ToInt(); break;
                case "Priority": _Priority = (Stardust.Models.ProcessPriority)value.ToInt(); break;
                case "Mode": _Mode = (Stardust.Models.ServiceModes)value.ToInt(); break;
                case "Delay": _Delay = value.ToInt(); break;
                case "ProcessId": _ProcessId = value.ToInt(); break;
                case "ProcessName": _ProcessName = Convert.ToString(value); break;
                case "ProcessUser": _ProcessUser = Convert.ToString(value); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Compile": _Compile = value.ToDateTime(); break;
                case "Listens": _Listens = Convert.ToString(value); break;
                case "LastActive": _LastActive = value.ToDateTime(); break;
                case "LastUpload": _LastUpload = value.ToDateTime(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
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
    /// <summary>应用部署集</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AppDeploy Deploy => Extends.Get(nameof(Deploy), k => AppDeploy.FindById(DeployId));

    #endregion

    #region 扩展查询
    #endregion

    #region 字段名
    /// <summary>取得应用节点字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>发布名。默认为空，使用部署集上名字。可用于单节点多发布场景</summary>
        public static readonly Field DeployName = FindByName("DeployName");

        /// <summary>应用部署集。对应AppDeploy</summary>
        public static readonly Field DeployId = FindByName("DeployId");

        /// <summary>节点。节点服务器</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>IP地址。节点所在内网IP地址</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>应用端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
        public static readonly Field Port = FindByName("Port");

        /// <summary>文件。应用启动文件，可直接使用zip包，支持差异定制，为空时使用应用集配置</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>参数。启动应用的参数，为空时使用应用集配置</summary>
        public static readonly Field Arguments = FindByName("Arguments");

        /// <summary>工作目录。应用根目录，为空时使用应用集配置</summary>
        public static readonly Field WorkingDirectory = FindByName("WorkingDirectory");

        /// <summary>用户名。以该用户执行应用</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>环境变量。启动应用前设置的环境变量</summary>
        public static readonly Field Environments = FindByName("Environments");

        /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
        public static readonly Field MaxMemory = FindByName("MaxMemory");

        /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip。为空时使用应用集配置</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>延迟。批量发布时，需要延迟执行的时间，用于滚动发布，单位秒</summary>
        public static readonly Field Delay = FindByName("Delay");

        /// <summary>进程</summary>
        public static readonly Field ProcessId = FindByName("ProcessId");

        /// <summary>进程名称</summary>
        public static readonly Field ProcessName = FindByName("ProcessName");

        /// <summary>进程用户。启动该进程的用户名</summary>
        public static readonly Field ProcessUser = FindByName("ProcessUser");

        /// <summary>进程时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>版本。客户端</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>编译时间。客户端</summary>
        public static readonly Field Compile = FindByName("Compile");

        /// <summary>监听端口。网络端口监听信息</summary>
        public static readonly Field Listens = FindByName("Listens");

        /// <summary>最后活跃。最后一次上报心跳的时间</summary>
        public static readonly Field LastActive = FindByName("LastActive");

        /// <summary>最后上传。最后一次上传客户端配置的时间</summary>
        public static readonly Field LastUpload = FindByName("LastUpload");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建人</summary>
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

    /// <summary>取得应用节点字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>发布名。默认为空，使用部署集上名字。可用于单节点多发布场景</summary>
        public const String DeployName = "DeployName";

        /// <summary>应用部署集。对应AppDeploy</summary>
        public const String DeployId = "DeployId";

        /// <summary>节点。节点服务器</summary>
        public const String NodeId = "NodeId";

        /// <summary>IP地址。节点所在内网IP地址</summary>
        public const String IP = "IP";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>应用端口。应用自身监听的端口，如果是dotnet应用会增加urls参数</summary>
        public const String Port = "Port";

        /// <summary>文件。应用启动文件，可直接使用zip包，支持差异定制，为空时使用应用集配置</summary>
        public const String FileName = "FileName";

        /// <summary>参数。启动应用的参数，为空时使用应用集配置</summary>
        public const String Arguments = "Arguments";

        /// <summary>工作目录。应用根目录，为空时使用应用集配置</summary>
        public const String WorkingDirectory = "WorkingDirectory";

        /// <summary>用户名。以该用户执行应用</summary>
        public const String UserName = "UserName";

        /// <summary>环境变量。启动应用前设置的环境变量</summary>
        public const String Environments = "Environments";

        /// <summary>最大内存。单位M，超过上限时自动重启应用，默认0不限制</summary>
        public const String MaxMemory = "MaxMemory";

        /// <summary>优先级。表示应用程序中任务或操作的优先级级别</summary>
        public const String Priority = "Priority";

        /// <summary>工作模式。0默认exe/zip；1仅解压；2解压后运行；3仅运行一次；4多实例exe/zip。为空时使用应用集配置</summary>
        public const String Mode = "Mode";

        /// <summary>延迟。批量发布时，需要延迟执行的时间，用于滚动发布，单位秒</summary>
        public const String Delay = "Delay";

        /// <summary>进程</summary>
        public const String ProcessId = "ProcessId";

        /// <summary>进程名称</summary>
        public const String ProcessName = "ProcessName";

        /// <summary>进程用户。启动该进程的用户名</summary>
        public const String ProcessUser = "ProcessUser";

        /// <summary>进程时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>版本。客户端</summary>
        public const String Version = "Version";

        /// <summary>编译时间。客户端</summary>
        public const String Compile = "Compile";

        /// <summary>监听端口。网络端口监听信息</summary>
        public const String Listens = "Listens";

        /// <summary>最后活跃。最后一次上报心跳的时间</summary>
        public const String LastActive = "LastActive";

        /// <summary>最后上传。最后一次上传客户端配置的时间</summary>
        public const String LastUpload = "LastUpload";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建人</summary>
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
