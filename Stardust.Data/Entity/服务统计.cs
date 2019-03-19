using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data
{
    /// <summary>服务统计。记录服务每天访问量</summary>
    [Serializable]
    [DataObject]
    [Description("服务统计。记录服务每天访问量")]
    [BindIndex("IU_ServiceStat_ServiceID_Level_Time", true, "ServiceID,Level,Time")]
    [BindIndex("IX_ServiceStat_Level_Time", false, "Level,Time")]
    [BindTable("ServiceStat", Description = "服务统计。记录服务每天访问量", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class ServiceStat : IServiceStat
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get { return _ID; } set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private Int32 _ServiceID;
        /// <summary>服务</summary>
        [DisplayName("服务")]
        [Description("服务")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ServiceID", "服务", "")]
        public Int32 ServiceID { get { return _ServiceID; } set { if (OnPropertyChanging(__.ServiceID, value)) { _ServiceID = value; OnPropertyChanged(__.ServiceID); } } }

        private XCode.Statistics.StatLevels _Level;
        /// <summary>层级</summary>
        [DisplayName("层级")]
        [Description("层级")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Level", "层级", "")]
        public XCode.Statistics.StatLevels Level { get { return _Level; } set { if (OnPropertyChanging(__.Level, value)) { _Level = value; OnPropertyChanged(__.Level); } } }

        private DateTime _Time;
        /// <summary>日期</summary>
        [DisplayName("日期")]
        [Description("日期")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("Time", "日期", "")]
        public DateTime Time { get { return _Time; } set { if (OnPropertyChanging(__.Time, value)) { _Time = value; OnPropertyChanged(__.Time); } } }

        private Int64 _Count;
        /// <summary>次数</summary>
        [DisplayName("次数")]
        [Description("次数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Count", "次数", "")]
        public Int64 Count { get { return _Count; } set { if (OnPropertyChanging(__.Count, value)) { _Count = value; OnPropertyChanged(__.Count); } } }

        private Int32 _Cost;
        /// <summary>耗时。平均值，微秒us</summary>
        [DisplayName("耗时")]
        [Description("耗时。平均值，微秒us")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Cost", "耗时。平均值，微秒us", "")]
        public Int32 Cost { get { return _Cost; } set { if (OnPropertyChanging(__.Cost, value)) { _Cost = value; OnPropertyChanged(__.Cost); } } }

        private Int64 _TotalCost;
        /// <summary>总耗时。微秒us</summary>
        [DisplayName("总耗时")]
        [Description("总耗时。微秒us")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("TotalCost", "总耗时。微秒us", "")]
        public Int64 TotalCost { get { return _TotalCost; } set { if (OnPropertyChanging(__.TotalCost, value)) { _TotalCost = value; OnPropertyChanged(__.TotalCost); } } }

        private String _LastIP;
        /// <summary>最后IP</summary>
        [DisplayName("最后IP")]
        [Description("最后IP")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastIP", "最后IP", "")]
        public String LastIP { get { return _LastIP; } set { if (OnPropertyChanging(__.LastIP, value)) { _LastIP = value; OnPropertyChanged(__.LastIP); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get { return _CreateTime; } set { if (OnPropertyChanging(__.CreateTime, value)) { _CreateTime = value; OnPropertyChanged(__.CreateTime); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get { return _UpdateTime; } set { if (OnPropertyChanging(__.UpdateTime, value)) { _UpdateTime = value; OnPropertyChanged(__.UpdateTime); } } }
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
                    case __.ID : return _ID;
                    case __.ServiceID : return _ServiceID;
                    case __.Level : return _Level;
                    case __.Time : return _Time;
                    case __.Count : return _Count;
                    case __.Cost : return _Cost;
                    case __.TotalCost : return _TotalCost;
                    case __.LastIP : return _LastIP;
                    case __.CreateTime : return _CreateTime;
                    case __.UpdateTime : return _UpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID : _ID = value.ToInt(); break;
                    case __.ServiceID : _ServiceID = value.ToInt(); break;
                    case __.Level : _Level = (XCode.Statistics.StatLevels)value.ToInt(); break;
                    case __.Time : _Time = value.ToDateTime(); break;
                    case __.Count : _Count = value.ToLong(); break;
                    case __.Cost : _Cost = value.ToInt(); break;
                    case __.TotalCost : _TotalCost = value.ToLong(); break;
                    case __.LastIP : _LastIP = Convert.ToString(value); break;
                    case __.CreateTime : _CreateTime = value.ToDateTime(); break;
                    case __.UpdateTime : _UpdateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得服务统计字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>服务</summary>
            public static readonly Field ServiceID = FindByName(__.ServiceID);

            /// <summary>层级</summary>
            public static readonly Field Level = FindByName(__.Level);

            /// <summary>日期</summary>
            public static readonly Field Time = FindByName(__.Time);

            /// <summary>次数</summary>
            public static readonly Field Count = FindByName(__.Count);

            /// <summary>耗时。平均值，微秒us</summary>
            public static readonly Field Cost = FindByName(__.Cost);

            /// <summary>总耗时。微秒us</summary>
            public static readonly Field TotalCost = FindByName(__.TotalCost);

            /// <summary>最后IP</summary>
            public static readonly Field LastIP = FindByName(__.LastIP);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得服务统计字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>服务</summary>
            public const String ServiceID = "ServiceID";

            /// <summary>层级</summary>
            public const String Level = "Level";

            /// <summary>日期</summary>
            public const String Time = "Time";

            /// <summary>次数</summary>
            public const String Count = "Count";

            /// <summary>耗时。平均值，微秒us</summary>
            public const String Cost = "Cost";

            /// <summary>总耗时。微秒us</summary>
            public const String TotalCost = "TotalCost";

            /// <summary>最后IP</summary>
            public const String LastIP = "LastIP";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";
        }
        #endregion
    }

    /// <summary>服务统计。记录服务每天访问量接口</summary>
    public partial interface IServiceStat
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>服务</summary>
        Int32 ServiceID { get; set; }

        /// <summary>层级</summary>
        XCode.Statistics.StatLevels Level { get; set; }

        /// <summary>日期</summary>
        DateTime Time { get; set; }

        /// <summary>次数</summary>
        Int64 Count { get; set; }

        /// <summary>耗时。平均值，微秒us</summary>
        Int32 Cost { get; set; }

        /// <summary>总耗时。微秒us</summary>
        Int64 TotalCost { get; set; }

        /// <summary>最后IP</summary>
        String LastIP { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>更新时间</summary>
        DateTime UpdateTime { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}