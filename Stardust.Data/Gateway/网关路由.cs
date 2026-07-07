using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Gateway;

/// <summary>网关路由。请求匹配规则，定义如何将请求转发到目标集群</summary>
[Serializable]
[DataObject]
[Description("网关路由。请求匹配规则，定义如何将请求转发到目标集群")]
[BindIndex("IU_GatewayRoute_Name", true, "Name")]
[BindIndex("IX_GatewayRoute_ProjectId", false, "ProjectId")]
[BindIndex("IX_GatewayRoute_ClusterId", false, "ClusterId")]
[BindIndex("IX_GatewayRoute_Domain", false, "Domain")]
[BindIndex("IX_GatewayRoute_Priority", false, "Priority")]
[BindTable("GatewayRoute", Description = "网关路由。请求匹配规则，定义如何将请求转发到目标集群", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class GatewayRoute
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
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "", DefaultValue = "True")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Priority;
    /// <summary>优先级。数值越大优先级越高</summary>
    [DisplayName("优先级")]
    [Description("优先级。数值越大优先级越高")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "优先级。数值越大优先级越高", "", DefaultValue = "0")]
    public Int32 Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private Int32 _ClusterId;
    /// <summary>集群。目标后端集群</summary>
    [DisplayName("集群")]
    [Description("集群。目标后端集群")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ClusterId", "集群。目标后端集群", "")]
    public Int32 ClusterId { get => _ClusterId; set { if (OnPropertyChanging("ClusterId", value)) { _ClusterId = value; OnPropertyChanged("ClusterId"); } } }

    private String _Domain;
    /// <summary>域名匹配。支持通配符 *.example.com，多个用逗号分隔</summary>
    [DisplayName("域名匹配")]
    [Description("域名匹配。支持通配符 *.example.com，多个用逗号分隔")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Domain", "域名匹配。支持通配符 *.example.com，多个用逗号分隔", "")]
    public String Domain { get => _Domain; set { if (OnPropertyChanging("Domain", value)) { _Domain = value; OnPropertyChanged("Domain"); } } }

    private String _Path;
    /// <summary>路径匹配。如 /api/*，多个用逗号分隔</summary>
    [DisplayName("路径匹配")]
    [Description("路径匹配。如 /api/*，多个用逗号分隔")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Path", "路径匹配。如 /api/*，多个用逗号分隔", "")]
    public String Path { get => _Path; set { if (OnPropertyChanging("Path", value)) { _Path = value; OnPropertyChanged("Path"); } } }

    private String _Methods;
    /// <summary>HTTP方法。GET,POST,PUT,DELETE,PATCH，为空表示全部</summary>
    [DisplayName("HTTP方法")]
    [Description("HTTP方法。GET,POST,PUT,DELETE,PATCH，为空表示全部")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Methods", "HTTP方法。GET,POST,PUT,DELETE,PATCH，为空表示全部", "")]
    public String Methods { get => _Methods; set { if (OnPropertyChanging("Methods", value)) { _Methods = value; OnPropertyChanged("Methods"); } } }

    private String _Headers;
    /// <summary>请求头匹配。JSON格式，如 {key:value}</summary>
    [DisplayName("请求头匹配")]
    [Description("请求头匹配。JSON格式，如 {key:value}")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Headers", "请求头匹配。JSON格式，如 {key:value}", "")]
    public String Headers { get => _Headers; set { if (OnPropertyChanging("Headers", value)) { _Headers = value; OnPropertyChanged("Headers"); } } }

    private Boolean _StripPrefix;
    /// <summary>去除前缀。转发时去除匹配的路径前缀</summary>
    [DisplayName("去除前缀")]
    [Description("去除前缀。转发时去除匹配的路径前缀")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StripPrefix", "去除前缀。转发时去除匹配的路径前缀", "")]
    public Boolean StripPrefix { get => _StripPrefix; set { if (OnPropertyChanging("StripPrefix", value)) { _StripPrefix = value; OnPropertyChanged("StripPrefix"); } } }

    private String _AddHeaders;
    /// <summary>添加请求头。JSON格式，如 {key:value}</summary>
    [DisplayName("添加请求头")]
    [Description("添加请求头。JSON格式，如 {key:value}")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("AddHeaders", "添加请求头。JSON格式，如 {key:value}", "")]
    public String AddHeaders { get => _AddHeaders; set { if (OnPropertyChanging("AddHeaders", value)) { _AddHeaders = value; OnPropertyChanged("AddHeaders"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private String _CreateUser;
    /// <summary>创建者</summary>
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _UpdateUser;
    /// <summary>更新者</summary>
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
            "Enable" => _Enable,
            "Priority" => _Priority,
            "ClusterId" => _ClusterId,
            "Domain" => _Domain,
            "Path" => _Path,
            "Methods" => _Methods,
            "Headers" => _Headers,
            "StripPrefix" => _StripPrefix,
            "AddHeaders" => _AddHeaders,
            "Remark" => _Remark,
            "CreateUser" => _CreateUser,
            "CreateTime" => _CreateTime,
            "UpdateUser" => _UpdateUser,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Priority": _Priority = value.ToInt(); break;
                case "ClusterId": _ClusterId = value.ToInt(); break;
                case "Domain": _Domain = Convert.ToString(value); break;
                case "Path": _Path = Convert.ToString(value); break;
                case "Methods": _Methods = Convert.ToString(value); break;
                case "Headers": _Headers = Convert.ToString(value); break;
                case "StripPrefix": _StripPrefix = value.ToBoolean(); break;
                case "AddHeaders": _AddHeaders = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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

    /// <summary>集群</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Gateway.GatewayCluster Cluster => Extends.Get(nameof(Cluster), k => Stardust.Data.Gateway.GatewayCluster.FindById(ClusterId));

    /// <summary>集群</summary>
    [Map(nameof(ClusterId), typeof(Stardust.Data.Gateway.GatewayCluster), "Id")]
    public String ClusterName => Cluster?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static GatewayRoute FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static GatewayRoute FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as GatewayRoute;

        //return Find(_.Name == name);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayRoute> FindAllByProjectId(Int32 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据集群查找</summary>
    /// <param name="clusterId">集群</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayRoute> FindAllByClusterId(Int32 clusterId)
    {
        if (clusterId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ClusterId == clusterId);

        return FindAll(_.ClusterId == clusterId);
    }

    /// <summary>根据域名匹配查找</summary>
    /// <param name="domain">域名匹配</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayRoute> FindAllByDomain(String domain)
    {
        if (domain.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Domain.EqualIgnoreCase(domain));

        return FindAll(_.Domain == domain);
    }

    /// <summary>根据优先级查找</summary>
    /// <param name="priority">优先级</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayRoute> FindAllByPriority(Int32 priority)
    {
        if (priority < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Priority == priority);

        return FindAll(_.Priority == priority);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目。资源归属的团队</param>
    /// <param name="priority">优先级。数值越大优先级越高</param>
    /// <param name="clusterId">集群。目标后端集群</param>
    /// <param name="domain">域名匹配。支持通配符 *.example.com，多个用逗号分隔</param>
    /// <param name="stripPrefix">去除前缀。转发时去除匹配的路径前缀</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayRoute> Search(Int32 projectId, Int32 priority, Int32 clusterId, String domain, Boolean? stripPrefix, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (priority >= 0) exp &= _.Priority == priority;
        if (clusterId >= 0) exp &= _.ClusterId == clusterId;
        if (!domain.IsNullOrEmpty()) exp &= _.Domain == domain;
        if (stripPrefix != null) exp &= _.StripPrefix == stripPrefix;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得网关路由字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>优先级。数值越大优先级越高</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>集群。目标后端集群</summary>
        public static readonly Field ClusterId = FindByName("ClusterId");

        /// <summary>域名匹配。支持通配符 *.example.com，多个用逗号分隔</summary>
        public static readonly Field Domain = FindByName("Domain");

        /// <summary>路径匹配。如 /api/*，多个用逗号分隔</summary>
        public static readonly Field Path = FindByName("Path");

        /// <summary>HTTP方法。GET,POST,PUT,DELETE,PATCH，为空表示全部</summary>
        public static readonly Field Methods = FindByName("Methods");

        /// <summary>请求头匹配。JSON格式，如 {key:value}</summary>
        public static readonly Field Headers = FindByName("Headers");

        /// <summary>去除前缀。转发时去除匹配的路径前缀</summary>
        public static readonly Field StripPrefix = FindByName("StripPrefix");

        /// <summary>添加请求头。JSON格式，如 {key:value}</summary>
        public static readonly Field AddHeaders = FindByName("AddHeaders");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得网关路由字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>优先级。数值越大优先级越高</summary>
        public const String Priority = "Priority";

        /// <summary>集群。目标后端集群</summary>
        public const String ClusterId = "ClusterId";

        /// <summary>域名匹配。支持通配符 *.example.com，多个用逗号分隔</summary>
        public const String Domain = "Domain";

        /// <summary>路径匹配。如 /api/*，多个用逗号分隔</summary>
        public const String Path = "Path";

        /// <summary>HTTP方法。GET,POST,PUT,DELETE,PATCH，为空表示全部</summary>
        public const String Methods = "Methods";

        /// <summary>请求头匹配。JSON格式，如 {key:value}</summary>
        public const String Headers = "Headers";

        /// <summary>去除前缀。转发时去除匹配的路径前缀</summary>
        public const String StripPrefix = "StripPrefix";

        /// <summary>添加请求头。JSON格式，如 {key:value}</summary>
        public const String AddHeaders = "AddHeaders";

        /// <summary>备注</summary>
        public const String Remark = "Remark";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
