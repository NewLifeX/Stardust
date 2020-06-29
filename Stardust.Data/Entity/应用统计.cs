using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data
{
    /// <summary>应用统计。记录应用每天访问量</summary>
    [Serializable]
    [DataObject]
    [Description("应用统计。记录应用每天访问量")]
    [BindIndex("IU_AppStat_AppID_Level_Time", true, "AppID,Level,Time")]
    [BindIndex("IX_AppStat_Level_Time", false, "Level,Time")]
    [BindTable("AppStat", Description = "应用统计。记录应用每天访问量", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppStat : IAppStat
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

        private Int32 _AppID;
        /// <summary>应用</summary>
        [DisplayName("应用")]
        [Description("应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppID", "应用", "")]
        public Int32 AppID { get => _AppID; set { if (OnPropertyChanging("AppID", value)) { _AppID = value; OnPropertyChanged("AppID"); } } }

        private XCode.Statistics.StatLevels _Level;
        /// <summary>层级</summary>
        [DisplayName("层级")]
        [Description("层级")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Level", "层级", "")]
        public XCode.Statistics.StatLevels Level { get => _Level; set { if (OnPropertyChanging("Level", value)) { _Level = value; OnPropertyChanged("Level"); } } }

        private DateTime _Time;
        /// <summary>日期</summary>
        [DisplayName("日期")]
        [Description("日期")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("Time", "日期", "")]
        public DateTime Time { get => _Time; set { if (OnPropertyChanging("Time", value)) { _Time = value; OnPropertyChanged("Time"); } } }

        private Int64 _Count;
        /// <summary>次数</summary>
        [DisplayName("次数")]
        [Description("次数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Count", "次数", "")]
        public Int64 Count { get => _Count; set { if (OnPropertyChanging("Count", value)) { _Count = value; OnPropertyChanged("Count"); } } }

        private Int32 _Cost;
        /// <summary>耗时。平均值，微秒us</summary>
        [DisplayName("耗时")]
        [Description("耗时。平均值，微秒us")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Cost", "耗时。平均值，微秒us", "")]
        public Int32 Cost { get => _Cost; set { if (OnPropertyChanging("Cost", value)) { _Cost = value; OnPropertyChanged("Cost"); } } }

        private Int64 _TotalCost;
        /// <summary>总耗时。微秒us</summary>
        [DisplayName("总耗时")]
        [Description("总耗时。微秒us")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("TotalCost", "总耗时。微秒us", "")]
        public Int64 TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

        private String _LastIP;
        /// <summary>最后IP</summary>
        [DisplayName("最后IP")]
        [Description("最后IP")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastIP", "最后IP", "")]
        public String LastIP { get => _LastIP; set { if (OnPropertyChanging("LastIP", value)) { _LastIP = value; OnPropertyChanged("LastIP"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
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
            get
            {
                switch (name)
                {
                    case "ID": return _ID;
                    case "AppID": return _AppID;
                    case "Level": return _Level;
                    case "Time": return _Time;
                    case "Count": return _Count;
                    case "Cost": return _Cost;
                    case "TotalCost": return _TotalCost;
                    case "LastIP": return _LastIP;
                    case "CreateTime": return _CreateTime;
                    case "UpdateTime": return _UpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToInt(); break;
                    case "AppID": _AppID = value.ToInt(); break;
                    case "Level": _Level = (XCode.Statistics.StatLevels)value.ToInt(); break;
                    case "Time": _Time = value.ToDateTime(); break;
                    case "Count": _Count = value.ToLong(); break;
                    case "Cost": _Cost = value.ToInt(); break;
                    case "TotalCost": _TotalCost = value.ToLong(); break;
                    case "LastIP": _LastIP = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得应用统计字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>应用</summary>
            public static readonly Field AppID = FindByName("AppID");

            /// <summary>层级</summary>
            public static readonly Field Level = FindByName("Level");

            /// <summary>日期</summary>
            public static readonly Field Time = FindByName("Time");

            /// <summary>次数</summary>
            public static readonly Field Count = FindByName("Count");

            /// <summary>耗时。平均值，微秒us</summary>
            public static readonly Field Cost = FindByName("Cost");

            /// <summary>总耗时。微秒us</summary>
            public static readonly Field TotalCost = FindByName("TotalCost");

            /// <summary>最后IP</summary>
            public static readonly Field LastIP = FindByName("LastIP");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得应用统计字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>应用</summary>
            public const String AppID = "AppID";

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

    /// <summary>应用统计。记录应用每天访问量接口</summary>
    public partial interface IAppStat
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>应用</summary>
        Int32 AppID { get; set; }

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