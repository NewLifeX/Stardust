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

/// <summary>网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置</summary>
[Serializable]
[DataObject]
[Description("网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置")]
[BindIndex("IU_GatewayCluster_Name", true, "Name")]
[BindIndex("IX_GatewayCluster_ProjectId", false, "ProjectId")]
[BindTable("GatewayCluster", Description = "网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class GatewayCluster
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

    private String _LoadBalance;
    /// <summary>负载均衡算法。RoundRobin/LeastConnection/IPHash</summary>
    [DisplayName("负载均衡算法")]
    [Description("负载均衡算法。RoundRobin/LeastConnection/IPHash")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("LoadBalance", "负载均衡算法。RoundRobin/LeastConnection/IPHash", "", DefaultValue = "RoundRobin")]
    public String LoadBalance { get => _LoadBalance; set { if (OnPropertyChanging("LoadBalance", value)) { _LoadBalance = value; OnPropertyChanged("LoadBalance"); } } }

    private String _HealthPath;
    /// <summary>健康检查路径。如 /health</summary>
    [DisplayName("健康检查路径")]
    [Description("健康检查路径。如 /health")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("HealthPath", "健康检查路径。如 /health", "")]
    public String HealthPath { get => _HealthPath; set { if (OnPropertyChanging("HealthPath", value)) { _HealthPath = value; OnPropertyChanged("HealthPath"); } } }

    private Int32 _HealthInterval;
    /// <summary>健康检查间隔。单位秒，默认10秒</summary>
    [DisplayName("健康检查间隔")]
    [Description("健康检查间隔。单位秒，默认10秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HealthInterval", "健康检查间隔。单位秒，默认10秒", "", DefaultValue = "10")]
    public Int32 HealthInterval { get => _HealthInterval; set { if (OnPropertyChanging("HealthInterval", value)) { _HealthInterval = value; OnPropertyChanged("HealthInterval"); } } }

    private Int32 _HealthTimeout;
    /// <summary>健康检查超时。单位毫秒，默认3000ms</summary>
    [DisplayName("健康检查超时")]
    [Description("健康检查超时。单位毫秒，默认3000ms")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HealthTimeout", "健康检查超时。单位毫秒，默认3000ms", "", DefaultValue = "3000")]
    public Int32 HealthTimeout { get => _HealthTimeout; set { if (OnPropertyChanging("HealthTimeout", value)) { _HealthTimeout = value; OnPropertyChanged("HealthTimeout"); } } }

    private Int32 _UnhealthyThreshold;
    /// <summary>不健康阈值。连续失败次数，默认3次</summary>
    [DisplayName("不健康阈值")]
    [Description("不健康阈值。连续失败次数，默认3次")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UnhealthyThreshold", "不健康阈值。连续失败次数，默认3次", "", DefaultValue = "3")]
    public Int32 UnhealthyThreshold { get => _UnhealthyThreshold; set { if (OnPropertyChanging("UnhealthyThreshold", value)) { _UnhealthyThreshold = value; OnPropertyChanged("UnhealthyThreshold"); } } }

    private Int32 _HealthyThreshold;
    /// <summary>健康阈值。连续成功次数，默认2次</summary>
    [DisplayName("健康阈值")]
    [Description("健康阈值。连续成功次数，默认2次")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HealthyThreshold", "健康阈值。连续成功次数，默认2次", "", DefaultValue = "2")]
    public Int32 HealthyThreshold { get => _HealthyThreshold; set { if (OnPropertyChanging("HealthyThreshold", value)) { _HealthyThreshold = value; OnPropertyChanged("HealthyThreshold"); } } }

    private Boolean _SessionSticky;
    /// <summary>会话保持。开启后同一来源IP转发到同一后端</summary>
    [DisplayName("会话保持")]
    [Description("会话保持。开启后同一来源IP转发到同一后端")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SessionSticky", "会话保持。开启后同一来源IP转发到同一后端", "")]
    public Boolean SessionSticky { get => _SessionSticky; set { if (OnPropertyChanging("SessionSticky", value)) { _SessionSticky = value; OnPropertyChanged("SessionSticky"); } } }

    private String _SessionStickyName;
    /// <summary>会话保持标识。如 Cookie 名</summary>
    [DisplayName("会话保持标识")]
    [Description("会话保持标识。如 Cookie 名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionStickyName", "会话保持标识。如 Cookie 名", "")]
    public String SessionStickyName { get => _SessionStickyName; set { if (OnPropertyChanging("SessionStickyName", value)) { _SessionStickyName = value; OnPropertyChanged("SessionStickyName"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "", DefaultValue = "True")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
            "LoadBalance" => _LoadBalance,
            "HealthPath" => _HealthPath,
            "HealthInterval" => _HealthInterval,
            "HealthTimeout" => _HealthTimeout,
            "UnhealthyThreshold" => _UnhealthyThreshold,
            "HealthyThreshold" => _HealthyThreshold,
            "SessionSticky" => _SessionSticky,
            "SessionStickyName" => _SessionStickyName,
            "Enable" => _Enable,
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
                case "LoadBalance": _LoadBalance = Convert.ToString(value); break;
                case "HealthPath": _HealthPath = Convert.ToString(value); break;
                case "HealthInterval": _HealthInterval = value.ToInt(); break;
                case "HealthTimeout": _HealthTimeout = value.ToInt(); break;
                case "UnhealthyThreshold": _UnhealthyThreshold = value.ToInt(); break;
                case "HealthyThreshold": _HealthyThreshold = value.ToInt(); break;
                case "SessionSticky": _SessionSticky = value.ToBoolean(); break;
                case "SessionStickyName": _SessionStickyName = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static GatewayCluster FindById(Int32 id)
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
    public static GatewayCluster FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as GatewayCluster;

        //return Find(_.Name == name);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayCluster> FindAllByProjectId(Int32 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目。资源归属的团队</param>
    /// <param name="sessionSticky">会话保持。开启后同一来源IP转发到同一后端</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayCluster> Search(Int32 projectId, Boolean? sessionSticky, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (sessionSticky != null) exp &= _.SessionSticky == sessionSticky;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得网关集群字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>负载均衡算法。RoundRobin/LeastConnection/IPHash</summary>
        public static readonly Field LoadBalance = FindByName("LoadBalance");

        /// <summary>健康检查路径。如 /health</summary>
        public static readonly Field HealthPath = FindByName("HealthPath");

        /// <summary>健康检查间隔。单位秒，默认10秒</summary>
        public static readonly Field HealthInterval = FindByName("HealthInterval");

        /// <summary>健康检查超时。单位毫秒，默认3000ms</summary>
        public static readonly Field HealthTimeout = FindByName("HealthTimeout");

        /// <summary>不健康阈值。连续失败次数，默认3次</summary>
        public static readonly Field UnhealthyThreshold = FindByName("UnhealthyThreshold");

        /// <summary>健康阈值。连续成功次数，默认2次</summary>
        public static readonly Field HealthyThreshold = FindByName("HealthyThreshold");

        /// <summary>会话保持。开启后同一来源IP转发到同一后端</summary>
        public static readonly Field SessionSticky = FindByName("SessionSticky");

        /// <summary>会话保持标识。如 Cookie 名</summary>
        public static readonly Field SessionStickyName = FindByName("SessionStickyName");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

    /// <summary>取得网关集群字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>负载均衡算法。RoundRobin/LeastConnection/IPHash</summary>
        public const String LoadBalance = "LoadBalance";

        /// <summary>健康检查路径。如 /health</summary>
        public const String HealthPath = "HealthPath";

        /// <summary>健康检查间隔。单位秒，默认10秒</summary>
        public const String HealthInterval = "HealthInterval";

        /// <summary>健康检查超时。单位毫秒，默认3000ms</summary>
        public const String HealthTimeout = "HealthTimeout";

        /// <summary>不健康阈值。连续失败次数，默认3次</summary>
        public const String UnhealthyThreshold = "UnhealthyThreshold";

        /// <summary>健康阈值。连续成功次数，默认2次</summary>
        public const String HealthyThreshold = "HealthyThreshold";

        /// <summary>会话保持。开启后同一来源IP转发到同一后端</summary>
        public const String SessionSticky = "SessionSticky";

        /// <summary>会话保持标识。如 Cookie 名</summary>
        public const String SessionStickyName = "SessionStickyName";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
