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

/// <summary>应用消费。应用消费的服务</summary>
[Serializable]
[DataObject]
[Description("应用消费。应用消费的服务")]
[BindIndex("IX_AppConsume_AppId", false, "AppId")]
[BindIndex("IX_AppConsume_ServiceId", false, "ServiceId")]
[BindIndex("IX_AppConsume_UpdateTime", false, "UpdateTime")]
[BindTable("AppConsume", Description = "应用消费。应用消费的服务", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppConsume
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
    /// <summary>应用。提供服务的应用程序</summary>
    [DisplayName("应用")]
    [Description("应用。提供服务的应用程序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用。提供服务的应用程序", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int32 _ServiceId;
    /// <summary>服务</summary>
    [DisplayName("服务")]
    [Description("服务")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ServiceId", "服务", "")]
    public Int32 ServiceId { get => _ServiceId; set { if (OnPropertyChanging("ServiceId", value)) { _ServiceId = value; OnPropertyChanged("ServiceId"); } } }

    private String _ServiceName;
    /// <summary>服务名</summary>
    [DisplayName("服务名")]
    [Description("服务名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ServiceName", "服务名", "")]
    public String ServiceName { get => _ServiceName; set { if (OnPropertyChanging("ServiceName", value)) { _ServiceName = value; OnPropertyChanged("ServiceName"); } } }

    private String _Client;
    /// <summary>客户端。由该应用实例提供服务，IP加端口</summary>
    [DisplayName("客户端")]
    [Description("客户端。由该应用实例提供服务，IP加端口")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Client", "客户端。由该应用实例提供服务，IP加端口", "")]
    public String Client { get => _Client; set { if (OnPropertyChanging("Client", value)) { _Client = value; OnPropertyChanged("Client"); } } }

    private Int32 _NodeId;
    /// <summary>节点。节点服务器</summary>
    [DisplayName("节点")]
    [Description("节点。节点服务器")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。节点服务器", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _PingCount;
    /// <summary>心跳。应用程序定期向注册中心更新服务状态</summary>
    [DisplayName("心跳")]
    [Description("心跳。应用程序定期向注册中心更新服务状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PingCount", "心跳。应用程序定期向注册中心更新服务状态", "")]
    public Int32 PingCount { get => _PingCount; set { if (OnPropertyChanging("PingCount", value)) { _PingCount = value; OnPropertyChanged("PingCount"); } } }

    private String _MinVersion;
    /// <summary>最低版本。要求返回大于等于该版本的服务提供者</summary>
    [DisplayName("最低版本")]
    [Description("最低版本。要求返回大于等于该版本的服务提供者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("MinVersion", "最低版本。要求返回大于等于该版本的服务提供者", "")]
    public String MinVersion { get => _MinVersion; set { if (OnPropertyChanging("MinVersion", value)) { _MinVersion = value; OnPropertyChanged("MinVersion"); } } }

    private String _Scope;
    /// <summary>作用域。根据配置中心应用规则计算，禁止跨域访问服务</summary>
    [DisplayName("作用域")]
    [Description("作用域。根据配置中心应用规则计算，禁止跨域访问服务")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Scope", "作用域。根据配置中心应用规则计算，禁止跨域访问服务", "")]
    public String Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private String _Tag;
    /// <summary>标签。带有指定特性，逗号分隔</summary>
    [DisplayName("标签")]
    [Description("标签。带有指定特性，逗号分隔")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Tag", "标签。带有指定特性，逗号分隔", "")]
    public String Tag { get => _Tag; set { if (OnPropertyChanging("Tag", value)) { _Tag = value; OnPropertyChanged("Tag"); } } }

    private String _Address;
    /// <summary>地址。最终消费得到的地址</summary>
    [DisplayName("地址")]
    [Description("地址。最终消费得到的地址")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Address", "地址。最终消费得到的地址", "")]
    public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

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
            "AppId" => _AppId,
            "ServiceId" => _ServiceId,
            "ServiceName" => _ServiceName,
            "Client" => _Client,
            "NodeId" => _NodeId,
            "Enable" => _Enable,
            "PingCount" => _PingCount,
            "MinVersion" => _MinVersion,
            "Scope" => _Scope,
            "Tag" => _Tag,
            "Address" => _Address,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "ServiceId": _ServiceId = value.ToInt(); break;
                case "ServiceName": _ServiceName = Convert.ToString(value); break;
                case "Client": _Client = Convert.ToString(value); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "PingCount": _PingCount = value.ToInt(); break;
                case "MinVersion": _MinVersion = Convert.ToString(value); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Tag": _Tag = Convert.ToString(value); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>应用</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.App App => Extends.Get(nameof(App), k => Stardust.Data.App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(nameof(AppId), typeof(Stardust.Data.App), "Id")]
    public String AppName => App?.Name;

    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Nodes.Node Node => Extends.Get(nameof(Node), k => Stardust.Data.Nodes.Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(nameof(NodeId), typeof(Stardust.Data.Nodes.Node), "ID")]
    public String NodeName => Node?.Name;

    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用。提供服务的应用程序</param>
    /// <param name="serviceId">服务</param>
    /// <param name="nodeId">节点。节点服务器</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppConsume> Search(Int32 appId, Int32 serviceId, Int32 nodeId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (serviceId >= 0) exp &= _.ServiceId == serviceId;
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用消费字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用。提供服务的应用程序</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>服务</summary>
        public static readonly Field ServiceId = FindByName("ServiceId");

        /// <summary>服务名</summary>
        public static readonly Field ServiceName = FindByName("ServiceName");

        /// <summary>客户端。由该应用实例提供服务，IP加端口</summary>
        public static readonly Field Client = FindByName("Client");

        /// <summary>节点。节点服务器</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>心跳。应用程序定期向注册中心更新服务状态</summary>
        public static readonly Field PingCount = FindByName("PingCount");

        /// <summary>最低版本。要求返回大于等于该版本的服务提供者</summary>
        public static readonly Field MinVersion = FindByName("MinVersion");

        /// <summary>作用域。根据配置中心应用规则计算，禁止跨域访问服务</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>标签。带有指定特性，逗号分隔</summary>
        public static readonly Field Tag = FindByName("Tag");

        /// <summary>地址。最终消费得到的地址</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用消费字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用。提供服务的应用程序</summary>
        public const String AppId = "AppId";

        /// <summary>服务</summary>
        public const String ServiceId = "ServiceId";

        /// <summary>服务名</summary>
        public const String ServiceName = "ServiceName";

        /// <summary>客户端。由该应用实例提供服务，IP加端口</summary>
        public const String Client = "Client";

        /// <summary>节点。节点服务器</summary>
        public const String NodeId = "NodeId";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>心跳。应用程序定期向注册中心更新服务状态</summary>
        public const String PingCount = "PingCount";

        /// <summary>最低版本。要求返回大于等于该版本的服务提供者</summary>
        public const String MinVersion = "MinVersion";

        /// <summary>作用域。根据配置中心应用规则计算，禁止跨域访问服务</summary>
        public const String Scope = "Scope";

        /// <summary>标签。带有指定特性，逗号分隔</summary>
        public const String Tag = "Tag";

        /// <summary>地址。最终消费得到的地址</summary>
        public const String Address = "Address";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
