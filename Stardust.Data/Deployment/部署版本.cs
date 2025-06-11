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

/// <summary>部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本，来自上传或自动编译</summary>
[Serializable]
[DataObject]
[Description("部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本，来自上传或自动编译")]
[BindIndex("IU_AppDeployVersion_DeployId_Version", true, "DeployId,Version")]
[BindTable("AppDeployVersion", Description = "部署版本。应用的多个可发布版本，主要管理应用程序包，支持随时切换使用不同版本，来自上传或自动编译", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppDeployVersion
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

    private String _Version;
    /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
    [DisplayName("版本")]
    [Description("版本。如2.3.2022.0911，字符串比较大小")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Version", "版本。如2.3.2022.0911，字符串比较大小", "", Master = true)]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _Url;
    /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
    [DisplayName("资源地址")]
    [Description("资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Url", "资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖", "", ItemType = "file")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private String _Overwrite;
    /// <summary>覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝</summary>
    [DisplayName("覆盖文件")]
    [Description("覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Overwrite", "覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝", "")]
    public String Overwrite { get => _Overwrite; set { if (OnPropertyChanging("Overwrite", value)) { _Overwrite = value; OnPropertyChanged("Overwrite"); } } }

    private Stardust.Models.DeployModes _Mode;
    /// <summary>发布模式。1部分包，仅覆盖；2标准包，清空可执行文件再覆盖；3完整包，清空所有文件</summary>
    [DisplayName("发布模式")]
    [Description("发布模式。1部分包，仅覆盖；2标准包，清空可执行文件再覆盖；3完整包，清空所有文件")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Mode", "发布模式。1部分包，仅覆盖；2标准包，清空可执行文件再覆盖；3完整包，清空所有文件", "")]
    public Stardust.Models.DeployModes Mode { get => _Mode; set { if (OnPropertyChanging("Mode", value)) { _Mode = value; OnPropertyChanged("Mode"); } } }

    private Int64 _Size;
    /// <summary>文件大小</summary>
    [DisplayName("文件大小")]
    [Description("文件大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
    public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

    private String _Hash;
    /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
    [DisplayName("文件哈希")]
    [Description("文件哈希。MD5散列，避免下载的文件有缺失")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Hash", "文件哈希。MD5散列，避免下载的文件有缺失", "")]
    public String Hash { get => _Hash; set { if (OnPropertyChanging("Hash", value)) { _Hash = value; OnPropertyChanged("Hash"); } } }

    private Stardust.Models.RuntimeIdentifier _Runtime;
    /// <summary>运行时。RID是运行时标识符，用于标识应用程序运行所在的目标平台。如win-x64/linux-arm</summary>
    [DisplayName("运行时")]
    [Description("运行时。RID是运行时标识符，用于标识应用程序运行所在的目标平台。如win-x64/linux-arm")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Runtime", "运行时。RID是运行时标识符，用于标识应用程序运行所在的目标平台。如win-x64/linux-arm", "")]
    public Stardust.Models.RuntimeIdentifier Runtime { get => _Runtime; set { if (OnPropertyChanging("Runtime", value)) { _Runtime = value; OnPropertyChanged("Runtime"); } } }

    private String _TargetFramework;
    /// <summary>目标框架。TFM目标运行时框架，如net8.0</summary>
    [DisplayName("目标框架")]
    [Description("目标框架。TFM目标运行时框架，如net8.0")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TargetFramework", "目标框架。TFM目标运行时框架，如net8.0", "")]
    public String TargetFramework { get => _TargetFramework; set { if (OnPropertyChanging("TargetFramework", value)) { _TargetFramework = value; OnPropertyChanged("TargetFramework"); } } }

    private String _Progress;
    /// <summary>进度。发布进度</summary>
    [DisplayName("进度")]
    [Description("进度。发布进度")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Progress", "进度。发布进度", "")]
    public String Progress { get => _Progress; set { if (OnPropertyChanging("Progress", value)) { _Progress = value; OnPropertyChanged("Progress"); } } }

    private String _CommitId;
    /// <summary>提交标识</summary>
    [Category("编译参数")]
    [DisplayName("提交标识")]
    [Description("提交标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CommitId", "提交标识", "")]
    public String CommitId { get => _CommitId; set { if (OnPropertyChanging("CommitId", value)) { _CommitId = value; OnPropertyChanged("CommitId"); } } }

    private String _CommitLog;
    /// <summary>提交记录</summary>
    [Category("编译参数")]
    [DisplayName("提交记录")]
    [Description("提交记录")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CommitLog", "提交记录", "")]
    public String CommitLog { get => _CommitLog; set { if (OnPropertyChanging("CommitLog", value)) { _CommitLog = value; OnPropertyChanged("CommitLog"); } } }

    private DateTime _CommitTime;
    /// <summary>提交时间</summary>
    [Category("编译参数")]
    [DisplayName("提交时间")]
    [Description("提交时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CommitTime", "提交时间", "")]
    public DateTime CommitTime { get => _CommitTime; set { if (OnPropertyChanging("CommitTime", value)) { _CommitTime = value; OnPropertyChanged("CommitTime"); } } }

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
            "DeployId" => _DeployId,
            "Version" => _Version,
            "Enable" => _Enable,
            "Url" => _Url,
            "Overwrite" => _Overwrite,
            "Mode" => _Mode,
            "Size" => _Size,
            "Hash" => _Hash,
            "Runtime" => _Runtime,
            "TargetFramework" => _TargetFramework,
            "Progress" => _Progress,
            "CommitId" => _CommitId,
            "CommitLog" => _CommitLog,
            "CommitTime" => _CommitTime,
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
                case "Version": _Version = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Url": _Url = Convert.ToString(value); break;
                case "Overwrite": _Overwrite = Convert.ToString(value); break;
                case "Mode": _Mode = (Stardust.Models.DeployModes)value.ToInt(); break;
                case "Size": _Size = value.ToLong(); break;
                case "Hash": _Hash = Convert.ToString(value); break;
                case "Runtime": _Runtime = (Stardust.Models.RuntimeIdentifier)value.ToInt(); break;
                case "TargetFramework": _TargetFramework = Convert.ToString(value); break;
                case "Progress": _Progress = Convert.ToString(value); break;
                case "CommitId": _CommitId = Convert.ToString(value); break;
                case "CommitLog": _CommitLog = Convert.ToString(value); break;
                case "CommitTime": _CommitTime = value.ToDateTime(); break;
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
    /// <summary>根据应用部署集查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployVersion> FindAllByDeployId(Int32 deployId)
    {
        if (deployId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId);

        return FindAll(_.DeployId == deployId);
    }
    #endregion

    #region 字段名
    /// <summary>取得部署版本字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用部署集。对应AppDeploy</summary>
        public static readonly Field DeployId = FindByName("DeployId");

        /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
        public static readonly Field Url = FindByName("Url");

        /// <summary>覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝</summary>
        public static readonly Field Overwrite = FindByName("Overwrite");

        /// <summary>发布模式。1部分包，仅覆盖；2标准包，清空可执行文件再覆盖；3完整包，清空所有文件</summary>
        public static readonly Field Mode = FindByName("Mode");

        /// <summary>文件大小</summary>
        public static readonly Field Size = FindByName("Size");

        /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
        public static readonly Field Hash = FindByName("Hash");

        /// <summary>运行时。RID是运行时标识符，用于标识应用程序运行所在的目标平台。如win-x64/linux-arm</summary>
        public static readonly Field Runtime = FindByName("Runtime");

        /// <summary>目标框架。TFM目标运行时框架，如net8.0</summary>
        public static readonly Field TargetFramework = FindByName("TargetFramework");

        /// <summary>进度。发布进度</summary>
        public static readonly Field Progress = FindByName("Progress");

        /// <summary>提交标识</summary>
        public static readonly Field CommitId = FindByName("CommitId");

        /// <summary>提交记录</summary>
        public static readonly Field CommitLog = FindByName("CommitLog");

        /// <summary>提交时间</summary>
        public static readonly Field CommitTime = FindByName("CommitTime");

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

    /// <summary>取得部署版本字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用部署集。对应AppDeploy</summary>
        public const String DeployId = "DeployId";

        /// <summary>版本。如2.3.2022.0911，字符串比较大小</summary>
        public const String Version = "Version";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>资源地址。一般打包为Zip包，StarAgent下载后解压缩覆盖</summary>
        public const String Url = "Url";

        /// <summary>覆盖文件。需要拷贝覆盖已存在的文件或子目录，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝</summary>
        public const String Overwrite = "Overwrite";

        /// <summary>发布模式。1部分包，仅覆盖；2标准包，清空可执行文件再覆盖；3完整包，清空所有文件</summary>
        public const String Mode = "Mode";

        /// <summary>文件大小</summary>
        public const String Size = "Size";

        /// <summary>文件哈希。MD5散列，避免下载的文件有缺失</summary>
        public const String Hash = "Hash";

        /// <summary>运行时。RID是运行时标识符，用于标识应用程序运行所在的目标平台。如win-x64/linux-arm</summary>
        public const String Runtime = "Runtime";

        /// <summary>目标框架。TFM目标运行时框架，如net8.0</summary>
        public const String TargetFramework = "TargetFramework";

        /// <summary>进度。发布进度</summary>
        public const String Progress = "Progress";

        /// <summary>提交标识</summary>
        public const String CommitId = "CommitId";

        /// <summary>提交记录</summary>
        public const String CommitLog = "CommitLog";

        /// <summary>提交时间</summary>
        public const String CommitTime = "CommitTime";

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
