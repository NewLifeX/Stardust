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

/// <summary>报警记录。记录报警的开始、持续和恢复，统计报警持续时间</summary>
[Serializable]
[DataObject]
[Description("报警记录。记录报警的开始、持续和恢复，统计报警持续时间")]
[BindIndex("IX_AlarmRecord_GroupId_Status", false, "GroupId,Status")]
[BindIndex("IX_AlarmRecord_Category_Action_Status", false, "Category,Action,Status")]
[BindIndex("IX_AlarmRecord_Status_UpdateTime", false, "Status,UpdateTime")]
[BindTable("AlarmRecord", Description = "报警记录。记录报警的开始、持续和恢复，统计报警持续时间", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AlarmRecord
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _GroupId;
    /// <summary>告警组</summary>
    [DisplayName("告警组")]
    [Description("告警组")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("GroupId", "告警组", "")]
    public Int32 GroupId { get => _GroupId; set { if (OnPropertyChanging("GroupId", value)) { _GroupId = value; OnPropertyChanged("GroupId"); } } }

    private String _Category;
    /// <summary>类别。应用下线、节点下线、错误数过高等</summary>
    [DisplayName("类别")]
    [Description("类别。应用下线、节点下线、错误数过高等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别。应用下线、节点下线、错误数过高等", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Action;
    /// <summary>操作。报警对象，如应用名或节点名</summary>
    [DisplayName("操作")]
    [Description("操作。报警对象，如应用名或节点名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Action", "操作。报警对象，如应用名或节点名", "")]
    public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

    private Stardust.Data.Monitors.AlarmStatuses _Status;
    /// <summary>状态。报警中、已恢复</summary>
    [DisplayName("状态")]
    [Description("状态。报警中、已恢复")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态。报警中、已恢复", "")]
    public Stardust.Data.Monitors.AlarmStatuses Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private DateTime _StartTime;
    /// <summary>开始时间。报警首次触发的时间</summary>
    [DisplayName("开始时间")]
    [Description("开始时间。报警首次触发的时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StartTime", "开始时间。报警首次触发的时间", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private DateTime _EndTime;
    /// <summary>结束时间。报警恢复的时间</summary>
    [DisplayName("结束时间")]
    [Description("结束时间。报警恢复的时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("EndTime", "结束时间。报警恢复的时间", "")]
    public DateTime EndTime { get => _EndTime; set { if (OnPropertyChanging("EndTime", value)) { _EndTime = value; OnPropertyChanged("EndTime"); } } }

    private Int32 _Duration;
    /// <summary>持续时间。报警持续的秒数</summary>
    [DisplayName("持续时间")]
    [Description("持续时间。报警持续的秒数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Duration", "持续时间。报警持续的秒数", "", ItemType = "TimeSpan")]
    public Int32 Duration { get => _Duration; set { if (OnPropertyChanging("Duration", value)) { _Duration = value; OnPropertyChanged("Duration"); } } }

    private Int32 _Times;
    /// <summary>通知次数。累计发送报警通知的次数</summary>
    [DisplayName("通知次数")]
    [Description("通知次数。累计发送报警通知的次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Times", "通知次数。累计发送报警通知的次数", "")]
    public Int32 Times { get => _Times; set { if (OnPropertyChanging("Times", value)) { _Times = value; OnPropertyChanged("Times"); } } }

    private String _Content;
    /// <summary>内容。报警详细信息</summary>
    [DisplayName("内容")]
    [Description("内容。报警详细信息")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Content", "内容。报警详细信息", "")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private String _Creator;
    /// <summary>创建者。服务端节点</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者。服务端节点")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Creator", "创建者。服务端节点", "")]
    public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

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
            "Id" => _Id,
            "Name" => _Name,
            "GroupId" => _GroupId,
            "Category" => _Category,
            "Action" => _Action,
            "Status" => _Status,
            "StartTime" => _StartTime,
            "EndTime" => _EndTime,
            "Duration" => _Duration,
            "Times" => _Times,
            "Content" => _Content,
            "Creator" => _Creator,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "GroupId": _GroupId = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Action": _Action = Convert.ToString(value); break;
                case "Status": _Status = (Stardust.Data.Monitors.AlarmStatuses)value.ToInt(); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "EndTime": _EndTime = value.ToDateTime(); break;
                case "Duration": _Duration = value.ToInt(); break;
                case "Times": _Times = value.ToInt(); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "Creator": _Creator = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>告警组</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AlarmGroup Group => Extends.Get(nameof(Group), k => AlarmGroup.FindById(GroupId));

    /// <summary>告警组</summary>
    [Map(nameof(GroupId), typeof(AlarmGroup), "Id")]
    public String GroupName => Group?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AlarmRecord FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据告警组、状态查找</summary>
    /// <param name="groupId">告警组</param>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmRecord> FindAllByGroupIdAndStatus(Int32 groupId, Stardust.Data.Monitors.AlarmStatuses status)
    {
        if (groupId < 0) return [];
        if (status < 0) return [];

        return FindAll(_.GroupId == groupId & _.Status == status);
    }

    /// <summary>根据类别、操作、状态查找</summary>
    /// <param name="category">类别</param>
    /// <param name="action">操作</param>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmRecord> FindAllByCategoryAndActionAndStatus(String category, String action, Stardust.Data.Monitors.AlarmStatuses status)
    {
        if (category.IsNullOrEmpty()) return [];
        if (action.IsNullOrEmpty()) return [];
        if (status < 0) return [];

        return FindAll(_.Category == category & _.Action == action & _.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="groupId">告警组</param>
    /// <param name="category">类别。应用下线、节点下线、错误数过高等</param>
    /// <param name="action">操作。报警对象，如应用名或节点名</param>
    /// <param name="status">状态。报警中、已恢复</param>
    /// <param name="updateTime">更新时间</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmRecord> Search(Int32 groupId, String category, String action, Stardust.Data.Monitors.AlarmStatuses status, DateTime updateTime, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (groupId >= 0) exp &= _.GroupId == groupId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
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
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得报警记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>告警组</summary>
        public static readonly Field GroupId = FindByName("GroupId");

        /// <summary>类别。应用下线、节点下线、错误数过高等</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>操作。报警对象，如应用名或节点名</summary>
        public static readonly Field Action = FindByName("Action");

        /// <summary>状态。报警中、已恢复</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>开始时间。报警首次触发的时间</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>结束时间。报警恢复的时间</summary>
        public static readonly Field EndTime = FindByName("EndTime");

        /// <summary>持续时间。报警持续的秒数</summary>
        public static readonly Field Duration = FindByName("Duration");

        /// <summary>通知次数。累计发送报警通知的次数</summary>
        public static readonly Field Times = FindByName("Times");

        /// <summary>内容。报警详细信息</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>创建者。服务端节点</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得报警记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>告警组</summary>
        public const String GroupId = "GroupId";

        /// <summary>类别。应用下线、节点下线、错误数过高等</summary>
        public const String Category = "Category";

        /// <summary>操作。报警对象，如应用名或节点名</summary>
        public const String Action = "Action";

        /// <summary>状态。报警中、已恢复</summary>
        public const String Status = "Status";

        /// <summary>开始时间。报警首次触发的时间</summary>
        public const String StartTime = "StartTime";

        /// <summary>结束时间。报警恢复的时间</summary>
        public const String EndTime = "EndTime";

        /// <summary>持续时间。报警持续的秒数</summary>
        public const String Duration = "Duration";

        /// <summary>通知次数。累计发送报警通知的次数</summary>
        public const String Times = "Times";

        /// <summary>内容。报警详细信息</summary>
        public const String Content = "Content";

        /// <summary>创建者。服务端节点</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
