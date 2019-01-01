using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data
{
    /// <summary>IP统计。记录IP地址每天访问量</summary>
    [Serializable]
    [DataObject]
    [Description("IP统计。记录IP地址每天访问量")]
    [BindIndex("IU_IpStat_IP_Level_Time", true, "IP,Level,Time")]
    [BindIndex("IX_IpStat_Level_Time", false, "Level,Time")]
    [BindTable("IpStat", Description = "IP统计。记录IP地址每天访问量", ConnName = "Registry", DbType = DatabaseType.None)]
    public partial class IpStat : IIpStat
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get { return _ID; } set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private String _Ip;
        /// <summary>IP地址</summary>
        [DisplayName("IP地址")]
        [Description("IP地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Ip", "IP地址", "")]
        public String Ip { get { return _Ip; } set { if (OnPropertyChanging(__.Ip, value)) { _Ip = value; OnPropertyChanged(__.Ip); } } }

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

        private Int32 _LastAppID;
        /// <summary>最后应用</summary>
        [DisplayName("最后应用")]
        [Description("最后应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("LastAppID", "最后应用", "")]
        public Int32 LastAppID { get { return _LastAppID; } set { if (OnPropertyChanging(__.LastAppID, value)) { _LastAppID = value; OnPropertyChanged(__.LastAppID); } } }

        private Int32 _LastServiceID;
        /// <summary>最后服务</summary>
        [DisplayName("最后服务")]
        [Description("最后服务")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("LastServiceID", "最后服务", "")]
        public Int32 LastServiceID { get { return _LastServiceID; } set { if (OnPropertyChanging(__.LastServiceID, value)) { _LastServiceID = value; OnPropertyChanged(__.LastServiceID); } } }

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
                    case __.Ip : return _Ip;
                    case __.Level : return _Level;
                    case __.Time : return _Time;
                    case __.Count : return _Count;
                    case __.Cost : return _Cost;
                    case __.TotalCost : return _TotalCost;
                    case __.LastAppID : return _LastAppID;
                    case __.LastServiceID : return _LastServiceID;
                    case __.CreateTime : return _CreateTime;
                    case __.UpdateTime : return _UpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID : _ID = Convert.ToInt32(value); break;
                    case __.Ip : _Ip = Convert.ToString(value); break;
                    case __.Level : _Level = (XCode.Statistics.StatLevels)Convert.ToInt32(value); break;
                    case __.Time : _Time = Convert.ToDateTime(value); break;
                    case __.Count : _Count = Convert.ToInt64(value); break;
                    case __.Cost : _Cost = Convert.ToInt32(value); break;
                    case __.TotalCost : _TotalCost = Convert.ToInt64(value); break;
                    case __.LastAppID : _LastAppID = Convert.ToInt32(value); break;
                    case __.LastServiceID : _LastServiceID = Convert.ToInt32(value); break;
                    case __.CreateTime : _CreateTime = Convert.ToDateTime(value); break;
                    case __.UpdateTime : _UpdateTime = Convert.ToDateTime(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得IP统计字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>IP地址</summary>
            public static readonly Field Ip = FindByName(__.Ip);

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

            /// <summary>最后应用</summary>
            public static readonly Field LastAppID = FindByName(__.LastAppID);

            /// <summary>最后服务</summary>
            public static readonly Field LastServiceID = FindByName(__.LastServiceID);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得IP统计字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>IP地址</summary>
            public const String Ip = "Ip";

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

            /// <summary>最后应用</summary>
            public const String LastAppID = "LastAppID";

            /// <summary>最后服务</summary>
            public const String LastServiceID = "LastServiceID";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";
        }
        #endregion
    }

    /// <summary>IP统计。记录IP地址每天访问量接口</summary>
    public partial interface IIpStat
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>IP地址</summary>
        String Ip { get; set; }

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

        /// <summary>最后应用</summary>
        Int32 LastAppID { get; set; }

        /// <summary>最后服务</summary>
        Int32 LastServiceID { get; set; }

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