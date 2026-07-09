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

namespace Stardust.Data.Deployment;

/// <summary>流水线步骤。一次流水线运行中的单个步骤（Build/Upload/Deploy），记录每一步的执行节点、状态与消息</summary>
[Serializable]
[DataObject]
[Description("流水线步骤。一次流水线运行中的单个步骤（Build/Upload/Deploy），记录每一步的执行节点、状态与消息")]
[BindIndex("IX_AppPipelineStep_RunId_StepIndex", false, "RunId,StepIndex")]
[BindTable("AppPipelineStep", Description = "流水线步骤。一次流水线运行中的单个步骤（Build/Upload/Deploy），记录每一步的执行节点、状态与消息", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppPipelineStep
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _RunId;
    /// <summary>运行。对应AppPipelineRun.Id</summary>
    [DisplayName("运行")]
    [Description("运行。对应AppPipelineRun.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RunId", "运行。对应AppPipelineRun.Id", "")]
    public Int64 RunId { get => _RunId; set { if (OnPropertyChanging("RunId", value)) { _RunId = value; OnPropertyChanged("RunId"); } } }

    private String _StepType;
    /// <summary>步骤类型。Build/Upload/Deploy</summary>
    [DisplayName("步骤类型")]
    [Description("步骤类型。Build/Upload/Deploy")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("StepType", "步骤类型。Build/Upload/Deploy", "")]
    public String StepType { get => _StepType; set { if (OnPropertyChanging("StepType", value)) { _StepType = value; OnPropertyChanged("StepType"); } } }

    private Int32 _StepIndex;
    /// <summary>步骤序号。同一运行内从 0 开始递增</summary>
    [DisplayName("步骤序号")]
    [Description("步骤序号。同一运行内从 0 开始递增")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StepIndex", "步骤序号。同一运行内从 0 开始递增", "")]
    public Int32 StepIndex { get => _StepIndex; set { if (OnPropertyChanging("StepIndex", value)) { _StepIndex = value; OnPropertyChanged("StepIndex"); } } }

    private Int32 _NodeId;
    /// <summary>节点。执行该步骤的节点</summary>
    [DisplayName("节点")]
    [Description("节点。执行该步骤的节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。执行该步骤的节点", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private String _Status;
    /// <summary>状态。Pending/Running/Success/Failed/Skipped</summary>
    [DisplayName("状态")]
    [Description("状态。Pending/Running/Success/Failed/Skipped")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Status", "状态。Pending/Running/Success/Failed/Skipped", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _Message;
    /// <summary>消息。步骤执行输出与失败原因</summary>
    [DisplayName("消息")]
    [Description("消息。步骤执行输出与失败原因")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Message", "消息。步骤执行输出与失败原因", "")]
    public String Message { get => _Message; set { if (OnPropertyChanging("Message", value)) { _Message = value; OnPropertyChanged("Message"); } } }

    private DateTime _StartedTime;
    /// <summary>开始</summary>
    [DisplayName("开始")]
    [Description("开始")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartedTime", "开始", "")]
    public DateTime StartedTime { get => _StartedTime; set { if (OnPropertyChanging("StartedTime", value)) { _StartedTime = value; OnPropertyChanged("StartedTime"); } } }

    private DateTime _FinishedTime;
    /// <summary>完成</summary>
    [DisplayName("完成")]
    [Description("完成")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("FinishedTime", "完成", "")]
    public DateTime FinishedTime { get => _FinishedTime; set { if (OnPropertyChanging("FinishedTime", value)) { _FinishedTime = value; OnPropertyChanged("FinishedTime"); } } }

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
        get => name switch
        {
            "Id" => _Id,
            "RunId" => _RunId,
            "StepType" => _StepType,
            "StepIndex" => _StepIndex,
            "NodeId" => _NodeId,
            "Status" => _Status,
            "Message" => _Message,
            "StartedTime" => _StartedTime,
            "FinishedTime" => _FinishedTime,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "RunId": _RunId = value.ToLong(); break;
                case "StepType": _StepType = Convert.ToString(value); break;
                case "StepIndex": _StepIndex = value.ToInt(); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "Message": _Message = Convert.ToString(value); break;
                case "StartedTime": _StartedTime = value.ToDateTime(); break;
                case "FinishedTime": _FinishedTime = value.ToDateTime(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppPipelineStep FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据运行、步骤序号查找</summary>
    /// <param name="runId">运行</param>
    /// <param name="stepIndex">步骤序号</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipelineStep> FindAllByRunIdAndStepIndex(Int64 runId, Int32 stepIndex)
    {
        if (runId < 0) return [];
        if (stepIndex < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.RunId == runId && e.StepIndex == stepIndex);

        return FindAll(_.RunId == runId & _.StepIndex == stepIndex);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="runId">运行。对应AppPipelineRun.Id</param>
    /// <param name="stepIndex">步骤序号。同一运行内从 0 开始递增</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipelineStep> Search(Int64 runId, Int32 stepIndex, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (runId >= 0) exp &= _.RunId == runId;
        if (stepIndex >= 0) exp &= _.StepIndex == stepIndex;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得流水线步骤字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>运行。对应AppPipelineRun.Id</summary>
        public static readonly Field RunId = FindByName("RunId");

        /// <summary>步骤类型。Build/Upload/Deploy</summary>
        public static readonly Field StepType = FindByName("StepType");

        /// <summary>步骤序号。同一运行内从 0 开始递增</summary>
        public static readonly Field StepIndex = FindByName("StepIndex");

        /// <summary>节点。执行该步骤的节点</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>状态。Pending/Running/Success/Failed/Skipped</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>消息。步骤执行输出与失败原因</summary>
        public static readonly Field Message = FindByName("Message");

        /// <summary>开始</summary>
        public static readonly Field StartedTime = FindByName("StartedTime");

        /// <summary>完成</summary>
        public static readonly Field FinishedTime = FindByName("FinishedTime");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得流水线步骤字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>运行。对应AppPipelineRun.Id</summary>
        public const String RunId = "RunId";

        /// <summary>步骤类型。Build/Upload/Deploy</summary>
        public const String StepType = "StepType";

        /// <summary>步骤序号。同一运行内从 0 开始递增</summary>
        public const String StepIndex = "StepIndex";

        /// <summary>节点。执行该步骤的节点</summary>
        public const String NodeId = "NodeId";

        /// <summary>状态。Pending/Running/Success/Failed/Skipped</summary>
        public const String Status = "Status";

        /// <summary>消息。步骤执行输出与失败原因</summary>
        public const String Message = "Message";

        /// <summary>开始</summary>
        public const String StartedTime = "StartedTime";

        /// <summary>完成</summary>
        public const String FinishedTime = "FinishedTime";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
