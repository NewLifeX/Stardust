using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪数据。应用定时上报采样得到的埋点跟踪原始数据</summary>
    [Serializable]
    [DataObject]
    [Description("跟踪数据。应用定时上报采样得到的埋点跟踪原始数据")]
    [BindIndex("IX_TraceData_AppId_Name", false, "AppId,Name")]
    [BindIndex("IX_TraceData_CreateTime_AppId_Name", false, "CreateTime,AppId,Name")]
    [BindTable("TraceData", Description = "跟踪数据。应用定时上报采样得到的埋点跟踪原始数据", ConnName = "Monitor", DbType = DatabaseType.None)]
    public partial class TraceData : ITraceData
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

        private Int32 _AppId;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private String _ClientId;
        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        [DisplayName("实例")]
        [Description("实例。应用可能多实例部署，ip@proccessid")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ClientId", "实例。应用可能多实例部署，ip@proccessid", "")]
        public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

        private String _Name;
        /// <summary>操作名。接口名或埋点名</summary>
        [DisplayName("操作名")]
        [Description("操作名。接口名或埋点名")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Name", "操作名。接口名或埋点名", "", Master = true)]
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

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
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
            get
            {
                switch (name)
                {
                    case "ID": return _ID;
                    case "AppId": return _AppId;
                    case "ClientId": return _ClientId;
                    case "Name": return _Name;
                    case "StartTime": return _StartTime;
                    case "EndTime": return _EndTime;
                    case "Total": return _Total;
                    case "Errors": return _Errors;
                    case "TotalCost": return _TotalCost;
                    case "Cost": return _Cost;
                    case "MaxCost": return _MaxCost;
                    case "MinCost": return _MinCost;
                    case "Samples": return _Samples;
                    case "ErrorSamples": return _ErrorSamples;
                    case "CreateIP": return _CreateIP;
                    case "CreateTime": return _CreateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToInt(); break;
                    case "AppId": _AppId = value.ToInt(); break;
                    case "ClientId": _ClientId = Convert.ToString(value); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "StartTime": _StartTime = value.ToLong(); break;
                    case "EndTime": _EndTime = value.ToLong(); break;
                    case "Total": _Total = value.ToInt(); break;
                    case "Errors": _Errors = value.ToInt(); break;
                    case "TotalCost": _TotalCost = value.ToLong(); break;
                    case "Cost": _Cost = value.ToInt(); break;
                    case "MaxCost": _MaxCost = value.ToInt(); break;
                    case "MinCost": _MinCost = value.ToInt(); break;
                    case "Samples": _Samples = value.ToInt(); break;
                    case "ErrorSamples": _ErrorSamples = value.ToInt(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得跟踪数据字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
            public static readonly Field ClientId = FindByName("ClientId");

            /// <summary>操作名。接口名或埋点名</summary>
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

            /// <summary>正常采样</summary>
            public static readonly Field Samples = FindByName("Samples");

            /// <summary>异常采样</summary>
            public static readonly Field ErrorSamples = FindByName("ErrorSamples");

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
            public const String ID = "ID";

            /// <summary>应用</summary>
            public const String AppId = "AppId";

            /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
            public const String ClientId = "ClientId";

            /// <summary>操作名。接口名或埋点名</summary>
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

            /// <summary>正常采样</summary>
            public const String Samples = "Samples";

            /// <summary>异常采样</summary>
            public const String ErrorSamples = "ErrorSamples";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";
        }
        #endregion
    }

    /// <summary>跟踪数据。应用定时上报采样得到的埋点跟踪原始数据接口</summary>
    public partial interface ITraceData
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>应用</summary>
        Int32 AppId { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        String ClientId { get; set; }

        /// <summary>操作名。接口名或埋点名</summary>
        String Name { get; set; }

        /// <summary>开始时间。Unix毫秒</summary>
        Int64 StartTime { get; set; }

        /// <summary>结束时间。Unix毫秒</summary>
        Int64 EndTime { get; set; }

        /// <summary>总次数</summary>
        Int32 Total { get; set; }

        /// <summary>错误数</summary>
        Int32 Errors { get; set; }

        /// <summary>总耗时。单位毫秒</summary>
        Int64 TotalCost { get; set; }

        /// <summary>平均耗时。总耗时除以总次数</summary>
        Int32 Cost { get; set; }

        /// <summary>最大耗时。单位毫秒</summary>
        Int32 MaxCost { get; set; }

        /// <summary>最小耗时。单位毫秒</summary>
        Int32 MinCost { get; set; }

        /// <summary>正常采样</summary>
        Int32 Samples { get; set; }

        /// <summary>异常采样</summary>
        Int32 ErrorSamples { get; set; }

        /// <summary>创建地址</summary>
        String CreateIP { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}