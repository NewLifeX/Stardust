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

/// <summary>流水线运行。流水线的一次运行实例，记录触发来源、提交信息、编译与部署时间线及状态流转</summary>
[Serializable]
[DataObject]
[Description("流水线运行。流水线的一次运行实例，记录触发来源、提交信息、编译与部署时间线及状态流转")]
[BindIndex("IX_AppPipelineRun_PipelineId_Id", false, "PipelineId,Id")]
[BindIndex("IX_AppPipelineRun_Status_Id", false, "Status,Id")]
[BindTable("AppPipelineRun", Description = "流水线运行。流水线的一次运行实例，记录触发来源、提交信息、编译与部署时间线及状态流转", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppPipelineRun
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _PipelineId;
    /// <summary>流水线。对应AppPipeline.Id</summary>
    [DisplayName("流水线")]
    [Description("流水线。对应AppPipeline.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PipelineId", "流水线。对应AppPipeline.Id", "")]
    public Int32 PipelineId { get => _PipelineId; set { if (OnPropertyChanging("PipelineId", value)) { _PipelineId = value; OnPropertyChanged("PipelineId"); } } }

    private Stardust.Models.PipelineStatus _Status;
    /// <summary>状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled</summary>
    [DisplayName("状态")]
    [Description("状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled", "")]
    public Stardust.Models.PipelineStatus Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private String _TriggerSource;
    /// <summary>触发来源。webhook/manual/api</summary>
    [DisplayName("触发来源")]
    [Description("触发来源。webhook/manual/api")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TriggerSource", "触发来源。webhook/manual/api", "")]
    public String TriggerSource { get => _TriggerSource; set { if (OnPropertyChanging("TriggerSource", value)) { _TriggerSource = value; OnPropertyChanged("TriggerSource"); } } }

    private String _CommitId;
    /// <summary>提交SHA。触发本次运行的代码提交哈希</summary>
    [DisplayName("提交SHA")]
    [Description("提交SHA。触发本次运行的代码提交哈希")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CommitId", "提交SHA。触发本次运行的代码提交哈希", "")]
    public String CommitId { get => _CommitId; set { if (OnPropertyChanging("CommitId", value)) { _CommitId = value; OnPropertyChanged("CommitId"); } } }

    private String _CommitMessage;
    /// <summary>提交信息</summary>
    [DisplayName("提交信息")]
    [Description("提交信息")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("CommitMessage", "提交信息", "")]
    public String CommitMessage { get => _CommitMessage; set { if (OnPropertyChanging("CommitMessage", value)) { _CommitMessage = value; OnPropertyChanged("CommitMessage"); } } }

    private String _CommitAuthor;
    /// <summary>提交人</summary>
    [DisplayName("提交人")]
    [Description("提交人")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("CommitAuthor", "提交人", "")]
    public String CommitAuthor { get => _CommitAuthor; set { if (OnPropertyChanging("CommitAuthor", value)) { _CommitAuthor = value; OnPropertyChanged("CommitAuthor"); } } }

    private DateTime _CommitTime;
    /// <summary>提交时间</summary>
    [DisplayName("提交时间")]
    [Description("提交时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CommitTime", "提交时间", "")]
    public DateTime CommitTime { get => _CommitTime; set { if (OnPropertyChanging("CommitTime", value)) { _CommitTime = value; OnPropertyChanged("CommitTime"); } } }

    private String _Branch;
    /// <summary>分支</summary>
    [DisplayName("分支")]
    [Description("分支")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Branch", "分支", "")]
    public String Branch { get => _Branch; set { if (OnPropertyChanging("Branch", value)) { _Branch = value; OnPropertyChanged("Branch"); } } }

    private Int32 _BuildNodeId;
    /// <summary>编译节点</summary>
    [DisplayName("编译节点")]
    [Description("编译节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BuildNodeId", "编译节点", "")]
    public Int32 BuildNodeId { get => _BuildNodeId; set { if (OnPropertyChanging("BuildNodeId", value)) { _BuildNodeId = value; OnPropertyChanged("BuildNodeId"); } } }

    private Int32 _AppVersionId;
    /// <summary>产物版本。编译产物对应的部署版本AppDeployVersion.Id</summary>
    [DisplayName("产物版本")]
    [Description("产物版本。编译产物对应的部署版本AppDeployVersion.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppVersionId", "产物版本。编译产物对应的部署版本AppDeployVersion.Id", "")]
    public Int32 AppVersionId { get => _AppVersionId; set { if (OnPropertyChanging("AppVersionId", value)) { _AppVersionId = value; OnPropertyChanged("AppVersionId"); } } }

    private DateTime _BuildStartedTime;
    /// <summary>编译开始</summary>
    [DisplayName("编译开始")]
    [Description("编译开始")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("BuildStartedTime", "编译开始", "")]
    public DateTime BuildStartedTime { get => _BuildStartedTime; set { if (OnPropertyChanging("BuildStartedTime", value)) { _BuildStartedTime = value; OnPropertyChanged("BuildStartedTime"); } } }

    private DateTime _BuildFinishedTime;
    /// <summary>编译完成</summary>
    [DisplayName("编译完成")]
    [Description("编译完成")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("BuildFinishedTime", "编译完成", "")]
    public DateTime BuildFinishedTime { get => _BuildFinishedTime; set { if (OnPropertyChanging("BuildFinishedTime", value)) { _BuildFinishedTime = value; OnPropertyChanged("BuildFinishedTime"); } } }

    private DateTime _DeployStartedTime;
    /// <summary>部署开始</summary>
    [DisplayName("部署开始")]
    [Description("部署开始")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DeployStartedTime", "部署开始", "")]
    public DateTime DeployStartedTime { get => _DeployStartedTime; set { if (OnPropertyChanging("DeployStartedTime", value)) { _DeployStartedTime = value; OnPropertyChanged("DeployStartedTime"); } } }

    private DateTime _DeployFinishedTime;
    /// <summary>部署完成</summary>
    [DisplayName("部署完成")]
    [Description("部署完成")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("DeployFinishedTime", "部署完成", "")]
    public DateTime DeployFinishedTime { get => _DeployFinishedTime; set { if (OnPropertyChanging("DeployFinishedTime", value)) { _DeployFinishedTime = value; OnPropertyChanged("DeployFinishedTime"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者", "")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private Int32 _UpdateUserId;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "更新者", "")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

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
            "Id" => _Id,
            "PipelineId" => _PipelineId,
            "Status" => _Status,
            "TriggerSource" => _TriggerSource,
            "CommitId" => _CommitId,
            "CommitMessage" => _CommitMessage,
            "CommitAuthor" => _CommitAuthor,
            "CommitTime" => _CommitTime,
            "Branch" => _Branch,
            "BuildNodeId" => _BuildNodeId,
            "AppVersionId" => _AppVersionId,
            "BuildStartedTime" => _BuildStartedTime,
            "BuildFinishedTime" => _BuildFinishedTime,
            "DeployStartedTime" => _DeployStartedTime,
            "DeployFinishedTime" => _DeployFinishedTime,
            "TraceId" => _TraceId,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserId" => _UpdateUserId,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "PipelineId": _PipelineId = value.ToInt(); break;
                case "Status": _Status = (Stardust.Models.PipelineStatus)value.ToInt(); break;
                case "TriggerSource": _TriggerSource = Convert.ToString(value); break;
                case "CommitId": _CommitId = Convert.ToString(value); break;
                case "CommitMessage": _CommitMessage = Convert.ToString(value); break;
                case "CommitAuthor": _CommitAuthor = Convert.ToString(value); break;
                case "CommitTime": _CommitTime = value.ToDateTime(); break;
                case "Branch": _Branch = Convert.ToString(value); break;
                case "BuildNodeId": _BuildNodeId = value.ToInt(); break;
                case "AppVersionId": _AppVersionId = value.ToInt(); break;
                case "BuildStartedTime": _BuildStartedTime = value.ToDateTime(); break;
                case "BuildFinishedTime": _BuildFinishedTime = value.ToDateTime(); break;
                case "DeployStartedTime": _DeployStartedTime = value.ToDateTime(); break;
                case "DeployFinishedTime": _DeployFinishedTime = value.ToDateTime(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
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
    public static AppPipelineRun FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据流水线查找</summary>
    /// <param name="pipelineId">流水线</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipelineRun> FindAllByPipelineId(Int32 pipelineId)
    {
        if (pipelineId < 0) return [];

        return FindAll(_.PipelineId == pipelineId);
    }

    /// <summary>根据状态查找</summary>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipelineRun> FindAllByStatus(Stardust.Models.PipelineStatus status)
    {
        if (status < 0) return [];

        return FindAll(_.Status == status);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="pipelineId">流水线。对应AppPipeline.Id</param>
    /// <param name="status">状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipelineRun> Search(Int32 pipelineId, Stardust.Models.PipelineStatus status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (pipelineId >= 0) exp &= _.PipelineId == pipelineId;
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
    /// <summary>取得流水线运行字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>流水线。对应AppPipeline.Id</summary>
        public static readonly Field PipelineId = FindByName("PipelineId");

        /// <summary>状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>触发来源。webhook/manual/api</summary>
        public static readonly Field TriggerSource = FindByName("TriggerSource");

        /// <summary>提交SHA。触发本次运行的代码提交哈希</summary>
        public static readonly Field CommitId = FindByName("CommitId");

        /// <summary>提交信息</summary>
        public static readonly Field CommitMessage = FindByName("CommitMessage");

        /// <summary>提交人</summary>
        public static readonly Field CommitAuthor = FindByName("CommitAuthor");

        /// <summary>提交时间</summary>
        public static readonly Field CommitTime = FindByName("CommitTime");

        /// <summary>分支</summary>
        public static readonly Field Branch = FindByName("Branch");

        /// <summary>编译节点</summary>
        public static readonly Field BuildNodeId = FindByName("BuildNodeId");

        /// <summary>产物版本。编译产物对应的部署版本AppDeployVersion.Id</summary>
        public static readonly Field AppVersionId = FindByName("AppVersionId");

        /// <summary>编译开始</summary>
        public static readonly Field BuildStartedTime = FindByName("BuildStartedTime");

        /// <summary>编译完成</summary>
        public static readonly Field BuildFinishedTime = FindByName("BuildFinishedTime");

        /// <summary>部署开始</summary>
        public static readonly Field DeployStartedTime = FindByName("DeployStartedTime");

        /// <summary>部署完成</summary>
        public static readonly Field DeployFinishedTime = FindByName("DeployFinishedTime");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得流水线运行字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>流水线。对应AppPipeline.Id</summary>
        public const String PipelineId = "PipelineId";

        /// <summary>状态。Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled</summary>
        public const String Status = "Status";

        /// <summary>触发来源。webhook/manual/api</summary>
        public const String TriggerSource = "TriggerSource";

        /// <summary>提交SHA。触发本次运行的代码提交哈希</summary>
        public const String CommitId = "CommitId";

        /// <summary>提交信息</summary>
        public const String CommitMessage = "CommitMessage";

        /// <summary>提交人</summary>
        public const String CommitAuthor = "CommitAuthor";

        /// <summary>提交时间</summary>
        public const String CommitTime = "CommitTime";

        /// <summary>分支</summary>
        public const String Branch = "Branch";

        /// <summary>编译节点</summary>
        public const String BuildNodeId = "BuildNodeId";

        /// <summary>产物版本。编译产物对应的部署版本AppDeployVersion.Id</summary>
        public const String AppVersionId = "AppVersionId";

        /// <summary>编译开始</summary>
        public const String BuildStartedTime = "BuildStartedTime";

        /// <summary>编译完成</summary>
        public const String BuildFinishedTime = "BuildFinishedTime";

        /// <summary>部署开始</summary>
        public const String DeployStartedTime = "DeployStartedTime";

        /// <summary>部署完成</summary>
        public const String DeployFinishedTime = "DeployFinishedTime";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建者</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
