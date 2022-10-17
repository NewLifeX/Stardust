using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes
{
    /// <summary>节点统计。每日统计</summary>
    [Serializable]
    [DataObject]
    [Description("节点统计。每日统计")]
    [BindIndex("IU_NodeStat_StatDate_AreaID", true, "StatDate,AreaID")]
    [BindIndex("IX_NodeStat_UpdateTime_AreaID", false, "UpdateTime,AreaID")]
    [BindTable("NodeStat", Description = "节点统计。每日统计", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class NodeStat
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
        [BindColumn("StatDate", "统计日期", "")]
        public DateTime StatDate { get => _StatDate; set { if (OnPropertyChanging("StatDate", value)) { _StatDate = value; OnPropertyChanged("StatDate"); } } }

        private Int32 _AreaID;
        /// <summary>地区。省份，0表示全国</summary>
        [DisplayName("地区")]
        [Description("地区。省份，0表示全国")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AreaID", "地区。省份，0表示全国", "")]
        public Int32 AreaID { get => _AreaID; set { if (OnPropertyChanging("AreaID", value)) { _AreaID = value; OnPropertyChanged("AreaID"); } } }

        private Int32 _Total;
        /// <summary>总数。截止今天的全部设备数</summary>
        [DisplayName("总数")]
        [Description("总数。截止今天的全部设备数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Total", "总数。截止今天的全部设备数", "")]
        public Int32 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

        private Int32 _Actives;
        /// <summary>活跃数。最后登录位于今天</summary>
        [DisplayName("活跃数")]
        [Description("活跃数。最后登录位于今天")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Actives", "活跃数。最后登录位于今天", "")]
        public Int32 Actives { get => _Actives; set { if (OnPropertyChanging("Actives", value)) { _Actives = value; OnPropertyChanged("Actives"); } } }

        private Int32 _T7Actives;
        /// <summary>7天活跃数。最后登录位于7天内</summary>
        [DisplayName("7天活跃数")]
        [Description("7天活跃数。最后登录位于7天内")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("T7Actives", "7天活跃数。最后登录位于7天内", "")]
        public Int32 T7Actives { get => _T7Actives; set { if (OnPropertyChanging("T7Actives", value)) { _T7Actives = value; OnPropertyChanged("T7Actives"); } } }

        private Int32 _T30Actives;
        /// <summary>30天活跃数。最后登录位于30天内</summary>
        [DisplayName("30天活跃数")]
        [Description("30天活跃数。最后登录位于30天内")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("T30Actives", "30天活跃数。最后登录位于30天内", "")]
        public Int32 T30Actives { get => _T30Actives; set { if (OnPropertyChanging("T30Actives", value)) { _T30Actives = value; OnPropertyChanged("T30Actives"); } } }

        private Int32 _News;
        /// <summary>新增数。今天创建</summary>
        [DisplayName("新增数")]
        [Description("新增数。今天创建")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("News", "新增数。今天创建", "")]
        public Int32 News { get => _News; set { if (OnPropertyChanging("News", value)) { _News = value; OnPropertyChanged("News"); } } }

        private Int32 _T7News;
        /// <summary>7天新增数。7天创建</summary>
        [DisplayName("7天新增数")]
        [Description("7天新增数。7天创建")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("T7News", "7天新增数。7天创建", "")]
        public Int32 T7News { get => _T7News; set { if (OnPropertyChanging("T7News", value)) { _T7News = value; OnPropertyChanged("T7News"); } } }

        private Int32 _T30News;
        /// <summary>30天新增数。30天创建</summary>
        [DisplayName("30天新增数")]
        [Description("30天新增数。30天创建")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("T30News", "30天新增数。30天创建", "")]
        public Int32 T30News { get => _T30News; set { if (OnPropertyChanging("T30News", value)) { _T30News = value; OnPropertyChanged("T30News"); } } }

        private Int32 _Registers;
        /// <summary>注册数。今天激活或重新激活</summary>
        [DisplayName("注册数")]
        [Description("注册数。今天激活或重新激活")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Registers", "注册数。今天激活或重新激活", "")]
        public Int32 Registers { get => _Registers; set { if (OnPropertyChanging("Registers", value)) { _Registers = value; OnPropertyChanged("Registers"); } } }

        private Int32 _MaxOnline;
        /// <summary>最高在线。今天最高在线数</summary>
        [DisplayName("最高在线")]
        [Description("最高在线。今天最高在线数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxOnline", "最高在线。今天最高在线数", "")]
        public Int32 MaxOnline { get => _MaxOnline; set { if (OnPropertyChanging("MaxOnline", value)) { _MaxOnline = value; OnPropertyChanged("MaxOnline"); } } }

        private DateTime _MaxOnlineTime;
        /// <summary>最高在线时间</summary>
        [DisplayName("最高在线时间")]
        [Description("最高在线时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("MaxOnlineTime", "最高在线时间", "")]
        public DateTime MaxOnlineTime { get => _MaxOnlineTime; set { if (OnPropertyChanging("MaxOnlineTime", value)) { _MaxOnlineTime = value; OnPropertyChanged("MaxOnlineTime"); } } }

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
            get
            {
                switch (name)
                {
                    case "ID": return _ID;
                    case "StatDate": return _StatDate;
                    case "AreaID": return _AreaID;
                    case "Total": return _Total;
                    case "Actives": return _Actives;
                    case "T7Actives": return _T7Actives;
                    case "T30Actives": return _T30Actives;
                    case "News": return _News;
                    case "T7News": return _T7News;
                    case "T30News": return _T30News;
                    case "Registers": return _Registers;
                    case "MaxOnline": return _MaxOnline;
                    case "MaxOnlineTime": return _MaxOnlineTime;
                    case "CreateTime": return _CreateTime;
                    case "UpdateTime": return _UpdateTime;
                    case "Remark": return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToInt(); break;
                    case "StatDate": _StatDate = value.ToDateTime(); break;
                    case "AreaID": _AreaID = value.ToInt(); break;
                    case "Total": _Total = value.ToInt(); break;
                    case "Actives": _Actives = value.ToInt(); break;
                    case "T7Actives": _T7Actives = value.ToInt(); break;
                    case "T30Actives": _T30Actives = value.ToInt(); break;
                    case "News": _News = value.ToInt(); break;
                    case "T7News": _T7News = value.ToInt(); break;
                    case "T30News": _T30News = value.ToInt(); break;
                    case "Registers": _Registers = value.ToInt(); break;
                    case "MaxOnline": _MaxOnline = value.ToInt(); break;
                    case "MaxOnlineTime": _MaxOnlineTime = value.ToDateTime(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                    case "Remark": _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得节点统计字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>统计日期</summary>
            public static readonly Field StatDate = FindByName("StatDate");

            /// <summary>地区。省份，0表示全国</summary>
            public static readonly Field AreaID = FindByName("AreaID");

            /// <summary>总数。截止今天的全部设备数</summary>
            public static readonly Field Total = FindByName("Total");

            /// <summary>活跃数。最后登录位于今天</summary>
            public static readonly Field Actives = FindByName("Actives");

            /// <summary>7天活跃数。最后登录位于7天内</summary>
            public static readonly Field T7Actives = FindByName("T7Actives");

            /// <summary>30天活跃数。最后登录位于30天内</summary>
            public static readonly Field T30Actives = FindByName("T30Actives");

            /// <summary>新增数。今天创建</summary>
            public static readonly Field News = FindByName("News");

            /// <summary>7天新增数。7天创建</summary>
            public static readonly Field T7News = FindByName("T7News");

            /// <summary>30天新增数。30天创建</summary>
            public static readonly Field T30News = FindByName("T30News");

            /// <summary>注册数。今天激活或重新激活</summary>
            public static readonly Field Registers = FindByName("Registers");

            /// <summary>最高在线。今天最高在线数</summary>
            public static readonly Field MaxOnline = FindByName("MaxOnline");

            /// <summary>最高在线时间</summary>
            public static readonly Field MaxOnlineTime = FindByName("MaxOnlineTime");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName("UpdateTime");

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName("Remark");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得节点统计字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>统计日期</summary>
            public const String StatDate = "StatDate";

            /// <summary>地区。省份，0表示全国</summary>
            public const String AreaID = "AreaID";

            /// <summary>总数。截止今天的全部设备数</summary>
            public const String Total = "Total";

            /// <summary>活跃数。最后登录位于今天</summary>
            public const String Actives = "Actives";

            /// <summary>7天活跃数。最后登录位于7天内</summary>
            public const String T7Actives = "T7Actives";

            /// <summary>30天活跃数。最后登录位于30天内</summary>
            public const String T30Actives = "T30Actives";

            /// <summary>新增数。今天创建</summary>
            public const String News = "News";

            /// <summary>7天新增数。7天创建</summary>
            public const String T7News = "T7News";

            /// <summary>30天新增数。30天创建</summary>
            public const String T30News = "T30News";

            /// <summary>注册数。今天激活或重新激活</summary>
            public const String Registers = "Registers";

            /// <summary>最高在线。今天最高在线数</summary>
            public const String MaxOnline = "MaxOnline";

            /// <summary>最高在线时间</summary>
            public const String MaxOnlineTime = "MaxOnlineTime";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>更新时间</summary>
            public const String UpdateTime = "UpdateTime";

            /// <summary>备注</summary>
            public const String Remark = "Remark";
        }
        #endregion
    }
}