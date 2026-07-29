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

/// <summary>资源版本。资源的多个版本，支持不同运行时平台</summary>
[Serializable]
[DataObject]
[Description("资源版本。资源的多个版本，支持不同运行时平台")]
[BindIndex("IX_AppResourceVersion_ResourceId_Version", false, "ResourceId,Version")]
[BindTable("AppResourceVersion", Description = "资源版本。资源的多个版本，支持不同运行时平台", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppResourceVersion
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ResourceId;
    /// <summary>资源</summary>
    [DisplayName("资源")]
    [Description("资源")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ResourceId", "资源", "")]
    public Int32 ResourceId { get => _ResourceId; set { if (OnPropertyChanging("ResourceId", value)) { _ResourceId = value; OnPropertyChanged("ResourceId"); } } }

    private String _Version;
    /// <summary>版本。如1.0.2025.0701</summary>
    [DisplayName("版本")]
    [Description("版本。如1.0.2025.0701")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Version", "版本。如1.0.2025.0701", "", Master = true)]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _Url;
    /// <summary>资源地址</summary>
    [DisplayName("资源地址")]
    [Description("资源地址")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Url", "资源地址", "", ItemType = "file")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private Int64 _Size;
    /// <summary>文件大小</summary>
    [DisplayName("文件大小")]
    [Description("文件大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
    public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

    private String _Hash;
    /// <summary>文件哈希。MD5</summary>
    [DisplayName("文件哈希")]
    [Description("文件哈希。MD5")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Hash", "文件哈希。MD5", "")]
    public String Hash { get => _Hash; set { if (OnPropertyChanging("Hash", value)) { _Hash = value; OnPropertyChanged("Hash"); } } }

    private Stardust.Models.OSKind _OS;
    /// <summary>操作系统。目标操作系统，为0表示通用</summary>
    [DisplayName("操作系统")]
    [Description("操作系统。目标操作系统，为0表示通用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OS", "操作系统。目标操作系统，为0表示通用", "")]
    public Stardust.Models.OSKind OS { get => _OS; set { if (OnPropertyChanging("OS", value)) { _OS = value; OnPropertyChanged("OS"); } } }

    private Stardust.Models.CpuArch _Arch;
    /// <summary>CPU架构。目标指令集架构，为0表示通用</summary>
    [DisplayName("CPU架构")]
    [Description("CPU架构。目标指令集架构，为0表示通用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Arch", "CPU架构。目标指令集架构，为0表示通用", "")]
    public Stardust.Models.CpuArch Arch { get => _Arch; set { if (OnPropertyChanging("Arch", value)) { _Arch = value; OnPropertyChanged("Arch"); } } }

    private String _TargetFramework;
    /// <summary>目标框架。如net8.0，为空表示通用</summary>
    [DisplayName("目标框架")]
    [Description("目标框架。如net8.0，为空表示通用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TargetFramework", "目标框架。如net8.0，为空表示通用", "")]
    public String TargetFramework { get => _TargetFramework; set { if (OnPropertyChanging("TargetFramework", value)) { _TargetFramework = value; OnPropertyChanged("TargetFramework"); } } }

    private String _TraceId;
    /// <summary>追踪</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪", "")]
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
            "ResourceId" => _ResourceId,
            "Version" => _Version,
            "Enable" => _Enable,
            "Url" => _Url,
            "Size" => _Size,
            "Hash" => _Hash,
            "OS" => _OS,
            "Arch" => _Arch,
            "TargetFramework" => _TargetFramework,
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
                case "ResourceId": _ResourceId = value.ToInt(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Url": _Url = Convert.ToString(value); break;
                case "Size": _Size = value.ToLong(); break;
                case "Hash": _Hash = Convert.ToString(value); break;
                case "OS": _OS = (Stardust.Models.OSKind)value.ToInt(); break;
                case "Arch": _Arch = (Stardust.Models.CpuArch)value.ToInt(); break;
                case "TargetFramework": _TargetFramework = Convert.ToString(value); break;
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
    /// <summary>资源</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AppResource Resource => Extends.Get(nameof(Resource), k => AppResource.FindById(ResourceId));

    /// <summary>资源</summary>
    [Map(nameof(ResourceId), typeof(AppResource), "Id")]
    public String ResourceName => Resource?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppResourceVersion FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据资源、版本查找</summary>
    /// <param name="resourceId">资源</param>
    /// <param name="version">版本</param>
    /// <returns>实体列表</returns>
    public static IList<AppResourceVersion> FindAllByResourceIdAndVersion(Int32 resourceId, String version)
    {
        if (resourceId < 0) return [];
        if (version.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ResourceId == resourceId && e.Version.EqualIgnoreCase(version));

        return FindAll(_.ResourceId == resourceId & _.Version == version);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="resourceId">资源</param>
    /// <param name="os">操作系统。目标操作系统，为0表示通用</param>
    /// <param name="arch">CPU架构。目标指令集架构，为0表示通用</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppResourceVersion> Search(Int32 resourceId, Stardust.Models.OSKind os, Stardust.Models.CpuArch arch, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (resourceId >= 0) exp &= _.ResourceId == resourceId;
        if (os >= 0) exp &= _.OS == os;
        if (arch >= 0) exp &= _.Arch == arch;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得资源版本字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>资源</summary>
        public static readonly Field ResourceId = FindByName("ResourceId");

        /// <summary>版本。如1.0.2025.0701</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>资源地址</summary>
        public static readonly Field Url = FindByName("Url");

        /// <summary>文件大小</summary>
        public static readonly Field Size = FindByName("Size");

        /// <summary>文件哈希。MD5</summary>
        public static readonly Field Hash = FindByName("Hash");

        /// <summary>操作系统。目标操作系统，为0表示通用</summary>
        public static readonly Field OS = FindByName("OS");

        /// <summary>CPU架构。目标指令集架构，为0表示通用</summary>
        public static readonly Field Arch = FindByName("Arch");

        /// <summary>目标框架。如net8.0，为空表示通用</summary>
        public static readonly Field TargetFramework = FindByName("TargetFramework");

        /// <summary>追踪</summary>
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

    /// <summary>取得资源版本字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>资源</summary>
        public const String ResourceId = "ResourceId";

        /// <summary>版本。如1.0.2025.0701</summary>
        public const String Version = "Version";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>资源地址</summary>
        public const String Url = "Url";

        /// <summary>文件大小</summary>
        public const String Size = "Size";

        /// <summary>文件哈希。MD5</summary>
        public const String Hash = "Hash";

        /// <summary>操作系统。目标操作系统，为0表示通用</summary>
        public const String OS = "OS";

        /// <summary>CPU架构。目标指令集架构，为0表示通用</summary>
        public const String Arch = "Arch";

        /// <summary>目标框架。如net8.0，为空表示通用</summary>
        public const String TargetFramework = "TargetFramework";

        /// <summary>追踪</summary>
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
