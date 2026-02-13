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

/// <summary>MySql节点。MySql管理</summary>
[Serializable]
[DataObject]
[Description("MySql节点。MySql管理")]
[BindIndex("IU_MySqlNode_Server_Port", true, "Server,Port")]
[BindTable("MySqlNode", Description = "MySql节点。MySql管理", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class MySqlNode
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
    /// <summary>地址。服务器地址</summary>
    [DisplayName("地址")]
    [Description("地址。服务器地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Server", "地址。服务器地址", "")]
    public String Server { get => _Server; set { if (OnPropertyChanging("Server", value)) { _Server = value; OnPropertyChanged("Server"); } } }

    private Int32 _Port;
    /// <summary>端口。默认3306</summary>
    [DisplayName("端口")]
    [Description("端口。默认3306")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Port", "端口。默认3306", "")]
    public Int32 Port { get => _Port; set { if (OnPropertyChanging("Port", value)) { _Port = value; OnPropertyChanged("Port"); } } }

    private String _UserName;
    /// <summary>用户名</summary>
    [DisplayName("用户名")]
    [Description("用户名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名", "")]
    public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String _Password;
    /// <summary>密码</summary>
    [DisplayName("密码")]
    [Description("密码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Password", "密码", "")]
    public String Password { get => _Password; set { if (OnPropertyChanging("Password", value)) { _Password = value; OnPropertyChanged("Password"); } } }

    private String _DatabaseName;
    /// <summary>数据库。监控的数据库名称</summary>
    [DisplayName("数据库")]
    [Description("数据库。监控的数据库名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DatabaseName", "数据库。监控的数据库名称", "")]
    public String DatabaseName { get => _DatabaseName; set { if (OnPropertyChanging("DatabaseName", value)) { _DatabaseName = value; OnPropertyChanged("DatabaseName"); } } }

    private String _Version;
    /// <summary>版本</summary>
    [DisplayName("版本")]
    [Description("版本")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Boolean _Enable;
    /// <summary>启用。停用的节点不再执行监控</summary>
    [DisplayName("启用")]
    [Description("启用。停用的节点不再执行监控")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。停用的节点不再执行监控", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _WebHook;
    /// <summary>告警机器人。钉钉、企业微信等</summary>
    [Category("告警")]
    [DisplayName("告警机器人")]
    [Description("告警机器人。钉钉、企业微信等")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("WebHook", "告警机器人。钉钉、企业微信等", "")]
    public String WebHook { get => _WebHook; set { if (OnPropertyChanging("WebHook", value)) { _WebHook = value; OnPropertyChanged("WebHook"); } } }

    private Int32 _AlarmConnections;
    /// <summary>连接告警。连接数告警阈值</summary>
    [Category("告警")]
    [DisplayName("连接告警")]
    [Description("连接告警。连接数告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmConnections", "连接告警。连接数告警阈值", "")]
    public Int32 AlarmConnections { get => _AlarmConnections; set { if (OnPropertyChanging("AlarmConnections", value)) { _AlarmConnections = value; OnPropertyChanged("AlarmConnections"); } } }

    private Int32 _AlarmQPS;
    /// <summary>QPS告警。每秒查询数告警阈值</summary>
    [Category("告警")]
    [DisplayName("QPS告警")]
    [Description("QPS告警。每秒查询数告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmQPS", "QPS告警。每秒查询数告警阈值", "")]
    public Int32 AlarmQPS { get => _AlarmQPS; set { if (OnPropertyChanging("AlarmQPS", value)) { _AlarmQPS = value; OnPropertyChanged("AlarmQPS"); } } }

    private Int32 _AlarmSlowQuery;
    /// <summary>慢查询告警。慢查询数告警阈值</summary>
    [Category("告警")]
    [DisplayName("慢查询告警")]
    [Description("慢查询告警。慢查询数告警阈值")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmSlowQuery", "慢查询告警。慢查询数告警阈值", "")]
    public Int32 AlarmSlowQuery { get => _AlarmSlowQuery; set { if (OnPropertyChanging("AlarmSlowQuery", value)) { _AlarmSlowQuery = value; OnPropertyChanged("AlarmSlowQuery"); } } }

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
            "Port" => _Port,
            "UserName" => _UserName,
            "Password" => _Password,
            "DatabaseName" => _DatabaseName,
            "Version" => _Version,
            "Enable" => _Enable,
            "WebHook" => _WebHook,
            "AlarmConnections" => _AlarmConnections,
            "AlarmQPS" => _AlarmQPS,
            "AlarmSlowQuery" => _AlarmSlowQuery,
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
                case "Port": _Port = value.ToInt(); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "Password": _Password = Convert.ToString(value); break;
                case "DatabaseName": _DatabaseName = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "WebHook": _WebHook = Convert.ToString(value); break;
                case "AlarmConnections": _AlarmConnections = value.ToInt(); break;
                case "AlarmQPS": _AlarmQPS = value.ToInt(); break;
                case "AlarmSlowQuery": _AlarmSlowQuery = value.ToInt(); break;
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
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static MySqlNode FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据地址、端口查找</summary>
    /// <param name="server">地址</param>
    /// <param name="port">端口</param>
    /// <returns>实体对象</returns>
    public static MySqlNode FindByServerAndPort(String server, Int32 port)
    {
        if (server.IsNullOrEmpty()) return null;
        if (port < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Server.EqualIgnoreCase(server) && e.Port == port);

        return Find(_.Server == server & _.Port == port);
    }

    /// <summary>根据地址查找</summary>
    /// <param name="server">地址</param>
    /// <returns>实体列表</returns>
    public static IList<MySqlNode> FindAllByServer(String server)
    {
        if (server.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Server.EqualIgnoreCase(server));

        return FindAll(_.Server == server);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="server">地址。服务器地址</param>
    /// <param name="port">端口。默认3306</param>
    /// <param name="projectId">项目。资源归属的团队</param>
    /// <param name="enable">启用。停用的节点不再执行监控</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<MySqlNode> Search(String server, Int32 port, Int32 projectId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!server.IsNullOrEmpty()) exp &= _.Server == server;
        if (port >= 0) exp &= _.Port == port;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得MySql节点字段信息的快捷方式</summary>
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

        /// <summary>地址。服务器地址</summary>
        public static readonly Field Server = FindByName("Server");

        /// <summary>端口。默认3306</summary>
        public static readonly Field Port = FindByName("Port");

        /// <summary>用户名</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>密码</summary>
        public static readonly Field Password = FindByName("Password");

        /// <summary>数据库。监控的数据库名称</summary>
        public static readonly Field DatabaseName = FindByName("DatabaseName");

        /// <summary>版本</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>启用。停用的节点不再执行监控</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public static readonly Field WebHook = FindByName("WebHook");

        /// <summary>连接告警。连接数告警阈值</summary>
        public static readonly Field AlarmConnections = FindByName("AlarmConnections");

        /// <summary>QPS告警。每秒查询数告警阈值</summary>
        public static readonly Field AlarmQPS = FindByName("AlarmQPS");

        /// <summary>慢查询告警。慢查询数告警阈值</summary>
        public static readonly Field AlarmSlowQuery = FindByName("AlarmSlowQuery");

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

    /// <summary>取得MySql节点字段名称的快捷方式</summary>
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

        /// <summary>地址。服务器地址</summary>
        public const String Server = "Server";

        /// <summary>端口。默认3306</summary>
        public const String Port = "Port";

        /// <summary>用户名</summary>
        public const String UserName = "UserName";

        /// <summary>密码</summary>
        public const String Password = "Password";

        /// <summary>数据库。监控的数据库名称</summary>
        public const String DatabaseName = "DatabaseName";

        /// <summary>版本</summary>
        public const String Version = "Version";

        /// <summary>启用。停用的节点不再执行监控</summary>
        public const String Enable = "Enable";

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public const String WebHook = "WebHook";

        /// <summary>连接告警。连接数告警阈值</summary>
        public const String AlarmConnections = "AlarmConnections";

        /// <summary>QPS告警。每秒查询数告警阈值</summary>
        public const String AlarmQPS = "AlarmQPS";

        /// <summary>慢查询告警。慢查询数告警阈值</summary>
        public const String AlarmSlowQuery = "AlarmSlowQuery";

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
