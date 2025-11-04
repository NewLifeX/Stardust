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

namespace Stardust.Data.Nodes;

/// <summary>节点统计。每日统计</summary>
[Serializable]
[DataObject]
[Description("节点统计。每日统计")]
[BindIndex("IU_NodeStat_Category_StatDate_Key", true, "Category,StatDate,Key")]
[BindIndex("IX_NodeStat_Category_Key", false, "Category,Key")]
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

    private String _Category;
    /// <summary>类别。业务方向分类，例如操作系统占比</summary>
    [DisplayName("类别")]
    [Description("类别。业务方向分类，例如操作系统占比")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别。业务方向分类，例如操作系统占比", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private DateTime _StatDate;
    /// <summary>统计日期</summary>
    [DisplayName("统计日期")]
    [Description("统计日期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StatDate", "统计日期", "", DataScale = "time:yyyy-MM-dd")]
    public DateTime StatDate { get => _StatDate; set { if (OnPropertyChanging("StatDate", value)) { _StatDate = value; OnPropertyChanged("StatDate"); } } }

    private String _Key;
    /// <summary>统计项。统计项编码</summary>
    [DisplayName("统计项")]
    [Description("统计项。统计项编码")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Key", "统计项。统计项编码", "")]
    public String Key { get => _Key; set { if (OnPropertyChanging("Key", value)) { _Key = value; OnPropertyChanged("Key"); } } }

    private String _LinkItem;
    /// <summary>关联项</summary>
    [DisplayName("关联项")]
    [Description("关联项")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("LinkItem", "关联项", "")]
    public String LinkItem { get => _LinkItem; set { if (OnPropertyChanging("LinkItem", value)) { _LinkItem = value; OnPropertyChanged("LinkItem"); } } }

    private Int32 _Total;
    /// <summary>总数。1年内活跃过的全部节点数</summary>
    [DisplayName("总数")]
    [Description("总数。1年内活跃过的全部节点数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总数。1年内活跃过的全部节点数", "")]
    public Int32 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int32 _Actives;
    /// <summary>活跃数。最后活跃位于今天</summary>
    [DisplayName("活跃数")]
    [Description("活跃数。最后活跃位于今天")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Actives", "活跃数。最后活跃位于今天", "")]
    public Int32 Actives { get => _Actives; set { if (OnPropertyChanging("Actives", value)) { _Actives = value; OnPropertyChanged("Actives"); } } }

    private Int32 _ActivesT7;
    /// <summary>7天活跃数。最后活跃位于7天内</summary>
    [DisplayName("7天活跃数")]
    [Description("7天活跃数。最后活跃位于7天内")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ActivesT7", "7天活跃数。最后活跃位于7天内", "")]
    public Int32 ActivesT7 { get => _ActivesT7; set { if (OnPropertyChanging("ActivesT7", value)) { _ActivesT7 = value; OnPropertyChanged("ActivesT7"); } } }

    private Int32 _ActivesT30;
    /// <summary>30天活跃数。最后活跃位于30天内</summary>
    [DisplayName("30天活跃数")]
    [Description("30天活跃数。最后活跃位于30天内")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ActivesT30", "30天活跃数。最后活跃位于30天内", "")]
    public Int32 ActivesT30 { get => _ActivesT30; set { if (OnPropertyChanging("ActivesT30", value)) { _ActivesT30 = value; OnPropertyChanged("ActivesT30"); } } }

    private Int32 _News;
    /// <summary>新增数。今天创建</summary>
    [DisplayName("新增数")]
    [Description("新增数。今天创建")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("News", "新增数。今天创建", "")]
    public Int32 News { get => _News; set { if (OnPropertyChanging("News", value)) { _News = value; OnPropertyChanged("News"); } } }

    private Int32 _NewsT7;
    /// <summary>7天新增数。7天创建</summary>
    [DisplayName("7天新增数")]
    [Description("7天新增数。7天创建")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NewsT7", "7天新增数。7天创建", "")]
    public Int32 NewsT7 { get => _NewsT7; set { if (OnPropertyChanging("NewsT7", value)) { _NewsT7 = value; OnPropertyChanged("NewsT7"); } } }

    private Int32 _NewsT30;
    /// <summary>30天新增数。30天创建</summary>
    [DisplayName("30天新增数")]
    [Description("30天新增数。30天创建")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NewsT30", "30天新增数。30天创建", "")]
    public Int32 NewsT30 { get => _NewsT30; set { if (OnPropertyChanging("NewsT30", value)) { _NewsT30 = value; OnPropertyChanged("NewsT30"); } } }

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
        get => name switch
        {
            "ID" => _ID,
            "Category" => _Category,
            "StatDate" => _StatDate,
            "Key" => _Key,
            "LinkItem" => _LinkItem,
            "Total" => _Total,
            "Actives" => _Actives,
            "ActivesT7" => _ActivesT7,
            "ActivesT30" => _ActivesT30,
            "News" => _News,
            "NewsT7" => _NewsT7,
            "NewsT30" => _NewsT30,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "StatDate": _StatDate = value.ToDateTime(); break;
                case "Key": _Key = Convert.ToString(value); break;
                case "LinkItem": _LinkItem = Convert.ToString(value); break;
                case "Total": _Total = value.ToInt(); break;
                case "Actives": _Actives = value.ToInt(); break;
                case "ActivesT7": _ActivesT7 = value.ToInt(); break;
                case "ActivesT30": _ActivesT30 = value.ToInt(); break;
                case "News": _News = value.ToInt(); break;
                case "NewsT7": _NewsT7 = value.ToInt(); break;
                case "NewsT30": _NewsT30 = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据类别查找</summary>
    /// <param name="category">类别</param>
    /// <returns>实体列表</returns>
    public static IList<NodeStat> FindAllByCategory(String category)
    {
        if (category.IsNullOrEmpty()) return [];

        return FindAll(_.Category == category);
    }

    /// <summary>根据类别、统计日期查找</summary>
    /// <param name="category">类别</param>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<NodeStat> FindAllByCategoryAndStatDate(String category, DateTime statDate)
    {
        if (category.IsNullOrEmpty()) return [];
        if (statDate.Year < 1000) return [];

        return FindAll(_.Category == category & _.StatDate == statDate);
    }

    /// <summary>根据统计日期查找</summary>
    /// <param name="statDate">统计日期</param>
    /// <returns>实体列表</returns>
    public static IList<NodeStat> FindAllByStatDate(DateTime statDate)
    {
        if (statDate.Year < 1000) return [];

        return FindAll(_.StatDate == statDate);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">类别。业务方向分类，例如操作系统占比</param>
    /// <param name="start">统计日期开始</param>
    /// <param name="end">统计日期结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<NodeStat> Search(String category, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        exp &= _.StatDate.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        if (start == end) return Delete(_.StatDate == start);

        return Delete(_.StatDate.Between(start, end), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得节点统计字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>类别。业务方向分类，例如操作系统占比</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>统计日期</summary>
        public static readonly Field StatDate = FindByName("StatDate");

        /// <summary>统计项。统计项编码</summary>
        public static readonly Field Key = FindByName("Key");

        /// <summary>关联项</summary>
        public static readonly Field LinkItem = FindByName("LinkItem");

        /// <summary>总数。1年内活跃过的全部节点数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>活跃数。最后活跃位于今天</summary>
        public static readonly Field Actives = FindByName("Actives");

        /// <summary>7天活跃数。最后活跃位于7天内</summary>
        public static readonly Field ActivesT7 = FindByName("ActivesT7");

        /// <summary>30天活跃数。最后活跃位于30天内</summary>
        public static readonly Field ActivesT30 = FindByName("ActivesT30");

        /// <summary>新增数。今天创建</summary>
        public static readonly Field News = FindByName("News");

        /// <summary>7天新增数。7天创建</summary>
        public static readonly Field NewsT7 = FindByName("NewsT7");

        /// <summary>30天新增数。30天创建</summary>
        public static readonly Field NewsT30 = FindByName("NewsT30");

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

        /// <summary>类别。业务方向分类，例如操作系统占比</summary>
        public const String Category = "Category";

        /// <summary>统计日期</summary>
        public const String StatDate = "StatDate";

        /// <summary>统计项。统计项编码</summary>
        public const String Key = "Key";

        /// <summary>关联项</summary>
        public const String LinkItem = "LinkItem";

        /// <summary>总数。1年内活跃过的全部节点数</summary>
        public const String Total = "Total";

        /// <summary>活跃数。最后活跃位于今天</summary>
        public const String Actives = "Actives";

        /// <summary>7天活跃数。最后活跃位于7天内</summary>
        public const String ActivesT7 = "ActivesT7";

        /// <summary>30天活跃数。最后活跃位于30天内</summary>
        public const String ActivesT30 = "ActivesT30";

        /// <summary>新增数。今天创建</summary>
        public const String News = "News";

        /// <summary>7天新增数。7天创建</summary>
        public const String NewsT7 = "NewsT7";

        /// <summary>30天新增数。30天创建</summary>
        public const String NewsT30 = "NewsT30";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
