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

/// <summary>产品发布包。隶属于发布版本的安装包，面向不同.NET运行时目标</summary>
[Serializable]
[DataObject]
[Description("产品发布包。隶属于发布版本的安装包，面向不同.NET运行时目标")]
[BindIndex("IX_ProductPackage_ReleaseId", false, "ReleaseId")]
[BindTable("ProductPackage", Description = "产品发布包。隶属于发布版本的安装包，面向不同.NET运行时目标", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class ProductPackage
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ReleaseId;
    /// <summary>发布版本。所属发布版本</summary>
    [DisplayName("发布版本")]
    [Description("发布版本。所属发布版本")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ReleaseId", "发布版本。所属发布版本", "")]
    public Int32 ReleaseId { get => _ReleaseId; set { if (OnPropertyChanging("ReleaseId", value)) { _ReleaseId = value; OnPropertyChanged("ReleaseId"); } } }

    private String _TargetRuntime;
    /// <summary>目标运行时。目标.NET运行时主版本，如 4/5/6/7/8/9/10，*表示万能包</summary>
    [DisplayName("目标运行时")]
    [Description("目标运行时。目标.NET运行时主版本，如 4/5/6/7/8/9/10，*表示万能包")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TargetRuntime", "目标运行时。目标.NET运行时主版本，如 4/5/6/7/8/9/10，*表示万能包", "")]
    public String TargetRuntime { get => _TargetRuntime; set { if (OnPropertyChanging("TargetRuntime", value)) { _TargetRuntime = value; OnPropertyChanged("TargetRuntime"); } } }

    private String _FileName;
    /// <summary>文件名。安装包文件名，如 staragent80.zip</summary>
    [DisplayName("文件名")]
    [Description("文件名。安装包文件名，如 staragent80.zip")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("FileName", "文件名。安装包文件名，如 staragent80.zip", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _Source;
    /// <summary>升级源。下载路径（Cube附件或外部URL）</summary>
    [DisplayName("升级源")]
    [Description("升级源。下载路径（Cube附件或外部URL）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Source", "升级源。下载路径（Cube附件或外部URL）", "", ItemType = "file-zip")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int64 _Size;
    /// <summary>文件大小</summary>
    [DisplayName("文件大小")]
    [Description("文件大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
    public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

    private String _FileHash;
    /// <summary>文件哈希。MD5散列</summary>
    [DisplayName("文件哈希")]
    [Description("文件哈希。MD5散列")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FileHash", "文件哈希。MD5散列", "")]
    public String FileHash { get => _FileHash; set { if (OnPropertyChanging("FileHash", value)) { _FileHash = value; OnPropertyChanged("FileHash"); } } }

    private String _Preinstall;
    /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
    [DisplayName("预安装命令")]
    [Description("预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Preinstall", "预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行", "")]
    public String Preinstall { get => _Preinstall; set { if (OnPropertyChanging("Preinstall", value)) { _Preinstall = value; OnPropertyChanged("Preinstall"); } } }

    private String _Executor;
    /// <summary>执行命令。空格前后为文件名和参数，客户端根据此命令启动新版本</summary>
    [DisplayName("执行命令")]
    [Description("执行命令。空格前后为文件名和参数，客户端根据此命令启动新版本")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Executor", "执行命令。空格前后为文件名和参数，客户端根据此命令启动新版本", "")]
    public String Executor { get => _Executor; set { if (OnPropertyChanging("Executor", value)) { _Executor = value; OnPropertyChanged("Executor"); } } }

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

    private String _Remark;
    /// <summary>备注。如 net8.0 通用</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注。如 net8.0 通用")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注。如 net8.0 通用", "")]
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
            "ReleaseId" => _ReleaseId,
            "TargetRuntime" => _TargetRuntime,
            "FileName" => _FileName,
            "Source" => _Source,
            "Size" => _Size,
            "FileHash" => _FileHash,
            "Preinstall" => _Preinstall,
            "Executor" => _Executor,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ReleaseId": _ReleaseId = value.ToInt(); break;
                case "TargetRuntime": _TargetRuntime = Convert.ToString(value); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Size": _Size = value.ToLong(); break;
                case "FileHash": _FileHash = Convert.ToString(value); break;
                case "Preinstall": _Preinstall = Convert.ToString(value); break;
                case "Executor": _Executor = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>发布版本</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ProductRelease Release => Extends.Get(nameof(Release), k => ProductRelease.FindById(ReleaseId));

    /// <summary>发布版本</summary>
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ProductPackage FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据发布版本查找</summary>
    /// <param name="releaseId">发布版本</param>
    /// <returns>实体列表</returns>
    public static IList<ProductPackage> FindAllByReleaseId(Int32 releaseId)
    {
        if (releaseId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ReleaseId == releaseId);

        return FindAll(_.ReleaseId == releaseId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="releaseId">发布版本。所属发布版本</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ProductPackage> Search(Int32 releaseId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (releaseId >= 0) exp &= _.ReleaseId == releaseId;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得产品发布包字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>发布版本。所属发布版本</summary>
        public static readonly Field ReleaseId = FindByName("ReleaseId");

        /// <summary>目标运行时。目标.NET运行时主版本，如 4/5/6/7/8/9/10，*表示万能包</summary>
        public static readonly Field TargetRuntime = FindByName("TargetRuntime");

        /// <summary>文件名。安装包文件名，如 staragent80.zip</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>升级源。下载路径（Cube附件或外部URL）</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>文件大小</summary>
        public static readonly Field Size = FindByName("Size");

        /// <summary>文件哈希。MD5散列</summary>
        public static readonly Field FileHash = FindByName("FileHash");

        /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
        public static readonly Field Preinstall = FindByName("Preinstall");

        /// <summary>执行命令。空格前后为文件名和参数，客户端根据此命令启动新版本</summary>
        public static readonly Field Executor = FindByName("Executor");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>备注。如 net8.0 通用</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得产品发布包字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>发布版本。所属发布版本</summary>
        public const String ReleaseId = "ReleaseId";

        /// <summary>目标运行时。目标.NET运行时主版本，如 4/5/6/7/8/9/10，*表示万能包</summary>
        public const String TargetRuntime = "TargetRuntime";

        /// <summary>文件名。安装包文件名，如 staragent80.zip</summary>
        public const String FileName = "FileName";

        /// <summary>升级源。下载路径（Cube附件或外部URL）</summary>
        public const String Source = "Source";

        /// <summary>文件大小</summary>
        public const String Size = "Size";

        /// <summary>文件哈希。MD5散列</summary>
        public const String FileHash = "FileHash";

        /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
        public const String Preinstall = "Preinstall";

        /// <summary>执行命令。空格前后为文件名和参数，客户端根据此命令启动新版本</summary>
        public const String Executor = "Executor";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>备注。如 net8.0 通用</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
