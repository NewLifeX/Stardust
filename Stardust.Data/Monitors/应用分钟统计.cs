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

/// <summary>应用分钟统计。每应用每5分钟统计，用于分析应用健康状况</summary>
[Serializable]
[DataObject]
[Description("应用分钟统计。每应用每5分钟统计，用于分析应用健康状况")]
[BindIndex("IU_AppMinuteStat_StatTime_AppId", true, "StatTime,AppId")]
[BindIndex("IX_AppMinuteStat_AppId_Id", false, "AppId,Id")]
[BindTable("AppMinuteStat", Description = "应用分钟统计。每应用每5分钟统计，用于分析应用健康状况", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class AppMinuteStat
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private DateTime _StatTime;
    /// <summary>统计分钟</summary>
    [DisplayName("统计分钟")]
    [Description("统计分钟")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatTime", "统计分钟", "", DataScale = "time:yyyy-MM-dd HH:mm")]
    public DateTime StatTime { get => _StatTime; set { if (OnPropertyChanging("StatTime", value)) { _StatTime = value; OnPropertyChanged("StatTime"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int64 _Total;
    /// <summary>总次数</summary>
    [DisplayName("总次数")]
    [Description("总次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总次数", "")]
    public Int64 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

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
            "StatTime" => _StatTime,
            "AppId" => _AppId,
            "Total" => _Total,
            "Errors" => _Errors,
            "ErrorRate" => _ErrorRate,
            "TotalCost" => _TotalCost,
            "Cost" => _Cost,
            "MaxCost" => _MaxCost,
            "MinCost" => _MinCost,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "StatTime": _StatTime = value.ToDateTime(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "Total": _Total = value.ToLong(); break;
                case "Errors": _Errors = value.ToLong(); break;
                case "ErrorRate": _ErrorRate = value.ToDouble(); break;
                case "TotalCost": _TotalCost = value.ToLong(); break;
                case "Cost": _Cost = value.ToInt(); break;
                case "MaxCost": _MaxCost = value.ToInt(); break;
                case "MinCost": _MinCost = value.ToInt(); break;
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
    /// <summary>根据统计分钟查找</summary>
    /// <param name="statTime">统计分钟</param>
    /// <returns>实体列表</returns>
    public static IList<AppMinuteStat> FindAllByStatTime(DateTime statTime)
    {
        if (statTime.Year < 1000) return [];

        return FindAll(_.StatTime == statTime);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        if (start == end) return Delete(_.StatTime == start);

        return Delete(_.StatTime.Between(start, end));
    }
    #endregion

    #region 字段名
    /// <summary>取得应用分钟统计字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>统计分钟</summary>
        public static readonly Field StatTime = FindByName("StatTime");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>总次数</summary>
        public static readonly Field Total = FindByName("Total");

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

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用分钟统计字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>统计分钟</summary>
        public const String StatTime = "StatTime";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>总次数</summary>
        public const String Total = "Total";

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

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
