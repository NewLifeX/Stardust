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

/// <summary>网关节点。集群中的后端服务器节点</summary>
[Serializable]
[DataObject]
[Description("网关节点。集群中的后端服务器节点")]
[BindIndex("IX_GatewayNode_ClusterId", false, "ClusterId")]
[BindIndex("IX_GatewayNode_Address", false, "Address")]
[BindTable("GatewayNode", Description = "网关节点。集群中的后端服务器节点", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class GatewayNode
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ClusterId;
    /// <summary>集群。所属网关集群</summary>
    [DisplayName("集群")]
    [Description("集群。所属网关集群")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ClusterId", "集群。所属网关集群", "")]
    public Int32 ClusterId { get => _ClusterId; set { if (OnPropertyChanging("ClusterId", value)) { _ClusterId = value; OnPropertyChanged("ClusterId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Address;
    /// <summary>地址。如 http://192.168.1.100:5000</summary>
    [DisplayName("地址")]
    [Description("地址。如 http://192.168.1.100:5000")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Address", "地址。如 http://192.168.1.100:5000", "")]
    public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

    private Int32 _Weight;
    /// <summary>权重。负载均衡权重，默认1</summary>
    [DisplayName("权重")]
    [Description("权重。负载均衡权重，默认1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Weight", "权重。负载均衡权重，默认1", "", DefaultValue = "1")]
    public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "", DefaultValue = "True")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _IsHealthy;
    /// <summary>是否健康。运行时状态</summary>
    [DisplayName("是否健康")]
    [Description("是否健康。运行时状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsHealthy", "是否健康。运行时状态", "", DefaultValue = "True")]
    public Boolean IsHealthy { get => _IsHealthy; set { if (OnPropertyChanging("IsHealthy", value)) { _IsHealthy = value; OnPropertyChanged("IsHealthy"); } } }

    private Int32 _MaxFails;
    /// <summary>最大失败次数。超过后暂时移除</summary>
    [DisplayName("最大失败次数")]
    [Description("最大失败次数。超过后暂时移除")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxFails", "最大失败次数。超过后暂时移除", "", DefaultValue = "3")]
    public Int32 MaxFails { get => _MaxFails; set { if (OnPropertyChanging("MaxFails", value)) { _MaxFails = value; OnPropertyChanged("MaxFails"); } } }

    private Int32 _FailTimeout;
    /// <summary>失败超时。单位秒，超时后重新加入，默认60秒</summary>
    [DisplayName("失败超时")]
    [Description("失败超时。单位秒，超时后重新加入，默认60秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FailTimeout", "失败超时。单位秒，超时后重新加入，默认60秒", "", DefaultValue = "60")]
    public Int32 FailTimeout { get => _FailTimeout; set { if (OnPropertyChanging("FailTimeout", value)) { _FailTimeout = value; OnPropertyChanged("FailTimeout"); } } }

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
            "ClusterId" => _ClusterId,
            "Name" => _Name,
            "Address" => _Address,
            "Weight" => _Weight,
            "Enable" => _Enable,
            "IsHealthy" => _IsHealthy,
            "MaxFails" => _MaxFails,
            "FailTimeout" => _FailTimeout,
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
                case "ClusterId": _ClusterId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "Weight": _Weight = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "IsHealthy": _IsHealthy = value.ToBoolean(); break;
                case "MaxFails": _MaxFails = value.ToInt(); break;
                case "FailTimeout": _FailTimeout = value.ToInt(); break;
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
    public static GatewayNode FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据集群查找</summary>
    /// <param name="clusterId">集群</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayNode> FindAllByClusterId(Int32 clusterId)
    {
        if (clusterId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ClusterId == clusterId);

        return FindAll(_.ClusterId == clusterId);
    }

    /// <summary>根据地址查找</summary>
    /// <param name="address">地址</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayNode> FindAllByAddress(String address)
    {
        if (address.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Address.EqualIgnoreCase(address));

        return FindAll(_.Address == address);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="clusterId">集群。所属网关集群</param>
    /// <param name="address">地址。如 http://192.168.1.100:5000</param>
    /// <param name="isHealthy">是否健康。运行时状态</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayNode> Search(Int32 clusterId, String address, Boolean? isHealthy, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (clusterId >= 0) exp &= _.ClusterId == clusterId;
        if (!address.IsNullOrEmpty()) exp &= _.Address == address;
        if (isHealthy != null) exp &= _.IsHealthy == isHealthy;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得网关节点字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>集群。所属网关集群</summary>
        public static readonly Field ClusterId = FindByName("ClusterId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>地址。如 http://192.168.1.100:5000</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>权重。负载均衡权重，默认1</summary>
        public static readonly Field Weight = FindByName("Weight");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>是否健康。运行时状态</summary>
        public static readonly Field IsHealthy = FindByName("IsHealthy");

        /// <summary>最大失败次数。超过后暂时移除</summary>
        public static readonly Field MaxFails = FindByName("MaxFails");

        /// <summary>失败超时。单位秒，超时后重新加入，默认60秒</summary>
        public static readonly Field FailTimeout = FindByName("FailTimeout");

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

    /// <summary>取得网关节点字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>集群。所属网关集群</summary>
        public const String ClusterId = "ClusterId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>地址。如 http://192.168.1.100:5000</summary>
        public const String Address = "Address";

        /// <summary>权重。负载均衡权重，默认1</summary>
        public const String Weight = "Weight";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>是否健康。运行时状态</summary>
        public const String IsHealthy = "IsHealthy";

        /// <summary>最大失败次数。超过后暂时移除</summary>
        public const String MaxFails = "MaxFails";

        /// <summary>失败超时。单位秒，超时后重新加入，默认60秒</summary>
        public const String FailTimeout = "FailTimeout";

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
