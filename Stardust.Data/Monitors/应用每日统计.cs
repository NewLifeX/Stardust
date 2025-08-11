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

/// <summary>应用每日统计。每应用每日统计，用于分析应用健康状况</summary>
[Serializable]
[DataObject]
[Description("应用每日统计。每应用每日统计，用于分析应用健康状况")]
[BindIndex("IU_AppDayStat_StatDate_AppId", true, "StatDate,AppId")]
[BindIndex("IX_AppDayStat_AppId_Id", false, "AppId,Id")]
[BindTable("AppDayStat", Description = "应用每日统计。每应用每日统计，用于分析应用健康状况", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppDayStat
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

    private Int32 _Names;
    /// <summary>埋点数</summary>
    [DisplayName("埋点数")]
    [Description("埋点数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Names", "埋点数", "")]
    public Int32 Names { get => _Names; set { if (OnPropertyChanging("Names", value)) { _Names = value; OnPropertyChanged("Names"); } } }

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

    private Int64 _Apis;
    /// <summary>接口数</summary>
    [DisplayName("接口数")]
    [Description("接口数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Apis", "接口数", "")]
    public Int64 Apis { get => _Apis; set { if (OnPropertyChanging("Apis", value)) { _Apis = value; OnPropertyChanged("Apis"); } } }

    private Int64 _Https;
    /// <summary>Http请求</summary>
    [DisplayName("Http请求")]
    [Description("Http请求")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Https", "Http请求", "")]
    public Int64 Https { get => _Https; set { if (OnPropertyChanging("Https", value)) { _Https = value; OnPropertyChanged("Https"); } } }

    private Int64 _Dbs;
    /// <summary>数据库</summary>
    [DisplayName("数据库")]
    [Description("数据库")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Dbs", "数据库", "")]
    public Int64 Dbs { get => _Dbs; set { if (OnPropertyChanging("Dbs", value)) { _Dbs = value; OnPropertyChanged("Dbs"); } } }

    private Int64 _Mqs;
    /// <summary>消息队列</summary>
    [DisplayName("消息队列")]
    [Description("消息队列")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Mqs", "消息队列", "")]
    public Int64 Mqs { get => _Mqs; set { if (OnPropertyChanging("Mqs", value)) { _Mqs = value; OnPropertyChanged("Mqs"); } } }

    private Int64 _Redis;
    /// <summary>Redis</summary>
    [DisplayName("Redis")]
    [Description("Redis")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Redis", "Redis", "")]
    public Int64 Redis { get => _Redis; set { if (OnPropertyChanging("Redis", value)) { _Redis = value; OnPropertyChanged("Redis"); } } }

    private Int64 _Others;
    /// <summary>其它</summary>
    [DisplayName("其它")]
    [Description("其它")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Others", "其它", "")]
    public Int64 Others { get => _Others; set { if (OnPropertyChanging("Others", value)) { _Others = value; OnPropertyChanged("Others"); } } }

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
            "Names" => _Names,
            "Total" => _Total,
            "RingRate" => _RingRate,
            "Errors" => _Errors,
            "ErrorRate" => _ErrorRate,
            "TotalCost" => _TotalCost,
            "Cost" => _Cost,
            "MaxCost" => _MaxCost,
            "MinCost" => _MinCost,
            "Apis" => _Apis,
            "Https" => _Https,
            "Dbs" => _Dbs,
            "Mqs" => _Mqs,
            "Redis" => _Redis,
            "Others" => _Others,
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
                case "Names": _Names = value.ToInt(); break;
                case "Total": _Total = value.ToLong(); break;
                case "RingRate": _RingRate = value.ToDouble(); break;
                case "Errors": _Errors = value.ToLong(); break;
                case "ErrorRate": _ErrorRate = value.ToDouble(); break;
                case "TotalCost": _TotalCost = value.ToLong(); break;
                case "Cost": _Cost = value.ToInt(); break;
                case "MaxCost": _MaxCost = value.ToInt(); break;
                case "MinCost": _MinCost = value.ToInt(); break;
                case "Apis": _Apis = value.ToLong(); break;
                case "Https": _Https = value.ToLong(); break;
                case "Dbs": _Dbs = value.ToLong(); break;
                case "Mqs": _Mqs = value.ToLong(); break;
                case "Redis": _Redis = value.ToLong(); break;
                case "Others": _Others = value.ToLong(); break;
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
    /// <summary>根据统计日期查找</summary>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<AppDayStat> FindAllByStatDate(DateTime statDate)
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
    /// <summary>取得应用每日统计字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>统计日期</summary>
        public static readonly Field StatDate = FindByName("StatDate");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>埋点数</summary>
        public static readonly Field Names = FindByName("Names");

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

        /// <summary>接口数</summary>
        public static readonly Field Apis = FindByName("Apis");

        /// <summary>Http请求</summary>
        public static readonly Field Https = FindByName("Https");

        /// <summary>数据库</summary>
        public static readonly Field Dbs = FindByName("Dbs");

        /// <summary>消息队列</summary>
        public static readonly Field Mqs = FindByName("Mqs");

        /// <summary>Redis</summary>
        public static readonly Field Redis = FindByName("Redis");

        /// <summary>其它</summary>
        public static readonly Field Others = FindByName("Others");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用每日统计字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>统计日期</summary>
        public const String StatDate = "StatDate";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>埋点数</summary>
        public const String Names = "Names";

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

        /// <summary>接口数</summary>
        public const String Apis = "Apis";

        /// <summary>Http请求</summary>
        public const String Https = "Https";

        /// <summary>数据库</summary>
        public const String Dbs = "Dbs";

        /// <summary>消息队列</summary>
        public const String Mqs = "Mqs";

        /// <summary>Redis</summary>
        public const String Redis = "Redis";

        /// <summary>其它</summary>
        public const String Others = "Others";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
