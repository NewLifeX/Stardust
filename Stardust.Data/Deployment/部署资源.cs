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

/// <summary>部署资源。附加资源管理，如数据库驱动、SSL证书、配置模板等，支持全局/项目/应用三级归属</summary>
[Serializable]
[DataObject]
[Description("部署资源。附加资源管理，如数据库驱动、SSL证书、配置模板等，支持全局/项目/应用三级归属")]
[BindIndex("IU_AppResource_ProjectId_Name", true, "ProjectId,Name")]
[BindIndex("IX_AppResource_Category", false, "Category")]
[BindTable("AppResource", Description = "部署资源。附加资源管理，如数据库驱动、SSL证书、配置模板等，支持全局/项目/应用三级归属", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppResource
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ProjectId;
    /// <summary>项目。资源归属项目，为0时表示全局资源</summary>
    [DisplayName("项目")]
    [Description("项目。资源归属项目，为0时表示全局资源")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。资源归属项目，为0时表示全局资源", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Name;
    /// <summary>名称。资源唯一标识，如dm8-driver、newlifex-cert</summary>
    [DisplayName("名称")]
    [Description("名称。资源唯一标识，如dm8-driver、newlifex-cert")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称。资源唯一标识，如dm8-driver、newlifex-cert", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>类别。driver/cert/config/plugin等</summary>
    [DisplayName("类别")]
    [Description("类别。driver/cert/config/plugin等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别。driver/cert/config/plugin等", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _TargetPath;
    /// <summary>目标路径。相对于应用工作目录，如../Plugins、./certs</summary>
    [DisplayName("目标路径")]
    [Description("目标路径。相对于应用工作目录，如../Plugins、./certs")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TargetPath", "目标路径。相对于应用工作目录，如../Plugins、./certs", "")]
    public String TargetPath { get => _TargetPath; set { if (OnPropertyChanging("TargetPath", value)) { _TargetPath = value; OnPropertyChanged("TargetPath"); } } }

    private Boolean _UnZip;
    /// <summary>解压缩。下载后是否自动解压</summary>
    [DisplayName("解压缩")]
    [Description("解压缩。下载后是否自动解压")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UnZip", "解压缩。下载后是否自动解压", "")]
    public Boolean UnZip { get => _UnZip; set { if (OnPropertyChanging("UnZip", value)) { _UnZip = value; OnPropertyChanged("UnZip"); } } }

    private String _Overwrite;
    /// <summary>覆盖文件。需要覆盖的文件模式</summary>
    [DisplayName("覆盖文件")]
    [Description("覆盖文件。需要覆盖的文件模式")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Overwrite", "覆盖文件。需要覆盖的文件模式", "")]
    public String Overwrite { get => _Overwrite; set { if (OnPropertyChanging("Overwrite", value)) { _Overwrite = value; OnPropertyChanged("Overwrite"); } } }

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
            "ProjectId" => _ProjectId,
            "Name" => _Name,
            "Category" => _Category,
            "Enable" => _Enable,
            "TargetPath" => _TargetPath,
            "UnZip" => _UnZip,
            "Overwrite" => _Overwrite,
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
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "TargetPath": _TargetPath = Convert.ToString(value); break;
                case "UnZip": _UnZip = value.ToBoolean(); break;
                case "Overwrite": _Overwrite = Convert.ToString(value); break;
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
    public static AppResource FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目、名称查找</summary>
    /// <param name="projectId">项目</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static AppResource FindByProjectIdAndName(Int32 projectId, String name)
    {
        if (projectId < 0) return null;
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProjectId == projectId && e.Name.EqualIgnoreCase(name));

        return Find(_.ProjectId == projectId & _.Name == name);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<AppResource> FindAllByProjectId(Int32 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据类别查找</summary>
    /// <param name="category">类别</param>
    /// <returns>实体列表</returns>
    public static IList<AppResource> FindAllByCategory(String category)
    {
        if (category.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

        return FindAll(_.Category == category);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目。资源归属项目，为0时表示全局资源</param>
    /// <param name="category">类别。driver/cert/config/plugin等</param>
    /// <param name="unZip">解压缩。下载后是否自动解压</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppResource> Search(Int32 projectId, String category, Boolean? unZip, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (unZip != null) exp &= _.UnZip == unZip;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得部署资源字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属项目，为0时表示全局资源</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称。资源唯一标识，如dm8-driver、newlifex-cert</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>类别。driver/cert/config/plugin等</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>目标路径。相对于应用工作目录，如../Plugins、./certs</summary>
        public static readonly Field TargetPath = FindByName("TargetPath");

        /// <summary>解压缩。下载后是否自动解压</summary>
        public static readonly Field UnZip = FindByName("UnZip");

        /// <summary>覆盖文件。需要覆盖的文件模式</summary>
        public static readonly Field Overwrite = FindByName("Overwrite");

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

    /// <summary>取得部署资源字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属项目，为0时表示全局资源</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称。资源唯一标识，如dm8-driver、newlifex-cert</summary>
        public const String Name = "Name";

        /// <summary>类别。driver/cert/config/plugin等</summary>
        public const String Category = "Category";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>目标路径。相对于应用工作目录，如../Plugins、./certs</summary>
        public const String TargetPath = "TargetPath";

        /// <summary>解压缩。下载后是否自动解压</summary>
        public const String UnZip = "UnZip";

        /// <summary>覆盖文件。需要覆盖的文件模式</summary>
        public const String Overwrite = "Overwrite";

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
