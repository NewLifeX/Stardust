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

/// <summary>应用服务。应用提供的服务</summary>
[Serializable]
[DataObject]
[Description("应用服务。应用提供的服务")]
[BindIndex("IX_AppService_AppId", false, "AppId")]
[BindIndex("IX_AppService_ServiceId", false, "ServiceId")]
[BindIndex("IX_AppService_UpdateTime", false, "UpdateTime")]
[BindTable("AppService", Description = "应用服务。应用提供的服务", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppService
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

    private String _Version;
    /// <summary>版本。应用程序版本号</summary>
    [DisplayName("版本")]
    [Description("版本。应用程序版本号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本。应用程序版本号", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _Address;
    /// <summary>地址。经地址模版处理后对外服务的地址，默认是本地局域网服务地址，如http://127.0.0.1:1234</summary>
    [DisplayName("地址")]
    [Description("地址。经地址模版处理后对外服务的地址，默认是本地局域网服务地址，如http://127.0.0.1:1234")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Address", "地址。经地址模版处理后对外服务的地址，默认是本地局域网服务地址，如http://127.0.0.1:1234", "")]
    public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

    private String _OriginAddress;
    /// <summary>原始地址。客户端上报地址，需要经服务端处理后才能对外提供服务</summary>
    [DisplayName("原始地址")]
    [Description("原始地址。客户端上报地址，需要经服务端处理后才能对外提供服务")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("OriginAddress", "原始地址。客户端上报地址，需要经服务端处理后才能对外提供服务", "")]
    public String OriginAddress { get => _OriginAddress; set { if (OnPropertyChanging("OriginAddress", value)) { _OriginAddress = value; OnPropertyChanged("OriginAddress"); } } }

    private Int32 _Weight;
    /// <summary>权重。多实例提供服务时，通过权重系数调节客户端调用各实例服务的比例</summary>
    [DisplayName("权重")]
    [Description("权重。多实例提供服务时，通过权重系数调节客户端调用各实例服务的比例")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Weight", "权重。多实例提供服务时，通过权重系数调节客户端调用各实例服务的比例", "")]
    public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

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

    private Int32 _CheckTimes;
    /// <summary>监测次数。健康监测次数</summary>
    [DisplayName("监测次数")]
    [Description("监测次数。健康监测次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CheckTimes", "监测次数。健康监测次数", "")]
    public Int32 CheckTimes { get => _CheckTimes; set { if (OnPropertyChanging("CheckTimes", value)) { _CheckTimes = value; OnPropertyChanged("CheckTimes"); } } }

    private Boolean _Healthy;
    /// <summary>健康。无需健康监测，或监测后服务可用</summary>
    [DisplayName("健康")]
    [Description("健康。无需健康监测，或监测后服务可用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Healthy", "健康。无需健康监测，或监测后服务可用", "")]
    public Boolean Healthy { get => _Healthy; set { if (OnPropertyChanging("Healthy", value)) { _Healthy = value; OnPropertyChanged("Healthy"); } } }

    private DateTime _LastCheck;
    /// <summary>监测时间。最后一次监测时间，一段时间监测失败后禁用</summary>
    [DisplayName("监测时间")]
    [Description("监测时间。最后一次监测时间，一段时间监测失败后禁用")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastCheck", "监测时间。最后一次监测时间，一段时间监测失败后禁用", "")]
    public DateTime LastCheck { get => _LastCheck; set { if (OnPropertyChanging("LastCheck", value)) { _LastCheck = value; OnPropertyChanged("LastCheck"); } } }

    private String _CheckResult;
    /// <summary>监测结果。检测结果，错误信息等</summary>
    [DisplayName("监测结果")]
    [Description("监测结果。检测结果，错误信息等")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("CheckResult", "监测结果。检测结果，错误信息等", "")]
    public String CheckResult { get => _CheckResult; set { if (OnPropertyChanging("CheckResult", value)) { _CheckResult = value; OnPropertyChanged("CheckResult"); } } }

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
            "Version" => _Version,
            "Address" => _Address,
            "OriginAddress" => _OriginAddress,
            "Weight" => _Weight,
            "Scope" => _Scope,
            "Tag" => _Tag,
            "CheckTimes" => _CheckTimes,
            "Healthy" => _Healthy,
            "LastCheck" => _LastCheck,
            "CheckResult" => _CheckResult,
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
                case "Version": _Version = Convert.ToString(value); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "OriginAddress": _OriginAddress = Convert.ToString(value); break;
                case "Weight": _Weight = value.ToInt(); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Tag": _Tag = Convert.ToString(value); break;
                case "CheckTimes": _CheckTimes = value.ToInt(); break;
                case "Healthy": _Healthy = value.ToBoolean(); break;
                case "LastCheck": _LastCheck = value.ToDateTime(); break;
                case "CheckResult": _CheckResult = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用。提供服务的应用程序</param>
    /// <param name="serviceId">服务</param>
    /// <param name="healthy">健康。无需健康监测，或监测后服务可用</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppService> Search(Int32 appId, Int32 serviceId, Boolean? healthy, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (serviceId >= 0) exp &= _.ServiceId == serviceId;
        if (healthy != null) exp &= _.Healthy == healthy;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用服务字段信息的快捷方式</summary>
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

        /// <summary>版本。应用程序版本号</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>地址。经地址模版处理后对外服务的地址，默认是本地局域网服务地址，如http://127.0.0.1:1234</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>原始地址。客户端上报地址，需要经服务端处理后才能对外提供服务</summary>
        public static readonly Field OriginAddress = FindByName("OriginAddress");

        /// <summary>权重。多实例提供服务时，通过权重系数调节客户端调用各实例服务的比例</summary>
        public static readonly Field Weight = FindByName("Weight");

        /// <summary>作用域。根据配置中心应用规则计算，禁止跨域访问服务</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>标签。带有指定特性，逗号分隔</summary>
        public static readonly Field Tag = FindByName("Tag");

        /// <summary>监测次数。健康监测次数</summary>
        public static readonly Field CheckTimes = FindByName("CheckTimes");

        /// <summary>健康。无需健康监测，或监测后服务可用</summary>
        public static readonly Field Healthy = FindByName("Healthy");

        /// <summary>监测时间。最后一次监测时间，一段时间监测失败后禁用</summary>
        public static readonly Field LastCheck = FindByName("LastCheck");

        /// <summary>监测结果。检测结果，错误信息等</summary>
        public static readonly Field CheckResult = FindByName("CheckResult");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用服务字段名称的快捷方式</summary>
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

        /// <summary>版本。应用程序版本号</summary>
        public const String Version = "Version";

        /// <summary>地址。经地址模版处理后对外服务的地址，默认是本地局域网服务地址，如http://127.0.0.1:1234</summary>
        public const String Address = "Address";

        /// <summary>原始地址。客户端上报地址，需要经服务端处理后才能对外提供服务</summary>
        public const String OriginAddress = "OriginAddress";

        /// <summary>权重。多实例提供服务时，通过权重系数调节客户端调用各实例服务的比例</summary>
        public const String Weight = "Weight";

        /// <summary>作用域。根据配置中心应用规则计算，禁止跨域访问服务</summary>
        public const String Scope = "Scope";

        /// <summary>标签。带有指定特性，逗号分隔</summary>
        public const String Tag = "Tag";

        /// <summary>监测次数。健康监测次数</summary>
        public const String CheckTimes = "CheckTimes";

        /// <summary>健康。无需健康监测，或监测后服务可用</summary>
        public const String Healthy = "Healthy";

        /// <summary>监测时间。最后一次监测时间，一段时间监测失败后禁用</summary>
        public const String LastCheck = "LastCheck";

        /// <summary>监测结果。检测结果，错误信息等</summary>
        public const String CheckResult = "CheckResult";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
