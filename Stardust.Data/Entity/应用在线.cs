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

namespace Stardust.Data;

/// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
[Serializable]
[DataObject]
[Description("应用在线。一个应用有多个部署，每个在线会话对应一个服务地址")]
[BindIndex("IU_AppOnline_Client", true, "Client")]
[BindIndex("IX_AppOnline_ProjectId", false, "ProjectId")]
[BindIndex("IX_AppOnline_AppId_IP", false, "AppId,IP")]
[BindIndex("IX_AppOnline_Token", false, "Token")]
[BindIndex("IX_AppOnline_IP", false, "IP")]
[BindIndex("IX_AppOnline_UpdateTime", false, "UpdateTime")]
[BindTable("AppOnline", Description = "应用在线。一个应用有多个部署，每个在线会话对应一个服务地址", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class AppOnline
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

    private String _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _Name;
    /// <summary>名称。应用名称</summary>
    [DisplayName("名称")]
    [Description("名称。应用名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。应用名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _NodeId;
    /// <summary>节点。节点服务器</summary>
    [DisplayName("节点")]
    [Description("节点。节点服务器")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。节点服务器", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private String _Client;
    /// <summary>客户端。IP加进程，不同应用的Client可能相同，但几率很低，暂不考虑</summary>
    [DisplayName("客户端")]
    [Description("客户端。IP加进程，不同应用的Client可能相同，但几率很低，暂不考虑")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Client", "客户端。IP加进程，不同应用的Client可能相同，但几率很低，暂不考虑", "")]
    public String Client { get => _Client; set { if (OnPropertyChanging("Client", value)) { _Client = value; OnPropertyChanged("Client"); } } }

    private String _Scope;
    /// <summary>作用域</summary>
    [DisplayName("作用域")]
    [Description("作用域")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Scope", "作用域", "")]
    public String Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private String _Version;
    /// <summary>版本。客户端</summary>
    [DisplayName("版本")]
    [Description("版本。客户端")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本。客户端", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private DateTime _Compile;
    /// <summary>编译时间。客户端</summary>
    [DisplayName("编译时间")]
    [Description("编译时间。客户端")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Compile", "编译时间。客户端", "")]
    public DateTime Compile { get => _Compile; set { if (OnPropertyChanging("Compile", value)) { _Compile = value; OnPropertyChanged("Compile"); } } }

    private Int32 _PingCount;
    /// <summary>心跳</summary>
    [DisplayName("心跳")]
    [Description("心跳")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PingCount", "心跳", "")]
    public Int32 PingCount { get => _PingCount; set { if (OnPropertyChanging("PingCount", value)) { _PingCount = value; OnPropertyChanged("PingCount"); } } }

    private Int32 _WorkerId;
    /// <summary>雪花标识。应用内唯一标识该节点</summary>
    [DisplayName("雪花标识")]
    [Description("雪花标识。应用内唯一标识该节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("WorkerId", "雪花标识。应用内唯一标识该节点", "")]
    public Int32 WorkerId { get => _WorkerId; set { if (OnPropertyChanging("WorkerId", value)) { _WorkerId = value; OnPropertyChanged("WorkerId"); } } }

    private Boolean _WebSocket;
    /// <summary>长连接。WebSocket长连接</summary>
    [DisplayName("长连接")]
    [Description("长连接。WebSocket长连接")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("WebSocket", "长连接。WebSocket长连接", "")]
    public Boolean WebSocket { get => _WebSocket; set { if (OnPropertyChanging("WebSocket", value)) { _WebSocket = value; OnPropertyChanged("WebSocket"); } } }

    private Int32 _ProcessId;
    /// <summary>进程</summary>
    [DisplayName("进程")]
    [Description("进程")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProcessId", "进程", "")]
    public Int32 ProcessId { get => _ProcessId; set { if (OnPropertyChanging("ProcessId", value)) { _ProcessId = value; OnPropertyChanged("ProcessId"); } } }

    private String _ProcessName;
    /// <summary>进程名称</summary>
    [DisplayName("进程名称")]
    [Description("进程名称")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ProcessName", "进程名称", "")]
    public String ProcessName { get => _ProcessName; set { if (OnPropertyChanging("ProcessName", value)) { _ProcessName = value; OnPropertyChanged("ProcessName"); } } }

    private String _CommandLine;
    /// <summary>命令行</summary>
    [DisplayName("命令行")]
    [Description("命令行")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("CommandLine", "命令行", "")]
    public String CommandLine { get => _CommandLine; set { if (OnPropertyChanging("CommandLine", value)) { _CommandLine = value; OnPropertyChanged("CommandLine"); } } }

    private String _UserName;
    /// <summary>用户名。启动该进程的用户名</summary>
    [DisplayName("用户名")]
    [Description("用户名。启动该进程的用户名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名。启动该进程的用户名", "")]
    public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String _MachineName;
    /// <summary>机器名</summary>
    [DisplayName("机器名")]
    [Description("机器名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("MachineName", "机器名", "")]
    public String MachineName { get => _MachineName; set { if (OnPropertyChanging("MachineName", value)) { _MachineName = value; OnPropertyChanged("MachineName"); } } }

    private DateTime _StartTime;
    /// <summary>进程时间</summary>
    [DisplayName("进程时间")]
    [Description("进程时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartTime", "进程时间", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private String _IP;
    /// <summary>本地IP。节点本地IP地址</summary>
    [DisplayName("本地IP")]
    [Description("本地IP。节点本地IP地址")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("IP", "本地IP。节点本地IP地址", "")]
    public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private String _Listens;
    /// <summary>监听端口。网络端口监听信息</summary>
    [DisplayName("监听端口")]
    [Description("监听端口。网络端口监听信息")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Listens", "监听端口。网络端口监听信息", "")]
    public String Listens { get => _Listens; set { if (OnPropertyChanging("Listens", value)) { _Listens = value; OnPropertyChanged("Listens"); } } }

    private String _Token;
    /// <summary>令牌</summary>
    [DisplayName("令牌")]
    [Description("令牌")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Token", "令牌", "")]
    public String Token { get => _Token; set { if (OnPropertyChanging("Token", value)) { _Token = value; OnPropertyChanged("Token"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _Creator;
    /// <summary>创建者。服务端节点</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者。服务端节点")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Creator", "创建者。服务端节点", "")]
    public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

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
            "Category" => _Category,
            "AppId" => _AppId,
            "Name" => _Name,
            "NodeId" => _NodeId,
            "Client" => _Client,
            "Scope" => _Scope,
            "Version" => _Version,
            "Compile" => _Compile,
            "PingCount" => _PingCount,
            "WorkerId" => _WorkerId,
            "WebSocket" => _WebSocket,
            "ProcessId" => _ProcessId,
            "ProcessName" => _ProcessName,
            "CommandLine" => _CommandLine,
            "UserName" => _UserName,
            "MachineName" => _MachineName,
            "StartTime" => _StartTime,
            "IP" => _IP,
            "Listens" => _Listens,
            "Token" => _Token,
            "TraceId" => _TraceId,
            "Creator" => _Creator,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "Client": _Client = Convert.ToString(value); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Compile": _Compile = value.ToDateTime(); break;
                case "PingCount": _PingCount = value.ToInt(); break;
                case "WorkerId": _WorkerId = value.ToInt(); break;
                case "WebSocket": _WebSocket = value.ToBoolean(); break;
                case "ProcessId": _ProcessId = value.ToInt(); break;
                case "ProcessName": _ProcessName = Convert.ToString(value); break;
                case "CommandLine": _CommandLine = Convert.ToString(value); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "MachineName": _MachineName = Convert.ToString(value); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "Listens": _Listens = Convert.ToString(value); break;
                case "Token": _Token = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "Creator": _Creator = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
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
    /// <summary>根据客户端查找</summary>
    /// <param name="client">客户端</param>
    /// <returns>实体对象</returns>
    public static AppOnline FindByClient(String client)
    {
        if (client.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Client.EqualIgnoreCase(client));

        return Find(_.Client == client);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用在线字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>名称。应用名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>节点。节点服务器</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>客户端。IP加进程，不同应用的Client可能相同，但几率很低，暂不考虑</summary>
        public static readonly Field Client = FindByName("Client");

        /// <summary>作用域</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>版本。客户端</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>编译时间。客户端</summary>
        public static readonly Field Compile = FindByName("Compile");

        /// <summary>心跳</summary>
        public static readonly Field PingCount = FindByName("PingCount");

        /// <summary>雪花标识。应用内唯一标识该节点</summary>
        public static readonly Field WorkerId = FindByName("WorkerId");

        /// <summary>长连接。WebSocket长连接</summary>
        public static readonly Field WebSocket = FindByName("WebSocket");

        /// <summary>进程</summary>
        public static readonly Field ProcessId = FindByName("ProcessId");

        /// <summary>进程名称</summary>
        public static readonly Field ProcessName = FindByName("ProcessName");

        /// <summary>命令行</summary>
        public static readonly Field CommandLine = FindByName("CommandLine");

        /// <summary>用户名。启动该进程的用户名</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>机器名</summary>
        public static readonly Field MachineName = FindByName("MachineName");

        /// <summary>进程时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>本地IP。节点本地IP地址</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>监听端口。网络端口监听信息</summary>
        public static readonly Field Listens = FindByName("Listens");

        /// <summary>令牌</summary>
        public static readonly Field Token = FindByName("Token");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建者。服务端节点</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用在线字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>名称。应用名称</summary>
        public const String Name = "Name";

        /// <summary>节点。节点服务器</summary>
        public const String NodeId = "NodeId";

        /// <summary>客户端。IP加进程，不同应用的Client可能相同，但几率很低，暂不考虑</summary>
        public const String Client = "Client";

        /// <summary>作用域</summary>
        public const String Scope = "Scope";

        /// <summary>版本。客户端</summary>
        public const String Version = "Version";

        /// <summary>编译时间。客户端</summary>
        public const String Compile = "Compile";

        /// <summary>心跳</summary>
        public const String PingCount = "PingCount";

        /// <summary>雪花标识。应用内唯一标识该节点</summary>
        public const String WorkerId = "WorkerId";

        /// <summary>长连接。WebSocket长连接</summary>
        public const String WebSocket = "WebSocket";

        /// <summary>进程</summary>
        public const String ProcessId = "ProcessId";

        /// <summary>进程名称</summary>
        public const String ProcessName = "ProcessName";

        /// <summary>命令行</summary>
        public const String CommandLine = "CommandLine";

        /// <summary>用户名。启动该进程的用户名</summary>
        public const String UserName = "UserName";

        /// <summary>机器名</summary>
        public const String MachineName = "MachineName";

        /// <summary>进程时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>本地IP。节点本地IP地址</summary>
        public const String IP = "IP";

        /// <summary>监听端口。网络端口监听信息</summary>
        public const String Listens = "Listens";

        /// <summary>令牌</summary>
        public const String Token = "Token";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建者。服务端节点</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";
    }
    #endregion
}
