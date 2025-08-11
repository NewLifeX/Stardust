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

namespace Stardust.Data.Monitors;

/// <summary>跟踪项。应用下的多个埋点</summary>
[Serializable]
[DataObject]
[Description("跟踪项。应用下的多个埋点")]
[BindIndex("IU_TraceItem_AppId_Name", true, "AppId,Name")]
[BindIndex("IX_TraceItem_Kind_AppId", false, "Kind,AppId")]
[BindTable("TraceItem", Description = "跟踪项。应用下的多个埋点", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class TraceItem
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
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _Kind;
    /// <summary>种类</summary>
    [DisplayName("种类")]
    [Description("种类")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Kind", "种类", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Name;
    /// <summary>操作名。接口名或埋点名</summary>
    [DisplayName("操作名")]
    [Description("操作名。接口名或埋点名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Name", "操作名。接口名或埋点名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _DisplayName;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "显示名", "")]
    public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _Rules;
    /// <summary>规则。支持多个埋点操作按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开。如name=*/check*,*/ping*;clientId=10.10.*</summary>
    [DisplayName("规则")]
    [Description("规则。支持多个埋点操作按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开。如name=*/check*,*/ping*;clientId=10.10.*")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Rules", "规则。支持多个埋点操作按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开。如name=*/check*,*/ping*;clientId=10.10.*", "")]
    public String Rules { get => _Rules; set { if (OnPropertyChanging("Rules", value)) { _Rules = value; OnPropertyChanged("Rules"); } } }

    private Boolean _Cloned;
    /// <summary>克隆。根据规则匹配，把跟踪数据克隆一份，形成另一个维度的统计数据</summary>
    [DisplayName("克隆")]
    [Description("克隆。根据规则匹配，把跟踪数据克隆一份，形成另一个维度的统计数据")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Cloned", "克隆。根据规则匹配，把跟踪数据克隆一份，形成另一个维度的统计数据", "")]
    public Boolean Cloned { get => _Cloned; set { if (OnPropertyChanging("Cloned", value)) { _Cloned = value; OnPropertyChanged("Cloned"); } } }

    private Int32 _Timeout;
    /// <summary>超时时间。超过该时间时标记为异常，默认0表示不判断超时</summary>
    [DisplayName("超时时间")]
    [Description("超时时间。超过该时间时标记为异常，默认0表示不判断超时")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Timeout", "超时时间。超过该时间时标记为异常，默认0表示不判断超时", "")]
    public Int32 Timeout { get => _Timeout; set { if (OnPropertyChanging("Timeout", value)) { _Timeout = value; OnPropertyChanged("Timeout"); } } }

    private Int32 _Days;
    /// <summary>天数。共统计了多少天</summary>
    [DisplayName("天数")]
    [Description("天数。共统计了多少天")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Days", "天数。共统计了多少天", "")]
    public Int32 Days { get => _Days; set { if (OnPropertyChanging("Days", value)) { _Days = value; OnPropertyChanged("Days"); } } }

    private Int64 _Total;
    /// <summary>总次数。累计埋点采样次数</summary>
    [DisplayName("总次数")]
    [Description("总次数。累计埋点采样次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总次数。累计埋点采样次数", "")]
    public Int64 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int64 _Errors;
    /// <summary>错误数</summary>
    [DisplayName("错误数")]
    [Description("错误数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Errors", "错误数", "")]
    public Int64 Errors { get => _Errors; set { if (OnPropertyChanging("Errors", value)) { _Errors = value; OnPropertyChanged("Errors"); } } }

    private Int32 _Cost;
    /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
    [DisplayName("平均耗时")]
    [Description("平均耗时。总耗时除以总次数，单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Cost", "平均耗时。总耗时除以总次数，单位毫秒", "")]
    public Int32 Cost { get => _Cost; set { if (OnPropertyChanging("Cost", value)) { _Cost = value; OnPropertyChanged("Cost"); } } }

    private Int32 _AlarmThreshold;
    /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
    [Category("告警")]
    [DisplayName("告警阈值")]
    [Description("告警阈值。错误数达到该值时触发告警，0表示不启用，阈值和率值必须同时满足")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmThreshold", "告警阈值。错误数达到该值时触发告警，0表示不启用，阈值和率值必须同时满足", "")]
    public Int32 AlarmThreshold { get => _AlarmThreshold; set { if (OnPropertyChanging("AlarmThreshold", value)) { _AlarmThreshold = value; OnPropertyChanged("AlarmThreshold"); } } }

    private Double _AlarmErrorRate;
    /// <summary>告警错误率。错误率达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
    [Category("告警")]
    [DisplayName("告警错误率")]
    [Description("告警错误率。错误率达到该值时触发告警，0表示不启用，阈值和率值必须同时满足")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmErrorRate", "告警错误率。错误率达到该值时触发告警，0表示不启用，阈值和率值必须同时满足", "")]
    public Double AlarmErrorRate { get => _AlarmErrorRate; set { if (OnPropertyChanging("AlarmErrorRate", value)) { _AlarmErrorRate = value; OnPropertyChanged("AlarmErrorRate"); } } }

    private Double _MaxRingRate;
    /// <summary>最大环比。环比昨日超过该率时触发告警，一般大于1，如1.2表示超20%，0表示不启用</summary>
    [Category("告警")]
    [DisplayName("最大环比")]
    [Description("最大环比。环比昨日超过该率时触发告警，一般大于1，如1.2表示超20%，0表示不启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxRingRate", "最大环比。环比昨日超过该率时触发告警，一般大于1，如1.2表示超20%，0表示不启用", "")]
    public Double MaxRingRate { get => _MaxRingRate; set { if (OnPropertyChanging("MaxRingRate", value)) { _MaxRingRate = value; OnPropertyChanged("MaxRingRate"); } } }

    private Double _MinRingRate;
    /// <summary>最小环比。环比昨日小于该率时触发告警，一般小于1，如0.7表示低30%，0表示不启用</summary>
    [Category("告警")]
    [DisplayName("最小环比")]
    [Description("最小环比。环比昨日小于该率时触发告警，一般小于1，如0.7表示低30%，0表示不启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MinRingRate", "最小环比。环比昨日小于该率时触发告警，一般小于1，如0.7表示低30%，0表示不启用", "")]
    public Double MinRingRate { get => _MinRingRate; set { if (OnPropertyChanging("MinRingRate", value)) { _MinRingRate = value; OnPropertyChanged("MinRingRate"); } } }

    private String _AlarmGroup;
    /// <summary>告警组。使用告警组中指定的机器人</summary>
    [Category("告警")]
    [DisplayName("告警组")]
    [Description("告警组。使用告警组中指定的机器人")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("AlarmGroup", "告警组。使用告警组中指定的机器人", "")]
    public String AlarmGroup { get => _AlarmGroup; set { if (OnPropertyChanging("AlarmGroup", value)) { _AlarmGroup = value; OnPropertyChanged("AlarmGroup"); } } }

    private String _AlarmRobot;
    /// <summary>告警机器人。钉钉、企业微信等</summary>
    [Category("告警")]
    [DisplayName("告警机器人")]
    [Description("告警机器人。钉钉、企业微信等")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AlarmRobot", "告警机器人。钉钉、企业微信等", "")]
    public String AlarmRobot { get => _AlarmRobot; set { if (OnPropertyChanging("AlarmRobot", value)) { _AlarmRobot = value; OnPropertyChanged("AlarmRobot"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新人</summary>
    [Category("扩展")]
    [DisplayName("更新人")]
    [Description("更新人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新人", "")]
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
            "AppId" => _AppId,
            "Kind" => _Kind,
            "Name" => _Name,
            "DisplayName" => _DisplayName,
            "Enable" => _Enable,
            "Rules" => _Rules,
            "Cloned" => _Cloned,
            "Timeout" => _Timeout,
            "Days" => _Days,
            "Total" => _Total,
            "Errors" => _Errors,
            "Cost" => _Cost,
            "AlarmThreshold" => _AlarmThreshold,
            "AlarmErrorRate" => _AlarmErrorRate,
            "MaxRingRate" => _MaxRingRate,
            "MinRingRate" => _MinRingRate,
            "AlarmGroup" => _AlarmGroup,
            "AlarmRobot" => _AlarmRobot,
            "TraceId" => _TraceId,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
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
                case "AppId": _AppId = value.ToInt(); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Rules": _Rules = Convert.ToString(value); break;
                case "Cloned": _Cloned = value.ToBoolean(); break;
                case "Timeout": _Timeout = value.ToInt(); break;
                case "Days": _Days = value.ToInt(); break;
                case "Total": _Total = value.ToLong(); break;
                case "Errors": _Errors = value.ToLong(); break;
                case "Cost": _Cost = value.ToInt(); break;
                case "AlarmThreshold": _AlarmThreshold = value.ToInt(); break;
                case "AlarmErrorRate": _AlarmErrorRate = value.ToDouble(); break;
                case "MaxRingRate": _MaxRingRate = value.ToDouble(); break;
                case "MinRingRate": _MinRingRate = value.ToDouble(); break;
                case "AlarmGroup": _AlarmGroup = Convert.ToString(value); break;
                case "AlarmRobot": _AlarmRobot = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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
    #endregion

    #region 扩展查询
    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<TraceItem> FindAllByAppId(Int32 appId)
    {
        if (appId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据种类、应用查找</summary>
    /// <param name="kind">种类</param>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<TraceItem> FindAllByKindAndAppId(String kind, Int32 appId)
    {
        if (kind.IsNullOrEmpty()) return [];
        if (appId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Kind.EqualIgnoreCase(kind) && e.AppId == appId);

        return FindAll(_.Kind == kind & _.AppId == appId);
    }
    #endregion

    #region 字段名
    /// <summary>取得跟踪项字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>种类</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>操作名。接口名或埋点名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>显示名</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>规则。支持多个埋点操作按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开。如name=*/check*,*/ping*;clientId=10.10.*</summary>
        public static readonly Field Rules = FindByName("Rules");

        /// <summary>克隆。根据规则匹配，把跟踪数据克隆一份，形成另一个维度的统计数据</summary>
        public static readonly Field Cloned = FindByName("Cloned");

        /// <summary>超时时间。超过该时间时标记为异常，默认0表示不判断超时</summary>
        public static readonly Field Timeout = FindByName("Timeout");

        /// <summary>天数。共统计了多少天</summary>
        public static readonly Field Days = FindByName("Days");

        /// <summary>总次数。累计埋点采样次数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>错误数</summary>
        public static readonly Field Errors = FindByName("Errors");

        /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
        public static readonly Field Cost = FindByName("Cost");

        /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
        public static readonly Field AlarmThreshold = FindByName("AlarmThreshold");

        /// <summary>告警错误率。错误率达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
        public static readonly Field AlarmErrorRate = FindByName("AlarmErrorRate");

        /// <summary>最大环比。环比昨日超过该率时触发告警，一般大于1，如1.2表示超20%，0表示不启用</summary>
        public static readonly Field MaxRingRate = FindByName("MaxRingRate");

        /// <summary>最小环比。环比昨日小于该率时触发告警，一般小于1，如0.7表示低30%，0表示不启用</summary>
        public static readonly Field MinRingRate = FindByName("MinRingRate");

        /// <summary>告警组。使用告警组中指定的机器人</summary>
        public static readonly Field AlarmGroup = FindByName("AlarmGroup");

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public static readonly Field AlarmRobot = FindByName("AlarmRobot");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新人</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得跟踪项字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>种类</summary>
        public const String Kind = "Kind";

        /// <summary>操作名。接口名或埋点名</summary>
        public const String Name = "Name";

        /// <summary>显示名</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>规则。支持多个埋点操作按照规则聚合成为一个跟踪项，用于处理多变的操作名，支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开。如name=*/check*,*/ping*;clientId=10.10.*</summary>
        public const String Rules = "Rules";

        /// <summary>克隆。根据规则匹配，把跟踪数据克隆一份，形成另一个维度的统计数据</summary>
        public const String Cloned = "Cloned";

        /// <summary>超时时间。超过该时间时标记为异常，默认0表示不判断超时</summary>
        public const String Timeout = "Timeout";

        /// <summary>天数。共统计了多少天</summary>
        public const String Days = "Days";

        /// <summary>总次数。累计埋点采样次数</summary>
        public const String Total = "Total";

        /// <summary>错误数</summary>
        public const String Errors = "Errors";

        /// <summary>平均耗时。总耗时除以总次数，单位毫秒</summary>
        public const String Cost = "Cost";

        /// <summary>告警阈值。错误数达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
        public const String AlarmThreshold = "AlarmThreshold";

        /// <summary>告警错误率。错误率达到该值时触发告警，0表示不启用，阈值和率值必须同时满足</summary>
        public const String AlarmErrorRate = "AlarmErrorRate";

        /// <summary>最大环比。环比昨日超过该率时触发告警，一般大于1，如1.2表示超20%，0表示不启用</summary>
        public const String MaxRingRate = "MaxRingRate";

        /// <summary>最小环比。环比昨日小于该率时触发告警，一般小于1，如0.7表示低30%，0表示不启用</summary>
        public const String MinRingRate = "MinRingRate";

        /// <summary>告警组。使用告警组中指定的机器人</summary>
        public const String AlarmGroup = "AlarmGroup";

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public const String AlarmRobot = "AlarmRobot";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新人</summary>
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
