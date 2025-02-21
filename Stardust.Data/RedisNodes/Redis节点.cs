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

namespace Stardust.Data.Nodes;

/// <summary>Redis节点。Redis管理</summary>
[Serializable]
[DataObject]
[Description("Redis节点。Redis管理")]
[BindIndex("IU_RedisNode_Server", true, "Server")]
[BindTable("RedisNode", Description = "Redis节点。Redis管理", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class RedisNode
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

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>分类</summary>
    [DisplayName("分类")]
    [Description("分类")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Server;
    /// <summary>地址。含端口</summary>
    [DisplayName("地址")]
    [Description("地址。含端口")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Server", "地址。含端口", "")]
    public String Server { get => _Server; set { if (OnPropertyChanging("Server", value)) { _Server = value; OnPropertyChanged("Server"); } } }

    private String _UserName;
    /// <summary>用户名称，支持redis7用户名验证</summary>
    [DisplayName("用户名称")]
    [Description("用户名称，支持redis7用户名验证")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名称，支持redis7用户名验证", "")]
    public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String _Password;
    /// <summary>密码</summary>
    [DisplayName("密码")]
    [Description("密码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Password", "密码", "")]
    public String Password { get => _Password; set { if (OnPropertyChanging("Password", value)) { _Password = value; OnPropertyChanged("Password"); } } }

    private String _Version;
    /// <summary>版本</summary>
    [DisplayName("版本")]
    [Description("版本")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _Mode;
    /// <summary>模式</summary>
    [DisplayName("模式")]
    [Description("模式")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Mode", "模式", "")]
    public String Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private String _Role;
    /// <summary>角色</summary>
    [DisplayName("角色")]
    [Description("角色")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Role", "角色", "")]
    public String Role { get => _Role; set { if (OnPropertyChanging("Role", value)) { _Role = value; OnPropertyChanged("Role"); } } }

    private Int32 _MaxMemory;
    /// <summary>内存容量。单位MB</summary>
    [DisplayName("内存容量")]
    [Description("内存容量。单位MB")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxMemory", "内存容量。单位MB", "")]
    public Int32 MaxMemory { get => _MaxMemory; set { if (OnPropertyChanging("MaxMemory", value)) { _MaxMemory = value; OnPropertyChanged("MaxMemory"); } } }

    private String _MemoryPolicy;
    /// <summary>内存策略。缓存淘汰策略</summary>
    [DisplayName("内存策略")]
    [Description("内存策略。缓存淘汰策略")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("MemoryPolicy", "内存策略。缓存淘汰策略", "")]
    public String MemoryPolicy { get => _MemoryPolicy; set { if (OnPropertyChanging("MemoryPolicy", value)) { _MemoryPolicy = value; OnPropertyChanged("MemoryPolicy"); } } }

    private String _MemoryAllocator;
    /// <summary>分配器。内存分配器，低版本有内存泄漏</summary>
    [DisplayName("分配器")]
    [Description("分配器。内存分配器，低版本有内存泄漏")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("MemoryAllocator", "分配器。内存分配器，低版本有内存泄漏", "")]
    public String MemoryAllocator { get => _MemoryAllocator; set { if (OnPropertyChanging("MemoryAllocator", value)) { _MemoryAllocator = value; OnPropertyChanged("MemoryAllocator"); } } }

    private Boolean _Enable;
    /// <summary>启用。停用的节点不再执行监控</summary>
    [DisplayName("启用")]
    [Description("启用。停用的节点不再执行监控")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。停用的节点不再执行监控", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _ScanQueue;
    /// <summary>队列。自动扫描发现消息队列，默认true</summary>
    [DisplayName("队列")]
    [Description("队列。自动扫描发现消息队列，默认true")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ScanQueue", "队列。自动扫描发现消息队列，默认true", "")]
    public Boolean ScanQueue { get => _ScanQueue; set { if (OnPropertyChanging("ScanQueue", value)) { _ScanQueue = value; OnPropertyChanged("ScanQueue"); } } }

    private String _WebHook;
    /// <summary>告警机器人。钉钉、企业微信等</summary>
    [Category("告警")]
    [DisplayName("告警机器人")]
    [Description("告警机器人。钉钉、企业微信等")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("WebHook", "告警机器人。钉钉、企业微信等", "")]
    public String WebHook { get => _WebHook; set { if (OnPropertyChanging("WebHook", value)) { _WebHook = value; OnPropertyChanged("WebHook"); } } }

    private Int32 _AlarmMemoryRate;
    /// <summary>内存告警。内存告警的百分比阈值，百分之一</summary>
    [Category("告警")]
    [DisplayName("内存告警")]
    [Description("内存告警。内存告警的百分比阈值，百分之一")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmMemoryRate", "内存告警。内存告警的百分比阈值，百分之一", "")]
    public Int32 AlarmMemoryRate { get => _AlarmMemoryRate; set { if (OnPropertyChanging("AlarmMemoryRate", value)) { _AlarmMemoryRate = value; OnPropertyChanged("AlarmMemoryRate"); } } }

    private Int32 _AlarmConnections;
    /// <summary>连接告警。连接数告警阈值</summary>
    [Category("告警")]
    [DisplayName("连接告警")]
    [Description("连接告警。连接数告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmConnections", "连接告警。连接数告警阈值", "")]
    public Int32 AlarmConnections { get => _AlarmConnections; set { if (OnPropertyChanging("AlarmConnections", value)) { _AlarmConnections = value; OnPropertyChanged("AlarmConnections"); } } }

    private Int32 _AlarmSpeed;
    /// <summary>速度告警。速度告警阈值</summary>
    [Category("告警")]
    [DisplayName("速度告警")]
    [Description("速度告警。速度告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmSpeed", "速度告警。速度告警阈值", "")]
    public Int32 AlarmSpeed { get => _AlarmSpeed; set { if (OnPropertyChanging("AlarmSpeed", value)) { _AlarmSpeed = value; OnPropertyChanged("AlarmSpeed"); } } }

    private Int32 _AlarmInputKbps;
    /// <summary>入流量告警。入流量告警阈值</summary>
    [Category("告警")]
    [DisplayName("入流量告警")]
    [Description("入流量告警。入流量告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmInputKbps", "入流量告警。入流量告警阈值", "")]
    public Int32 AlarmInputKbps { get => _AlarmInputKbps; set { if (OnPropertyChanging("AlarmInputKbps", value)) { _AlarmInputKbps = value; OnPropertyChanged("AlarmInputKbps"); } } }

    private Int32 _AlarmOutputKbps;
    /// <summary>出流量告警。出流量告警阈值</summary>
    [Category("告警")]
    [DisplayName("出流量告警")]
    [Description("出流量告警。出流量告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmOutputKbps", "出流量告警。出流量告警阈值", "")]
    public Int32 AlarmOutputKbps { get => _AlarmOutputKbps; set { if (OnPropertyChanging("AlarmOutputKbps", value)) { _AlarmOutputKbps = value; OnPropertyChanged("AlarmOutputKbps"); } } }

    private String _CreateUser;
    /// <summary>创建人</summary>
    [Category("扩展")]
    [DisplayName("创建人")]
    [Description("创建人")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建人", "")]
    public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建者", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

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

    private String _UpdateUser;
    /// <summary>更新人</summary>
    [Category("扩展")]
    [DisplayName("更新人")]
    [Description("更新人")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新人", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新者", "")]
    public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

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
            "Name" => _Name,
            "Category" => _Category,
            "Server" => _Server,
            "UserName" => _UserName,
            "Password" => _Password,
            "Version" => _Version,
            "Mode" => _Mode,
            "Role" => _Role,
            "MaxMemory" => _MaxMemory,
            "MemoryPolicy" => _MemoryPolicy,
            "MemoryAllocator" => _MemoryAllocator,
            "Enable" => _Enable,
            "ScanQueue" => _ScanQueue,
            "WebHook" => _WebHook,
            "AlarmMemoryRate" => _AlarmMemoryRate,
            "AlarmConnections" => _AlarmConnections,
            "AlarmSpeed" => _AlarmSpeed,
            "AlarmInputKbps" => _AlarmInputKbps,
            "AlarmOutputKbps" => _AlarmOutputKbps,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUser" => _UpdateUser,
            "UpdateUserID" => _UpdateUserID,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Server": _Server = Convert.ToString(value); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "Password": _Password = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Mode": _Mode = Convert.ToString(value); break;
                case "Role": _Role = Convert.ToString(value); break;
                case "MaxMemory": _MaxMemory = value.ToInt(); break;
                case "MemoryPolicy": _MemoryPolicy = Convert.ToString(value); break;
                case "MemoryAllocator": _MemoryAllocator = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "ScanQueue": _ScanQueue = value.ToBoolean(); break;
                case "WebHook": _WebHook = Convert.ToString(value); break;
                case "AlarmMemoryRate": _AlarmMemoryRate = value.ToInt(); break;
                case "AlarmConnections": _AlarmConnections = value.ToInt(); break;
                case "AlarmSpeed": _AlarmSpeed = value.ToInt(); break;
                case "AlarmInputKbps": _AlarmInputKbps = value.ToInt(); break;
                case "AlarmOutputKbps": _AlarmOutputKbps = value.ToInt(); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
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
    /// <summary>取得Redis节点字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>地址。含端口</summary>
        public static readonly Field Server = FindByName("Server");

        /// <summary>用户名称，支持redis7用户名验证</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>密码</summary>
        public static readonly Field Password = FindByName("Password");

        /// <summary>版本</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>模式</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>角色</summary>
        public static readonly Field Role = FindByName("Role");

        /// <summary>内存容量。单位MB</summary>
        public static readonly Field MaxMemory = FindByName("MaxMemory");

        /// <summary>内存策略。缓存淘汰策略</summary>
        public static readonly Field MemoryPolicy = FindByName("MemoryPolicy");

        /// <summary>分配器。内存分配器，低版本有内存泄漏</summary>
        public static readonly Field MemoryAllocator = FindByName("MemoryAllocator");

        /// <summary>启用。停用的节点不再执行监控</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>队列。自动扫描发现消息队列，默认true</summary>
        public static readonly Field ScanQueue = FindByName("ScanQueue");

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public static readonly Field WebHook = FindByName("WebHook");

        /// <summary>内存告警。内存告警的百分比阈值，百分之一</summary>
        public static readonly Field AlarmMemoryRate = FindByName("AlarmMemoryRate");

        /// <summary>连接告警。连接数告警阈值</summary>
        public static readonly Field AlarmConnections = FindByName("AlarmConnections");

        /// <summary>速度告警。速度告警阈值</summary>
        public static readonly Field AlarmSpeed = FindByName("AlarmSpeed");

        /// <summary>入流量告警。入流量告警阈值</summary>
        public static readonly Field AlarmInputKbps = FindByName("AlarmInputKbps");

        /// <summary>出流量告警。出流量告警阈值</summary>
        public static readonly Field AlarmOutputKbps = FindByName("AlarmOutputKbps");

        /// <summary>创建人</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新人</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

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

    /// <summary>取得Redis节点字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>分类</summary>
        public const String Category = "Category";

        /// <summary>地址。含端口</summary>
        public const String Server = "Server";

        /// <summary>用户名称，支持redis7用户名验证</summary>
        public const String UserName = "UserName";

        /// <summary>密码</summary>
        public const String Password = "Password";

        /// <summary>版本</summary>
        public const String Version = "Version";

        /// <summary>模式</summary>
        public const String Mode = "Mode";

        /// <summary>角色</summary>
        public const String Role = "Role";

        /// <summary>内存容量。单位MB</summary>
        public const String MaxMemory = "MaxMemory";

        /// <summary>内存策略。缓存淘汰策略</summary>
        public const String MemoryPolicy = "MemoryPolicy";

        /// <summary>分配器。内存分配器，低版本有内存泄漏</summary>
        public const String MemoryAllocator = "MemoryAllocator";

        /// <summary>启用。停用的节点不再执行监控</summary>
        public const String Enable = "Enable";

        /// <summary>队列。自动扫描发现消息队列，默认true</summary>
        public const String ScanQueue = "ScanQueue";

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public const String WebHook = "WebHook";

        /// <summary>内存告警。内存告警的百分比阈值，百分之一</summary>
        public const String AlarmMemoryRate = "AlarmMemoryRate";

        /// <summary>连接告警。连接数告警阈值</summary>
        public const String AlarmConnections = "AlarmConnections";

        /// <summary>速度告警。速度告警阈值</summary>
        public const String AlarmSpeed = "AlarmSpeed";

        /// <summary>入流量告警。入流量告警阈值</summary>
        public const String AlarmInputKbps = "AlarmInputKbps";

        /// <summary>出流量告警。出流量告警阈值</summary>
        public const String AlarmOutputKbps = "AlarmOutputKbps";

        /// <summary>创建人</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新人</summary>
        public const String UpdateUser = "UpdateUser";

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
