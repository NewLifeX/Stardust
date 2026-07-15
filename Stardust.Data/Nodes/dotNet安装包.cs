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

/// <summary>dotNet安装包。管理面向不同操作系统和CPU架构的.NET运行时安装包</summary>
[Serializable]
[DataObject]
[Description("dotNet安装包。管理面向不同操作系统和CPU架构的.NET运行时安装包")]
[BindIndex("IX_DotNetPackage_Version_Kind_OSKind_Architecture", false, "Version,Kind,OSKind,Architecture")]
[BindTable("DotNetPackage", Description = "dotNet安装包。管理面向不同操作系统和CPU架构的.NET运行时安装包", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class DotNetPackage
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Version;
    /// <summary>版本号。完整版本号，如 10.0.9</summary>
    [DisplayName("版本号")]
    [Description("版本号。完整版本号，如 10.0.9")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本号。完整版本号，如 10.0.9", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _Kind;
    /// <summary>安装类型。runtime/aspnet/desktop/host</summary>
    [DisplayName("安装类型")]
    [Description("安装类型。runtime/aspnet/desktop/host")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Kind", "安装类型。runtime/aspnet/desktop/host", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Stardust.Models.OSKind _OSKind;
    /// <summary>操作系统。目标操作系统，0表示通用</summary>
    [DisplayName("操作系统")]
    [Description("操作系统。目标操作系统，0表示通用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OSKind", "操作系统。目标操作系统，0表示通用", "")]
    public Stardust.Models.OSKind OSKind { get => _OSKind; set { if (OnPropertyChanging("OSKind", value)) { _OSKind = value; OnPropertyChanged("OSKind"); } } }

    private Stardust.Models.CpuArch _Architecture;
    /// <summary>CPU架构。目标指令集架构，0表示通用</summary>
    [DisplayName("CPU架构")]
    [Description("CPU架构。目标指令集架构，0表示通用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Architecture", "CPU架构。目标指令集架构，0表示通用", "")]
    public Stardust.Models.CpuArch Architecture { get => _Architecture; set { if (OnPropertyChanging("Architecture", value)) { _Architecture = value; OnPropertyChanged("Architecture"); } } }

    private String _FileName;
    /// <summary>文件名。官方文件名，如 aspnetcore-runtime-10.0.9-linux-x64.tar.gz</summary>
    [DisplayName("文件名")]
    [Description("文件名。官方文件名，如 aspnetcore-runtime-10.0.9-linux-x64.tar.gz")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("FileName", "文件名。官方文件名，如 aspnetcore-runtime-10.0.9-linux-x64.tar.gz", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _Source;
    /// <summary>下载源。Cube附件（手动上传）或外部URL（自动同步）</summary>
    [DisplayName("下载源")]
    [Description("下载源。Cube附件（手动上传）或外部URL（自动同步）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Source", "下载源。Cube附件（手动上传）或外部URL（自动同步）", "", ItemType = "file-zip")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int64 _Size;
    /// <summary>文件大小</summary>
    [DisplayName("文件大小")]
    [Description("文件大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
    public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

    private String _FileHash;
    /// <summary>文件哈希。SHA512散列</summary>
    [DisplayName("文件哈希")]
    [Description("文件哈希。SHA512散列")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("FileHash", "文件哈希。SHA512散列", "")]
    public String FileHash { get => _FileHash; set { if (OnPropertyChanging("FileHash", value)) { _FileHash = value; OnPropertyChanged("FileHash"); } } }

    private Boolean _Enable;
    /// <summary>启用。启用/停用</summary>
    [DisplayName("启用")]
    [Description("启用。启用/停用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。启用/停用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Force;
    /// <summary>强制。强制安装，即使已存在同版本也重新安装</summary>
    [DisplayName("强制")]
    [Description("强制。强制安装，即使已存在同版本也重新安装")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Force", "强制。强制安装，即使已存在同版本也重新安装", "")]
    public Boolean Force { get => _Force; set { if (OnPropertyChanging("Force", value)) { _Force = value; OnPropertyChanged("Force"); } } }

    private NodeChannels _Channel;
    /// <summary>升级通道</summary>
    [DisplayName("升级通道")]
    [Description("升级通道")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Channel", "升级通道", "")]
    public NodeChannels Channel { get => _Channel; set { if (OnPropertyChanging("Channel", value)) { _Channel = value; OnPropertyChanged("Channel"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建者", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

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

    private Int32 _UpdateUserID;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新者", "")]
    public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

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
            "Version" => _Version,
            "Kind" => _Kind,
            "OSKind" => _OSKind,
            "Architecture" => _Architecture,
            "FileName" => _FileName,
            "Source" => _Source,
            "Size" => _Size,
            "FileHash" => _FileHash,
            "Enable" => _Enable,
            "Force" => _Force,
            "Channel" => _Channel,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserID" => _UpdateUserID,
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
                case "Version": _Version = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "OSKind": _OSKind = (Stardust.Models.OSKind)value.ToInt(); break;
                case "Architecture": _Architecture = (Stardust.Models.CpuArch)value.ToInt(); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Size": _Size = value.ToLong(); break;
                case "FileHash": _FileHash = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Force": _Force = value.ToBoolean(); break;
                case "Channel": _Channel = (NodeChannels)value.ToInt(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
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
    public static DotNetPackage FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="version">版本号。完整版本号，如 10.0.9</param>
    /// <param name="kind">安装类型。runtime/aspnet/desktop/host</param>
    /// <param name="oSKind">操作系统。目标操作系统，0表示通用</param>
    /// <param name="architecture">CPU架构。目标指令集架构，0表示通用</param>
    /// <param name="force">强制。强制安装，即使已存在同版本也重新安装</param>
    /// <param name="channel">升级通道</param>
    /// <param name="enable">启用。启用/停用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DotNetPackage> Search(String version, String kind, Stardust.Models.OSKind oSKind, Stardust.Models.CpuArch architecture, Boolean? force, NodeChannels channel, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!version.IsNullOrEmpty()) exp &= _.Version == version;
        if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
        if (oSKind >= 0) exp &= _.OSKind == oSKind;
        if (architecture >= 0) exp &= _.Architecture == architecture;
        if (force != null) exp &= _.Force == force;
        if (channel >= 0) exp &= _.Channel == channel;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得dotNet安装包字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>版本号。完整版本号，如 10.0.9</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>安装类型。runtime/aspnet/desktop/host</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>操作系统。目标操作系统，0表示通用</summary>
        public static readonly Field OSKind = FindByName("OSKind");

        /// <summary>CPU架构。目标指令集架构，0表示通用</summary>
        public static readonly Field Architecture = FindByName("Architecture");

        /// <summary>文件名。官方文件名，如 aspnetcore-runtime-10.0.9-linux-x64.tar.gz</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>下载源。Cube附件（手动上传）或外部URL（自动同步）</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>文件大小</summary>
        public static readonly Field Size = FindByName("Size");

        /// <summary>文件哈希。SHA512散列</summary>
        public static readonly Field FileHash = FindByName("FileHash");

        /// <summary>启用。启用/停用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>强制。强制安装，即使已存在同版本也重新安装</summary>
        public static readonly Field Force = FindByName("Force");

        /// <summary>升级通道</summary>
        public static readonly Field Channel = FindByName("Channel");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得dotNet安装包字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>版本号。完整版本号，如 10.0.9</summary>
        public const String Version = "Version";

        /// <summary>安装类型。runtime/aspnet/desktop/host</summary>
        public const String Kind = "Kind";

        /// <summary>操作系统。目标操作系统，0表示通用</summary>
        public const String OSKind = "OSKind";

        /// <summary>CPU架构。目标指令集架构，0表示通用</summary>
        public const String Architecture = "Architecture";

        /// <summary>文件名。官方文件名，如 aspnetcore-runtime-10.0.9-linux-x64.tar.gz</summary>
        public const String FileName = "FileName";

        /// <summary>下载源。Cube附件（手动上传）或外部URL（自动同步）</summary>
        public const String Source = "Source";

        /// <summary>文件大小</summary>
        public const String Size = "Size";

        /// <summary>文件哈希。SHA512散列</summary>
        public const String FileHash = "FileHash";

        /// <summary>启用。启用/停用</summary>
        public const String Enable = "Enable";

        /// <summary>强制。强制安装，即使已存在同版本也重新安装</summary>
        public const String Force = "Force";

        /// <summary>升级通道</summary>
        public const String Channel = "Channel";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
