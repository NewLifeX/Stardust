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

/// <summary>编译节点。应用部署集和编译节点的关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包</summary>
[Serializable]
[DataObject]
[Description("编译节点。应用部署集和编译节点的关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包")]
[BindIndex("IX_AppBuildNode_DeployId", false, "DeployId")]
[BindIndex("IX_AppBuildNode_NodeId", false, "NodeId")]
[BindTable("AppBuildNode", Description = "编译节点。应用部署集和编译节点的关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppBuildNode
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _DeployId;
    /// <summary>应用部署集。对应AppDeploy</summary>
    [DisplayName("应用部署集")]
    [Description("应用部署集。对应AppDeploy")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用部署集。对应AppDeploy", "")]
    public Int32 DeployId { get => _DeployId; set { if (OnPropertyChanging("DeployId", value)) { _DeployId = value; OnPropertyChanged("DeployId"); } } }

    private Int32 _NodeId;
    /// <summary>节点。节点服务器</summary>
    [DisplayName("节点")]
    [Description("节点。节点服务器")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。节点服务器", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _SourcePath;
    /// <summary>源代码目录</summary>
    [DisplayName("源代码目录")]
    [Description("源代码目录")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SourcePath", "源代码目录", "")]
    public String SourcePath { get => _SourcePath; set { if (OnPropertyChanging("SourcePath", value)) { _SourcePath = value; OnPropertyChanged("SourcePath"); } } }

    private Boolean _PullCode;
    /// <summary>拉取源代码</summary>
    [DisplayName("拉取源代码")]
    [Description("拉取源代码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PullCode", "拉取源代码", "")]
    public Boolean PullCode { get => _PullCode; set { if (OnPropertyChanging("PullCode", value)) { _PullCode = value; OnPropertyChanged("PullCode"); } } }

    private Boolean _BuildProject;
    /// <summary>编译项目</summary>
    [DisplayName("编译项目")]
    [Description("编译项目")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BuildProject", "编译项目", "")]
    public Boolean BuildProject { get => _BuildProject; set { if (OnPropertyChanging("BuildProject", value)) { _BuildProject = value; OnPropertyChanged("BuildProject"); } } }

    private Boolean _PackageOutput;
    /// <summary>打包输出</summary>
    [DisplayName("打包输出")]
    [Description("打包输出")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PackageOutput", "打包输出", "")]
    public Boolean PackageOutput { get => _PackageOutput; set { if (OnPropertyChanging("PackageOutput", value)) { _PackageOutput = value; OnPropertyChanged("PackageOutput"); } } }

    private Boolean _UploadPackage;
    /// <summary>上传应用包</summary>
    [DisplayName("上传应用包")]
    [Description("上传应用包")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UploadPackage", "上传应用包", "")]
    public Boolean UploadPackage { get => _UploadPackage; set { if (OnPropertyChanging("UploadPackage", value)) { _UploadPackage = value; OnPropertyChanged("UploadPackage"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建人</summary>
    [Category("扩展")]
    [DisplayName("创建人")]
    [Description("创建人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建人", "")]
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
            "DeployId" => _DeployId,
            "NodeId" => _NodeId,
            "Enable" => _Enable,
            "SourcePath" => _SourcePath,
            "PullCode" => _PullCode,
            "BuildProject" => _BuildProject,
            "PackageOutput" => _PackageOutput,
            "UploadPackage" => _UploadPackage,
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
                case "DeployId": _DeployId = value.ToInt(); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "SourcePath": _SourcePath = Convert.ToString(value); break;
                case "PullCode": _PullCode = value.ToBoolean(); break;
                case "BuildProject": _BuildProject = value.ToBoolean(); break;
                case "PackageOutput": _PackageOutput = value.ToBoolean(); break;
                case "UploadPackage": _UploadPackage = value.ToBoolean(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppBuildNode FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用部署集查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <returns>实体列表</returns>
    public static IList<AppBuildNode> FindAllByDeployId(Int32 deployId)
    {
        if (deployId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId);

        return FindAll(_.DeployId == deployId);
    }

    /// <summary>根据节点查找</summary>
    /// <param name="nodeId">节点</param>
    /// <returns>实体列表</returns>
    public static IList<AppBuildNode> FindAllByNodeId(Int32 nodeId)
    {
        if (nodeId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeId == nodeId);

        return FindAll(_.NodeId == nodeId);
    }
    #endregion

    #region 字段名
    /// <summary>取得编译节点字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用部署集。对应AppDeploy</summary>
        public static readonly Field DeployId = FindByName("DeployId");

        /// <summary>节点。节点服务器</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>源代码目录</summary>
        public static readonly Field SourcePath = FindByName("SourcePath");

        /// <summary>拉取源代码</summary>
        public static readonly Field PullCode = FindByName("PullCode");

        /// <summary>编译项目</summary>
        public static readonly Field BuildProject = FindByName("BuildProject");

        /// <summary>打包输出</summary>
        public static readonly Field PackageOutput = FindByName("PackageOutput");

        /// <summary>上传应用包</summary>
        public static readonly Field UploadPackage = FindByName("UploadPackage");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建人</summary>
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

    /// <summary>取得编译节点字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用部署集。对应AppDeploy</summary>
        public const String DeployId = "DeployId";

        /// <summary>节点。节点服务器</summary>
        public const String NodeId = "NodeId";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>源代码目录</summary>
        public const String SourcePath = "SourcePath";

        /// <summary>拉取源代码</summary>
        public const String PullCode = "PullCode";

        /// <summary>编译项目</summary>
        public const String BuildProject = "BuildProject";

        /// <summary>打包输出</summary>
        public const String PackageOutput = "PackageOutput";

        /// <summary>上传应用包</summary>
        public const String UploadPackage = "UploadPackage";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建人</summary>
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
