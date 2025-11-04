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

namespace Stardust.Data.Platform;

/// <summary>星系项目。一个星系包含多个星星节点，以及多个尘埃应用，完成产品线的项目管理</summary>
[Serializable]
[DataObject]
[Description("星系项目。一个星系包含多个星星节点，以及多个尘埃应用，完成产品线的项目管理")]
[BindIndex("IU_GalaxyProject_Name", true, "Name")]
[BindTable("GalaxyProject", Description = "星系项目。一个星系包含多个星星节点，以及多个尘埃应用，完成产品线的项目管理", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class GalaxyProject
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

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

    private Int32 _TenantId;
    /// <summary>租户。该项目所属租户，实现隔离</summary>
    [DisplayName("租户")]
    [Description("租户。该项目所属租户，实现隔离")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TenantId", "租户。该项目所属租户，实现隔离", "")]
    public Int32 TenantId { get => _TenantId; set { if (OnPropertyChanging("TenantId", value)) { _TenantId = value; OnPropertyChanged("TenantId"); } } }

    private Int32 _ManagerId;
    /// <summary>管理者</summary>
    [DisplayName("管理者")]
    [Description("管理者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ManagerId", "管理者", "")]
    public Int32 ManagerId { get => _ManagerId; set { if (OnPropertyChanging("ManagerId", value)) { _ManagerId = value; OnPropertyChanged("ManagerId"); } } }

    private Int32 _Nodes;
    /// <summary>节点数</summary>
    [DisplayName("节点数")]
    [Description("节点数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Nodes", "节点数", "")]
    public Int32 Nodes { get => _Nodes; set { if (OnPropertyChanging("Nodes", value)) { _Nodes = value; OnPropertyChanged("Nodes"); } } }

    private Int32 _Apps;
    /// <summary>应用数</summary>
    [DisplayName("应用数")]
    [Description("应用数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Apps", "应用数", "")]
    public Int32 Apps { get => _Apps; set { if (OnPropertyChanging("Apps", value)) { _Apps = value; OnPropertyChanged("Apps"); } } }

    private Int32 _Sort;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Boolean _IsGlobal;
    /// <summary>全局。该项目的节点可以允许其它项目下应用选用</summary>
    [DisplayName("全局")]
    [Description("全局。该项目的节点可以允许其它项目下应用选用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsGlobal", "全局。该项目的节点可以允许其它项目下应用选用", "")]
    public Boolean IsGlobal { get => _IsGlobal; set { if (OnPropertyChanging("IsGlobal", value)) { _IsGlobal = value; OnPropertyChanged("IsGlobal"); } } }

    private String _WhiteIPs;
    /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
    [DisplayName("IP白名单")]
    [Description("IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("WhiteIPs", "IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开", "")]
    public String WhiteIPs { get => _WhiteIPs; set { if (OnPropertyChanging("WhiteIPs", value)) { _WhiteIPs = value; OnPropertyChanged("WhiteIPs"); } } }

    private String _BlackIPs;
    /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
    [DisplayName("IP黑名单")]
    [Description("IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("BlackIPs", "IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开", "")]
    public String BlackIPs { get => _BlackIPs; set { if (OnPropertyChanging("BlackIPs", value)) { _BlackIPs = value; OnPropertyChanged("BlackIPs"); } } }

    private String _NewServer;
    /// <summary>新服务器。该项目下的节点自动迁移到新的服务器地址</summary>
    [Category("参数设置")]
    [DisplayName("新服务器")]
    [Description("新服务器。该项目下的节点自动迁移到新的服务器地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("NewServer", "新服务器。该项目下的节点自动迁移到新的服务器地址", "")]
    public String NewServer { get => _NewServer; set { if (OnPropertyChanging("NewServer", value)) { _NewServer = value; OnPropertyChanged("NewServer"); } } }

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
            "Name" => _Name,
            "Enable" => _Enable,
            "TenantId" => _TenantId,
            "ManagerId" => _ManagerId,
            "Nodes" => _Nodes,
            "Apps" => _Apps,
            "Sort" => _Sort,
            "IsGlobal" => _IsGlobal,
            "WhiteIPs" => _WhiteIPs,
            "BlackIPs" => _BlackIPs,
            "NewServer" => _NewServer,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "TenantId": _TenantId = value.ToInt(); break;
                case "ManagerId": _ManagerId = value.ToInt(); break;
                case "Nodes": _Nodes = value.ToInt(); break;
                case "Apps": _Apps = value.ToInt(); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "IsGlobal": _IsGlobal = value.ToBoolean(); break;
                case "WhiteIPs": _WhiteIPs = Convert.ToString(value); break;
                case "BlackIPs": _BlackIPs = Convert.ToString(value); break;
                case "NewServer": _NewServer = Convert.ToString(value); break;
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
    /// <summary>租户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.Tenant Tenant => Extends.Get(nameof(Tenant), k => XCode.Membership.Tenant.FindById(TenantId));

    /// <summary>租户</summary>
    [Map(nameof(TenantId), typeof(XCode.Membership.Tenant), "Id")]
    public String TenantName => Tenant?.ToString();

    /// <summary>管理者</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.User Manager => Extends.Get(nameof(Manager), k => XCode.Membership.User.FindByID(ManagerId));

    /// <summary>管理者</summary>
    [Map(nameof(ManagerId), typeof(XCode.Membership.User), "ID")]
    public String ManagerName => Manager?.ToString();

    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="tenantId">租户。该项目所属租户，实现隔离</param>
    /// <param name="managerId">管理者</param>
    /// <param name="isGlobal">全局。该项目的节点可以允许其它项目下应用选用</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<GalaxyProject> Search(Int32 tenantId, Int32 managerId, Boolean? isGlobal, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (tenantId >= 0) exp &= _.TenantId == tenantId;
        if (managerId >= 0) exp &= _.ManagerId == managerId;
        if (isGlobal != null) exp &= _.IsGlobal == isGlobal;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得星系项目字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>租户。该项目所属租户，实现隔离</summary>
        public static readonly Field TenantId = FindByName("TenantId");

        /// <summary>管理者</summary>
        public static readonly Field ManagerId = FindByName("ManagerId");

        /// <summary>节点数</summary>
        public static readonly Field Nodes = FindByName("Nodes");

        /// <summary>应用数</summary>
        public static readonly Field Apps = FindByName("Apps");

        /// <summary>排序</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>全局。该项目的节点可以允许其它项目下应用选用</summary>
        public static readonly Field IsGlobal = FindByName("IsGlobal");

        /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
        public static readonly Field WhiteIPs = FindByName("WhiteIPs");

        /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
        public static readonly Field BlackIPs = FindByName("BlackIPs");

        /// <summary>新服务器。该项目下的节点自动迁移到新的服务器地址</summary>
        public static readonly Field NewServer = FindByName("NewServer");

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

    /// <summary>取得星系项目字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>租户。该项目所属租户，实现隔离</summary>
        public const String TenantId = "TenantId";

        /// <summary>管理者</summary>
        public const String ManagerId = "ManagerId";

        /// <summary>节点数</summary>
        public const String Nodes = "Nodes";

        /// <summary>应用数</summary>
        public const String Apps = "Apps";

        /// <summary>排序</summary>
        public const String Sort = "Sort";

        /// <summary>全局。该项目的节点可以允许其它项目下应用选用</summary>
        public const String IsGlobal = "IsGlobal";

        /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
        public const String WhiteIPs = "WhiteIPs";

        /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
        public const String BlackIPs = "BlackIPs";

        /// <summary>新服务器。该项目下的节点自动迁移到新的服务器地址</summary>
        public const String NewServer = "NewServer";

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
