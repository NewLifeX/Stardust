using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    [BindTable("NodeStat", Description = "节点统计。每日统计", ConnName = "Node", DbType = DatabaseType.None)]
    public partial class NodeStat : INodeStat
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private DateTime _StatDate;
        /// <summary>统计日期</summary>
        [DisplayName("统计日期")]
        [Description("统计日期")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("StatDate", "统计日期", "")]
        public DateTime StatDate { get => _StatDate; set { if (OnPropertyChanging(__.StatDate, value)) { _StatDate = value; OnPropertyChanged(__.StatDate); } } }

        private Int32 _AreaID;
        /// <summary>地区。省份，0表示全国</summary>
        [DisplayName("地区")]
        [Description("地区。省份，0表示全国")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AreaID", "地区。省份，0表示全国", "")]
        public Int32 AreaID { get => _AreaID; set { if (OnPropertyChanging(__.AreaID, value)) { _AreaID = value; OnPropertyChanged(__.AreaID); } } }

        private Int32 _Total;
        /// <summary>总数。截止今天的全部设备数</summary>
        [DisplayName("总数")]
        [Description("总数。截止今天的全部设备数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Total", "总数。截止今天的全部设备数", "")]
        public Int32 Total { get => _Total; set { if (OnPropertyChanging(__.Total, value)) { _Total = value; OnPropertyChanged(__.Total); } } }

        private Int32 _Actives;
        /// <summary>活跃数。最后登录位于今天</summary>
        [DisplayName("活跃数")]
        [Description("活跃数。最后登录位于今天")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Actives", "活跃数。最后登录位于今天", "")]
        public Int32 Actives { get => _Actives; set { if (OnPropertyChanging(__.Actives, value)) { _Actives = value; OnPropertyChanged(__.Actives); } } }

        private Int32 _News;
        /// <summary>新增数。今天创建</summary>
        [DisplayName("新增数")]
        [Description("新增数。今天创建")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("News", "新增数。今天创建", "")]
        public Int32 News { get => _News; set { if (OnPropertyChanging(__.News, value)) { _News = value; OnPropertyChanged(__.News); } } }

        private Int32 _Registers;
        /// <summary>注册数。今天激活或重新激活</summary>
        [DisplayName("注册数")]
        [Description("注册数。今天激活或重新激活")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Registers", "注册数。今天激活或重新激活", "")]
        public Int32 Registers { get => _Registers; set { if (OnPropertyChanging(__.Registers, value)) { _Registers = value; OnPropertyChanged(__.Registers); } } }

        private Int32 _MaxOnline;
        /// <summary>最高在线。今天最高在线数</summary>
        [DisplayName("最高在线")]
        [Description("最高在线。今天最高在线数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxOnline", "最高在线。今天最高在线数", "")]
        public Int32 MaxOnline { get => _MaxOnline; set { if (OnPropertyChanging(__.MaxOnline, value)) { _MaxOnline = value; OnPropertyChanged(__.MaxOnline); } } }

        private DateTime _MaxOnlineTime;
        /// <summary>最高在线时间</summary>
        [DisplayName("最高在线时间")]
        [Description("最高在线时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("MaxOnlineTime", "最高在线时间", "")]
        public DateTime MaxOnlineTime { get => _MaxOnlineTime; set { if (OnPropertyChanging(__.MaxOnlineTime, value)) { _MaxOnlineTime = value; OnPropertyChanged(__.MaxOnlineTime); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging(__.CreateTime, value)) { _CreateTime = value; OnPropertyChanged(__.CreateTime); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging(__.UpdateTime, value)) { _UpdateTime = value; OnPropertyChanged(__.UpdateTime); } } }

        private String _Remark;
        /// <summary>备注</summary>
        [DisplayName("备注")]
        [Description("备注")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Remark", "备注", "")]
        public String Remark { get => _Remark; set { if (OnPropertyChanging(__.Remark, value)) { _Remark = value; OnPropertyChanged(__.Remark); } } }
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
                    case __.ID: return _ID;
                    case __.StatDate: return _StatDate;
                    case __.AreaID: return _AreaID;
                    case __.Total: return _Total;
                    case __.Actives: return _Actives;
                    case __.News: return _News;
                    case __.Registers: return _Registers;
                    case __.MaxOnline: return _MaxOnline;
                    case __.MaxOnlineTime: return _MaxOnlineTime;
                    case __.CreateTime: return _CreateTime;
                    case __.UpdateTime: return _UpdateTime;
                    case __.Remark: return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID: _ID = value.ToInt(); break;
                    case __.StatDate: _StatDate = value.ToDateTime(); break;
                    case __.AreaID: _AreaID = value.ToInt(); break;
                    case __.Total: _Total = value.ToInt(); break;
                    case __.Actives: _Actives = value.ToInt(); break;
                    case __.News: _News = value.ToInt(); break;
                    case __.Registers: _Registers = value.ToInt(); break;
                    case __.MaxOnline: _MaxOnline = value.ToInt(); break;
                    case __.MaxOnlineTime: _MaxOnlineTime = value.ToDateTime(); break;
                    case __.CreateTime: _CreateTime = value.ToDateTime(); break;
                    case __.UpdateTime: _UpdateTime = value.ToDateTime(); break;
                    case __.Remark: _Remark = Convert.ToString(value); break;
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
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>统计日期</summary>
            public static readonly Field StatDate = FindByName(__.StatDate);

            /// <summary>地区。省份，0表示全国</summary>
            public static readonly Field AreaID = FindByName(__.AreaID);

            /// <summary>总数。截止今天的全部设备数</summary>
            public static readonly Field Total = FindByName(__.Total);

            /// <summary>活跃数。最后登录位于今天</summary>
            public static readonly Field Actives = FindByName(__.Actives);

            /// <summary>新增数。今天创建</summary>
            public static readonly Field News = FindByName(__.News);

            /// <summary>注册数。今天激活或重新激活</summary>
            public static readonly Field Registers = FindByName(__.Registers);

            /// <summary>最高在线。今天最高在线数</summary>
            public static readonly Field MaxOnline = FindByName(__.MaxOnline);

            /// <summary>最高在线时间</summary>
            public static readonly Field MaxOnlineTime = FindByName(__.MaxOnlineTime);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName(__.Remark);

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

            /// <summary>新增数。今天创建</summary>
            public const String News = "News";

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

    /// <summary>节点统计。每日统计接口</summary>
    public partial interface INodeStat
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>统计日期</summary>
        DateTime StatDate { get; set; }

        /// <summary>地区。省份，0表示全国</summary>
        Int32 AreaID { get; set; }

        /// <summary>总数。截止今天的全部设备数</summary>
        Int32 Total { get; set; }

        /// <summary>活跃数。最后登录位于今天</summary>
        Int32 Actives { get; set; }

        /// <summary>新增数。今天创建</summary>
        Int32 News { get; set; }

        /// <summary>注册数。今天激活或重新激活</summary>
        Int32 Registers { get; set; }

        /// <summary>最高在线。今天最高在线数</summary>
        Int32 MaxOnline { get; set; }

        /// <summary>最高在线时间</summary>
        DateTime MaxOnlineTime { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>更新时间</summary>
        DateTime UpdateTime { get; set; }

        /// <summary>备注</summary>
        String Remark { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}