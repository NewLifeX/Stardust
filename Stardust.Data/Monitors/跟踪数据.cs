using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Monitors;

/// <summary>跟踪数据。应用定时上报采样得到的埋点追踪原始数据，应用端已完成初步统计，后端将再次向上汇总</summary>
[Serializable]
[DataObject]
[Description("跟踪数据。应用定时上报采样得到的埋点追踪原始数据，应用端已完成初步统计，后端将再次向上汇总")]
[BindIndex("IX_TraceData_StatDate_AppId_ItemId_StartTime", false, "StatDate,AppId,ItemId,StartTime")]
[BindIndex("IX_TraceData_StatHour_AppId_ItemId", false, "StatHour,AppId,ItemId")]
[BindIndex("IX_TraceData_StatMinute_AppId_ItemId", false, "StatMinute,AppId,ItemId")]
[BindIndex("IX_TraceData_AppId_StatMinute", false, "AppId,StatMinute")]
[BindIndex("IX_TraceData_AppId_ClientId", false, "AppId,ClientId")]
[BindTable("TraceData", Description = "跟踪数据。应用定时上报采样得到的埋点追踪原始数据，应用端已完成初步统计，后端将再次向上汇总", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class TraceData
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "timeShard:dd")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private DateTime _StatDate;
    /// <summary>统计日期</summary>
    [DisplayName("统计日期")]
    [Description("统计日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatDate", "统计日期", "")]
    public DateTime StatDate { get => _StatDate; set { if (OnPropertyChanging("StatDate", value)) { _StatDate = value; OnPropertyChanged("StatDate"); } } }

    private DateTime _StatHour;
    /// <summary>统计小时</summary>
    [DisplayName("统计小时")]
    [Description("统计小时")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatHour", "统计小时", "")]
    public DateTime StatHour { get => _StatHour; set { if (OnPropertyChanging("StatHour", value)) { _StatHour = value; OnPropertyChanged("StatHour"); } } }

    private DateTime _StatMinute;
    /// <summary>统计分钟</summary>
    [DisplayName("统计分钟")]
    [Description("统计分钟")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatMinute", "统计分钟", "")]
    public DateTime StatMinute { get => _StatMinute; set { if (OnPropertyChanging("StatMinute", value)) { _StatMinute = value; OnPropertyChanged("StatMinute"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int32 _NodeId;
    /// <summary>节点</summary>
    [DisplayName("节点")]
    [Description("节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private String _ClientId;
    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    [DisplayName("实例")]
    [Description("实例。应用可能多实例部署，ip@proccessid")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ClientId", "实例。应用可能多实例部署，ip@proccessid", "")]
    public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private Int32 _ItemId;
    /// <summary>跟踪项</summary>
    [DisplayName("跟踪项")]
    [Description("跟踪项")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ItemId", "跟踪项", "")]
    public Int32 ItemId { get => _ItemId; set { if (OnPropertyChanging("ItemId", value)) { _ItemId = value; OnPropertyChanged("ItemId"); } } }

    private String _Name;
    /// <summary>操作名。原始接口名或埋点名</summary>
    [DisplayName("操作名")]
    [Description("操作名。原始接口名或埋点名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Name", "操作名。原始接口名或埋点名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int64 _StartTime;
    /// <summary>开始时间。Unix毫秒</summary>
    [DisplayName("开始时间")]
    [Description("开始时间。Unix毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StartTime", "开始时间。Unix毫秒", "")]
    public Int64 StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private Int64 _EndTime;
    /// <summary>结束时间。Unix毫秒</summary>
    [DisplayName("结束时间")]
    [Description("结束时间。Unix毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EndTime", "结束时间。Unix毫秒", "")]
    public Int64 EndTime { get => _EndTime; set { if (OnPropertyChanging("EndTime", value)) { _EndTime = value; OnPropertyChanged("EndTime"); } } }

    private Int32 _Total;
    /// <summary>总次数</summary>
    [DisplayName("总次数")]
    [Description("总次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总次数", "")]
    public Int32 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int32 _Errors;
    /// <summary>错误数</summary>
    [DisplayName("错误数")]
    [Description("错误数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Errors", "错误数", "")]
    public Int32 Errors { get => _Errors; set { if (OnPropertyChanging("Errors", value)) { _Errors = value; OnPropertyChanged("Errors"); } } }

    private Int64 _TotalCost;
    /// <summary>总耗时。单位毫秒</summary>
    [DisplayName("总耗时")]
    [Description("总耗时。单位毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总耗时。单位毫秒", "")]
    public Int64 TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int32 _Cost;
    /// <summary>平均耗时。总耗时除以总次数</summary>
    [DisplayName("平均耗时")]
    [Description("平均耗时。总耗时除以总次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Cost", "平均耗时。总耗时除以总次数", "")]
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

    private Int32 _Samples;
    /// <summary>正常采样</summary>
    [DisplayName("正常采样")]
    [Description("正常采样")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Samples", "正常采样", "")]
    public Int32 Samples { get => _Samples; set { if (OnPropertyChanging("Samples", value)) { _Samples = value; OnPropertyChanged("Samples"); } } }

    private Int32 _ErrorSamples;
    /// <summary>异常采样</summary>
    [DisplayName("异常采样")]
    [Description("异常采样")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ErrorSamples", "异常采样", "")]
    public Int32 ErrorSamples { get => _ErrorSamples; set { if (OnPropertyChanging("ErrorSamples", value)) { _ErrorSamples = value; OnPropertyChanged("ErrorSamples"); } } }

    private Int64 _LinkId;
    /// <summary>关联项。当前跟踪数据为克隆数据时，采用数据落在关联项所指定的跟踪数据之下</summary>
    [DisplayName("关联项")]
    [Description("关联项。当前跟踪数据为克隆数据时，采用数据落在关联项所指定的跟踪数据之下")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LinkId", "关联项。当前跟踪数据为克隆数据时，采用数据落在关联项所指定的跟踪数据之下", "")]
    public Int64 LinkId { get => _LinkId; set { if (OnPropertyChanging("LinkId", value)) { _LinkId = value; OnPropertyChanged("LinkId"); } } }

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
            "StatDate" => _StatDate,
            "StatHour" => _StatHour,
            "StatMinute" => _StatMinute,
            "AppId" => _AppId,
            "NodeId" => _NodeId,
            "ClientId" => _ClientId,
            "ItemId" => _ItemId,
            "Name" => _Name,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "Total" => _Total,
            "Errors" => _Errors,
            "TotalCost" => _TotalCost,
            "Cost" => _Cost,
            "MaxCost" => _MaxCost,
            "MinCost" => _MinCost,
            "TotalValue" => _TotalValue,
            "Samples" => _Samples,
            "ErrorSamples" => _ErrorSamples,
            "LinkId" => _LinkId,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "StatDate": _StatDate = value.ToDateTime(); break;
                case "StatHour": _StatHour = value.ToDateTime(); break;
                case "StatMinute": _StatMinute = value.ToDateTime(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "ClientId": _ClientId = Convert.ToString(value); break;
                case "ItemId": _ItemId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "StartTime": _StartTime = value.ToLong(); break;
                case "EndTime": _EndTime = value.ToLong(); break;
                case "Total": _Total = value.ToInt(); break;
                case "Errors": _Errors = value.ToInt(); break;
                case "TotalCost": _TotalCost = value.ToLong(); break;
                case "Cost": _Cost = value.ToInt(); break;
                case "MaxCost": _MaxCost = value.ToInt(); break;
                case "MinCost": _MinCost = value.ToInt(); break;
                case "TotalValue": _TotalValue = value.ToLong(); break;
                case "Samples": _Samples = value.ToInt(); break;
                case "ErrorSamples": _ErrorSamples = value.ToInt(); break;
                case "LinkId": _LinkId = value.ToLong(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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
    public static IList<TraceData> FindAllByAppId(Int32 appId)
    {
        if (appId < 0) return [];

        return FindAll(_.AppId == appId);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow));
    }

    /// <summary>删除指定时间段内的数据表</summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DropWith(DateTime start, DateTime end)
    {
        return Meta.AutoShard(start, end, session =>
        {
            try
            {
                return session.Execute($"Drop Table {session.FormatedTableName}");
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                return 0;
            }
        }
        ).Sum();
    }
    #endregion

    #region 字段名
    /// <summary>取得跟踪数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>统计日期</summary>
        public static readonly Field StatDate = FindByName("StatDate");

        /// <summary>统计小时</summary>
        public static readonly Field StatHour = FindByName("StatHour");

        /// <summary>统计分钟</summary>
        public static readonly Field StatMinute = FindByName("StatMinute");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>节点</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public static readonly Field ClientId = FindByName("ClientId");

        /// <summary>跟踪项</summary>
        public static readonly Field ItemId = FindByName("ItemId");

        /// <summary>操作名。原始接口名或埋点名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>开始时间。Unix毫秒</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间。Unix毫秒</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>总次数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>错误数</summary>
        public static readonly Field Errors = FindByName("Errors");

        /// <summary>总耗时。单位毫秒</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>平均耗时。总耗时除以总次数</summary>
        public static readonly Field Cost = FindByName("Cost");

        /// <summary>最大耗时。单位毫秒</summary>
        public static readonly Field MaxCost = FindByName("MaxCost");

        /// <summary>最小耗时。单位毫秒</summary>
        public static readonly Field MinCost = FindByName("MinCost");

        /// <summary>总数值。用户自定义标量</summary>
        public static readonly Field TotalValue = FindByName("TotalValue");

        /// <summary>正常采样</summary>
        public static readonly Field Samples = FindByName("Samples");

        /// <summary>异常采样</summary>
        public static readonly Field ErrorSamples = FindByName("ErrorSamples");

        /// <summary>关联项。当前跟踪数据为克隆数据时，采用数据落在关联项所指定的跟踪数据之下</summary>
        public static readonly Field LinkId = FindByName("LinkId");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得跟踪数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>统计日期</summary>
        public const String StatDate = "StatDate";

        /// <summary>统计小时</summary>
        public const String StatHour = "StatHour";

        /// <summary>统计分钟</summary>
        public const String StatMinute = "StatMinute";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>节点</summary>
        public const String NodeId = "NodeId";

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public const String ClientId = "ClientId";

        /// <summary>跟踪项</summary>
        public const String ItemId = "ItemId";

        /// <summary>操作名。原始接口名或埋点名</summary>
        public const String Name = "Name";

        /// <summary>开始时间。Unix毫秒</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间。Unix毫秒</summary>
        public const String EndTime = "EndTime";

        /// <summary>总次数</summary>
        public const String Total = "Total";

        /// <summary>错误数</summary>
        public const String Errors = "Errors";

        /// <summary>总耗时。单位毫秒</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>平均耗时。总耗时除以总次数</summary>
        public const String Cost = "Cost";

        /// <summary>最大耗时。单位毫秒</summary>
        public const String MaxCost = "MaxCost";

        /// <summary>最小耗时。单位毫秒</summary>
        public const String MinCost = "MinCost";

        /// <summary>总数值。用户自定义标量</summary>
        public const String TotalValue = "TotalValue";

        /// <summary>正常采样</summary>
        public const String Samples = "Samples";

        /// <summary>异常采样</summary>
        public const String ErrorSamples = "ErrorSamples";

        /// <summary>关联项。当前跟踪数据为克隆数据时，采用数据落在关联项所指定的跟踪数据之下</summary>
        public const String LinkId = "LinkId";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
