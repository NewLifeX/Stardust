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

/// <summary>流水线。流水线配置，关联一个应用部署集与一个编译节点，监听指定分支的 webhook 触发，自动完成代码拉取、编译、上传、部署的全流程</summary>
[Serializable]
[DataObject]
[Description("流水线。流水线配置，关联一个应用部署集与一个编译节点，监听指定分支的 webhook 触发，自动完成代码拉取、编译、上传、部署的全流程")]
[BindIndex("IU_AppPipeline_Name", true, "Name")]
[BindIndex("IX_AppPipeline_DeployId", false, "DeployId")]
[BindTable("AppPipeline", Description = "流水线。流水线配置，关联一个应用部署集与一个编译节点，监听指定分支的 webhook 触发，自动完成代码拉取、编译、上传、部署的全流程", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppPipeline
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称。流水线名称，全局唯一</summary>
    [DisplayName("名称")]
    [Description("名称。流水线名称，全局唯一")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称。流水线名称，全局唯一", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _DeployId;
    /// <summary>应用部署集。对应AppDeploy</summary>
    [DisplayName("应用部署集")]
    [Description("应用部署集。对应AppDeploy")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用部署集。对应AppDeploy", "")]
    public Int32 DeployId { get => _DeployId; set { if (OnPropertyChanging("DeployId", value)) { _DeployId = value; OnPropertyChanged("DeployId"); } } }

    private Int32 _ProjectId;
    /// <summary>项目。资源归属的团队项目</summary>
    [DisplayName("项目")]
    [Description("项目。资源归属的团队项目")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。资源归属的团队项目", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Branch;
    /// <summary>分支。触发的分支，支持精确匹配与前缀通配符，例如 release/*</summary>
    [DisplayName("分支")]
    [Description("分支。触发的分支，支持精确匹配与前缀通配符，例如 release/*")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Branch", "分支。触发的分支，支持精确匹配与前缀通配符，例如 release/*", "")]
    public String Branch { get => _Branch; set { if (OnPropertyChanging("Branch", value)) { _Branch = value; OnPropertyChanged("Branch"); } } }

    private String _Token;
    /// <summary>Token。webhook 鉴权 token，由系统自动生成</summary>
    [DisplayName("Token")]
    [Description("Token。webhook 鉴权 token，由系统自动生成")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Token", "Token。webhook 鉴权 token，由系统自动生成", "")]
    public String Token { get => _Token; set { if (OnPropertyChanging("Token", value)) { _Token = value; OnPropertyChanged("Token"); } } }

    private String _Secret;
    /// <summary>Secret。HMAC 签名密钥，可选，用于校验 webhook 来源</summary>
    [DisplayName("Secret")]
    [Description("Secret。HMAC 签名密钥，可选，用于校验 webhook 来源")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Secret", "Secret。HMAC 签名密钥，可选，用于校验 webhook 来源", "")]
    public String Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

    private Int32 _BuildNodeId;
    /// <summary>编译节点。对应Node.Id</summary>
    [DisplayName("编译节点")]
    [Description("编译节点。对应Node.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BuildNodeId", "编译节点。对应Node.Id", "")]
    public Int32 BuildNodeId { get => _BuildNodeId; set { if (OnPropertyChanging("BuildNodeId", value)) { _BuildNodeId = value; OnPropertyChanged("BuildNodeId"); } } }

    private String _DeployNodeIds;
    /// <summary>部署节点。部署节点 ID 列表，对应AppDeployNode.Id，多个分号分隔</summary>
    [DisplayName("部署节点")]
    [Description("部署节点。部署节点 ID 列表，对应AppDeployNode.Id，多个分号分隔")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("DeployNodeIds", "部署节点。部署节点 ID 列表，对应AppDeployNode.Id，多个分号分隔", "", ItemType = "multipleSelect")]
    public String DeployNodeIds { get => _DeployNodeIds; set { if (OnPropertyChanging("DeployNodeIds", value)) { _DeployNodeIds = value; OnPropertyChanged("DeployNodeIds"); } } }

    private Boolean _AutoDeploy;
    /// <summary>自动部署。编译成功后是否自动 install 到部署节点</summary>
    [DisplayName("自动部署")]
    [Description("自动部署。编译成功后是否自动 install 到部署节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AutoDeploy", "自动部署。编译成功后是否自动 install 到部署节点", "")]
    public Boolean AutoDeploy { get => _AutoDeploy; set { if (OnPropertyChanging("AutoDeploy", value)) { _AutoDeploy = value; OnPropertyChanged("AutoDeploy"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
            "Name" => _Name,
            "DeployId" => _DeployId,
            "ProjectId" => _ProjectId,
            "Branch" => _Branch,
            "Token" => _Token,
            "Secret" => _Secret,
            "BuildNodeId" => _BuildNodeId,
            "DeployNodeIds" => _DeployNodeIds,
            "AutoDeploy" => _AutoDeploy,
            "Enable" => _Enable,
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
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DeployId": _DeployId = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Branch": _Branch = Convert.ToString(value); break;
                case "Token": _Token = Convert.ToString(value); break;
                case "Secret": _Secret = Convert.ToString(value); break;
                case "BuildNodeId": _BuildNodeId = value.ToInt(); break;
                case "DeployNodeIds": _DeployNodeIds = Convert.ToString(value); break;
                case "AutoDeploy": _AutoDeploy = value.ToBoolean(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
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
    /// <summary>应用部署集</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AppDeploy Deploy => Extends.Get(nameof(Deploy), k => AppDeploy.FindById(DeployId));

    /// <summary>应用部署集</summary>
    [Map(nameof(DeployId), typeof(AppDeploy), "Id")]
    public String DeployName => Deploy?.ToString();

    /// <summary>项目</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Platform.GalaxyProject Project => Extends.Get(nameof(Project), k => Stardust.Data.Platform.GalaxyProject.FindById(ProjectId));

    /// <summary>项目</summary>
    [Map(nameof(ProjectId), typeof(Stardust.Data.Platform.GalaxyProject), "Id")]
    public String ProjectName => Project?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppPipeline FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static AppPipeline FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as AppPipeline;

        //return Find(_.Name == name);
    }

    /// <summary>根据应用部署集查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipeline> FindAllByDeployId(Int32 deployId)
    {
        if (deployId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.DeployId == deployId);

        return FindAll(_.DeployId == deployId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deployId">应用部署集。对应AppDeploy</param>
    /// <param name="projectId">项目。资源归属的团队项目</param>
    /// <param name="autoDeploy">自动部署。编译成功后是否自动 install 到部署节点</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppPipeline> Search(Int32 deployId, Int32 projectId, Boolean? autoDeploy, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deployId >= 0) exp &= _.DeployId == deployId;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (autoDeploy != null) exp &= _.AutoDeploy == autoDeploy;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得流水线字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称。流水线名称，全局唯一</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>应用部署集。对应AppDeploy</summary>
        public static readonly Field DeployId = FindByName("DeployId");

        /// <summary>项目。资源归属的团队项目</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>分支。触发的分支，支持精确匹配与前缀通配符，例如 release/*</summary>
        public static readonly Field Branch = FindByName("Branch");

        /// <summary>Token。webhook 鉴权 token，由系统自动生成</summary>
        public static readonly Field Token = FindByName("Token");

        /// <summary>Secret。HMAC 签名密钥，可选，用于校验 webhook 来源</summary>
        public static readonly Field Secret = FindByName("Secret");

        /// <summary>编译节点。对应Node.Id</summary>
        public static readonly Field BuildNodeId = FindByName("BuildNodeId");

        /// <summary>部署节点。部署节点 ID 列表，对应AppDeployNode.Id，多个分号分隔</summary>
        public static readonly Field DeployNodeIds = FindByName("DeployNodeIds");

        /// <summary>自动部署。编译成功后是否自动 install 到部署节点</summary>
        public static readonly Field AutoDeploy = FindByName("AutoDeploy");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

    /// <summary>取得流水线字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称。流水线名称，全局唯一</summary>
        public const String Name = "Name";

        /// <summary>应用部署集。对应AppDeploy</summary>
        public const String DeployId = "DeployId";

        /// <summary>项目。资源归属的团队项目</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>分支。触发的分支，支持精确匹配与前缀通配符，例如 release/*</summary>
        public const String Branch = "Branch";

        /// <summary>Token。webhook 鉴权 token，由系统自动生成</summary>
        public const String Token = "Token";

        /// <summary>Secret。HMAC 签名密钥，可选，用于校验 webhook 来源</summary>
        public const String Secret = "Secret";

        /// <summary>编译节点。对应Node.Id</summary>
        public const String BuildNodeId = "BuildNodeId";

        /// <summary>部署节点。部署节点 ID 列表，对应AppDeployNode.Id，多个分号分隔</summary>
        public const String DeployNodeIds = "DeployNodeIds";

        /// <summary>自动部署。编译成功后是否自动 install 到部署节点</summary>
        public const String AutoDeploy = "AutoDeploy";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
