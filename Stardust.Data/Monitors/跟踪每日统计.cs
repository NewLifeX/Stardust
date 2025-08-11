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

/// <summary>跟踪每日统计。每应用每接口每日统计，用于分析接口健康状况</summary>
[Serializable]
[DataObject]
[Description("跟踪每日统计。每应用每接口每日统计，用于分析接口健康状况")]
[BindIndex("IX_TraceDayStat_StatDate_AppId_Type", false, "StatDate,AppId,Type")]
[BindIndex("IX_TraceDayStat_StatDate_AppId_ItemId", false, "StatDate,AppId,ItemId")]
[BindIndex("IX_TraceDayStat_AppId_ItemId_Id", false, "AppId,ItemId,Id")]
[BindIndex("IX_TraceDayStat_AppId_Type_StatDate", false, "AppId,Type,StatDate")]
[BindIndex("IX_TraceDayStat_AppId_StatDate", false, "AppId,StatDate")]
[BindIndex("IX_TraceDayStat_AppId_ItemId_StatDate", false, "AppId,ItemId,StatDate")]
[BindTable("TraceDayStat", Description = "跟踪每日统计。每应用每接口每日统计，用于分析接口健康状况", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class TraceDayStat
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private DateTime _StatDate;
    /// <summary>统计日期</summary>
    [DisplayName("统计日期")]
    [Description("统计日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatDate", "统计日期", "", DataScale = "time:yyyy-MM-dd")]
    public DateTime StatDate { get => _StatDate; set { if (OnPropertyChanging("StatDate", value)) { _StatDate = value; OnPropertyChanged("StatDate"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int32 _ItemId;
    /// <summary>跟踪项</summary>
    [DisplayName("跟踪项")]
    [Description("跟踪项")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ItemId", "跟踪项", "")]
    public Int32 ItemId { get => _ItemId; set { if (OnPropertyChanging("ItemId", value)) { _ItemId = value; OnPropertyChanged("ItemId"); } } }

    private String _Name;
    /// <summary>操作名。接口名或埋点名</summary>
    [DisplayName("操作名")]
    [Description("操作名。接口名或埋点名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Name", "操作名。接口名或埋点名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Type;
    /// <summary>种类。Api/Http/Db/Mq/Redis/Other</summary>
    [DisplayName("种类")]
    [Description("种类。Api/Http/Db/Mq/Redis/Other")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Type", "种类。Api/Http/Db/Mq/Redis/Other", "")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private Int64 _Total;
    /// <summary>总次数</summary>
    [DisplayName("总次数")]
    [Description("总次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总次数", "")]
    public Int64 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Double _RingRate;
    /// <summary>环比。今天与昨天相比</summary>
    [DisplayName("环比")]
    [Description("环比。今天与昨天相比")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RingRate", "环比。今天与昨天相比", "")]
    public Double RingRate { get => _RingRate; set { if (OnPropertyChanging("RingRate", value)) { _RingRate = value; OnPropertyChanged("RingRate"); } } }

    private Int64 _Errors;
    /// <summary>错误数</summary>
    [DisplayName("错误数")]
    [Description("错误数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Errors", "错误数", "")]
    public Int64 Errors { get => _Errors; set { if (OnPropertyChanging("Errors", value)) { _Errors = value; OnPropertyChanged("Errors"); } } }

    private Double _ErrorRate;
    /// <summary>错误率。错误数除以总次数</summary>
    [DisplayName("错误率")]
    [Description("错误率。错误数除以总次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ErrorRate", "错误率。错误数除以总次数", "")]
    public Double ErrorRate { get => _ErrorRate; set { if (OnPropertyChanging("ErrorRate", value)) { _ErrorRate = value; OnPropertyChanged("ErrorRate"); } } }

    private Int64 _TotalCost;
    /// <summary>总耗时。单位毫秒</summary>
    [DisplayName("总耗时")]
    [Description("总耗时。单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总耗时。单位毫秒", "")]
    public Int64 TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int32 _Cost;
    /// <summary>平均耗时。逼近TP99，总耗时去掉最大值后除以总次数，单位毫秒</summary>
    [DisplayName("平均耗时")]
    [Description("平均耗时。逼近TP99，总耗时去掉最大值后除以总次数，单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Cost", "平均耗时。逼近TP99，总耗时去掉最大值后除以总次数，单位毫秒", "")]
    public Int32 Cost { get => _Cost; set { if (OnPropertyChanging("Cost", value)) { _Cost = value; OnPropertyChanged("Cost"); } } }

    private Int32 _MaxCost;
    /// <summary>最大耗时。单位毫秒</summary>
    [DisplayName("最大耗时")]
    [Description("最大耗时。单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxCost", "最大耗时。单位毫秒", "")]
    public Int32 MaxCost { get => _MaxCost; set { if (OnPropertyChanging("MaxCost", value)) { _MaxCost = value; OnPropertyChanged("MaxCost"); } } }

    private Int32 _MinCost;
    /// <summary>最小耗时。单位毫秒</summary>
    [DisplayName("最小耗时")]
    [Description("最小耗时。单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MinCost", "最小耗时。单位毫秒", "")]
    public Int32 MinCost { get => _MinCost; set { if (OnPropertyChanging("MinCost", value)) { _MinCost = value; OnPropertyChanged("MinCost"); } } }

    private Int64 _TotalValue;
    /// <summary>总数值。用户自定义标量</summary>
    [DisplayName("总数值")]
    [Description("总数值。用户自定义标量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalValue", "总数值。用户自定义标量", "")]
    public Int64 TotalValue { get => _TotalValue; set { if (OnPropertyChanging("TotalValue", value)) { _TotalValue = value; OnPropertyChanged("TotalValue"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

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
            "ID" => _ID,
            "StatDate" => _StatDate,
            "AppId" => _AppId,
            "ItemId" => _ItemId,
            "Name" => _Name,
            "Type" => _Type,
            "Total" => _Total,
            "RingRate" => _RingRate,
            "Errors" => _Errors,
            "ErrorRate" => _ErrorRate,
            "TotalCost" => _TotalCost,
            "Cost" => _Cost,
            "MaxCost" => _MaxCost,
            "MinCost" => _MinCost,
            "TotalValue" => _TotalValue,
            "TraceId" => _TraceId,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "StatDate": _StatDate = value.ToDateTime(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "ItemId": _ItemId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "Total": _Total = value.ToLong(); break;
                case "RingRate": _RingRate = value.ToDouble(); break;
                case "Errors": _Errors = value.ToLong(); break;
                case "ErrorRate": _ErrorRate = value.ToDouble(); break;
                case "TotalCost": _TotalCost = value.ToLong(); break;
                case "Cost": _Cost = value.ToInt(); break;
                case "MaxCost": _MaxCost = value.ToInt(); break;
                case "MinCost": _MinCost = value.ToInt(); break;
                case "TotalValue": _TotalValue = value.ToLong(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据应用、统计日期查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<TraceDayStat> FindAllByAppIdAndStatDate(Int32 appId, DateTime statDate)
    {
        if (appId < 0) return [];
        if (statDate.Year < 1000) return [];

        return FindAll(_.AppId == appId & _.StatDate == statDate);
    }

    /// <summary>根据应用、跟踪项、统计日期查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="itemId">跟踪项</param>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<TraceDayStat> FindAllByAppIdAndItemIdAndStatDate(Int32 appId, Int32 itemId, DateTime statDate)
    {
        if (appId < 0) return [];
        if (itemId < 0) return [];
        if (statDate.Year < 1000) return [];

        return FindAll(_.AppId == appId & _.ItemId == itemId & _.StatDate == statDate);
    }

    /// <summary>根据统计日期查找</summary>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<TraceDayStat> FindAllByStatDate(DateTime statDate)
    {
        if (statDate.Year < 1000) return [];

        return FindAll(_.StatDate == statDate);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        if (start == end) return Delete(_.StatDate == start);

        return Delete(_.StatDate.Between(start, end));
    }
    #endregion

    #region 字段名
    /// <summary>取得跟踪每日统计字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>统计日期</summary>
        public static readonly Field StatDate = FindByName("StatDate");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>跟踪项</summary>
        public static readonly Field ItemId = FindByName("ItemId");

        /// <summary>操作名。接口名或埋点名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>种类。Api/Http/Db/Mq/Redis/Other</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>总次数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>环比。今天与昨天相比</summary>
        public static readonly Field RingRate = FindByName("RingRate");

        /// <summary>错误数</summary>
        public static readonly Field Errors = FindByName("Errors");

        /// <summary>错误率。错误数除以总次数</summary>
        public static readonly Field ErrorRate = FindByName("ErrorRate");

        /// <summary>总耗时。单位毫秒</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>平均耗时。逼近TP99，总耗时去掉最大值后除以总次数，单位毫秒</summary>
        public static readonly Field Cost = FindByName("Cost");

        /// <summary>最大耗时。单位毫秒</summary>
        public static readonly Field MaxCost = FindByName("MaxCost");

        /// <summary>最小耗时。单位毫秒</summary>
        public static readonly Field MinCost = FindByName("MinCost");

        /// <summary>总数值。用户自定义标量</summary>
        public static readonly Field TotalValue = FindByName("TotalValue");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得跟踪每日统计字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>统计日期</summary>
        public const String StatDate = "StatDate";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>跟踪项</summary>
        public const String ItemId = "ItemId";

        /// <summary>操作名。接口名或埋点名</summary>
        public const String Name = "Name";

        /// <summary>种类。Api/Http/Db/Mq/Redis/Other</summary>
        public const String Type = "Type";

        /// <summary>总次数</summary>
        public const String Total = "Total";

        /// <summary>环比。今天与昨天相比</summary>
        public const String RingRate = "RingRate";

        /// <summary>错误数</summary>
        public const String Errors = "Errors";

        /// <summary>错误率。错误数除以总次数</summary>
        public const String ErrorRate = "ErrorRate";

        /// <summary>总耗时。单位毫秒</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>平均耗时。逼近TP99，总耗时去掉最大值后除以总次数，单位毫秒</summary>
        public const String Cost = "Cost";

        /// <summary>最大耗时。单位毫秒</summary>
        public const String MaxCost = "MaxCost";

        /// <summary>最小耗时。单位毫秒</summary>
        public const String MinCost = "MinCost";

        /// <summary>总数值。用户自定义标量</summary>
        public const String TotalValue = "TotalValue";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
