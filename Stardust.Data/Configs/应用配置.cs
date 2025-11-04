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

namespace Stardust.Data.Configs;

/// <summary>应用配置。需要管理配置的应用系统列表，每个应用以命令对形式管理配置数据，支持版本发布</summary>
[Serializable]
[DataObject]
[Description("应用配置。需要管理配置的应用系统列表，每个应用以命令对形式管理配置数据，支持版本发布")]
[BindIndex("IU_AppConfig_Name", true, "Name")]
[BindIndex("IX_AppConfig_ProjectId", false, "ProjectId")]
[BindIndex("IX_AppConfig_AppId", false, "AppId")]
[BindTable("AppConfig", Description = "应用配置。需要管理配置的应用系统列表，每个应用以命令对形式管理配置数据，支持版本发布", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppConfig
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
    /// <summary>项目。资源归属的团队</summary>
    [DisplayName("项目")]
    [Description("项目。资源归属的团队")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。资源归属的团队", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Int32 _AppId;
    /// <summary>应用。对应StarApp</summary>
    [DisplayName("应用")]
    [Description("应用。对应StarApp")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用。对应StarApp", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _DisplayName;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "显示名", "")]
    public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Version;
    /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
    [DisplayName("版本")]
    [Description("版本。应用正在使用的版本号，返回小于等于该版本的配置")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Version", "版本。应用正在使用的版本号，返回小于等于该版本的配置", "")]
    public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Int32 _NextVersion;
    /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
    [DisplayName("下一版本")]
    [Description("下一版本。下一个将要发布的版本，发布后两者相同")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NextVersion", "下一版本。下一个将要发布的版本，发布后两者相同", "")]
    public Int32 NextVersion { get => _NextVersion; set { if (OnPropertyChanging("NextVersion", value)) { _NextVersion = value; OnPropertyChanged("NextVersion"); } } }

    private DateTime _PublishTime;
    /// <summary>定时发布。在指定时间自动发布新版本</summary>
    [DisplayName("定时发布")]
    [Description("定时发布。在指定时间自动发布新版本")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("PublishTime", "定时发布。在指定时间自动发布新版本", "")]
    public DateTime PublishTime { get => _PublishTime; set { if (OnPropertyChanging("PublishTime", value)) { _PublishTime = value; OnPropertyChanged("PublishTime"); } } }

    private Boolean _CanBeQuoted;
    /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
    [DisplayName("可被依赖")]
    [Description("可被依赖。打开后，才能被其它应用依赖")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CanBeQuoted", "可被依赖。打开后，才能被其它应用依赖", "")]
    public Boolean CanBeQuoted { get => _CanBeQuoted; set { if (OnPropertyChanging("CanBeQuoted", value)) { _CanBeQuoted = value; OnPropertyChanged("CanBeQuoted"); } } }

    private String _Quotes;
    /// <summary>依赖应用。所依赖应用的集合</summary>
    [DisplayName("依赖应用")]
    [Description("依赖应用。所依赖应用的集合")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Quotes", "依赖应用。所依赖应用的集合", "")]
    public String Quotes { get => _Quotes; set { if (OnPropertyChanging("Quotes", value)) { _Quotes = value; OnPropertyChanged("Quotes"); } } }

    private Boolean _IsGlobal;
    /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
    [DisplayName("全局")]
    [Description("全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsGlobal", "全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回", "")]
    public Boolean IsGlobal { get => _IsGlobal; set { if (OnPropertyChanging("IsGlobal", value)) { _IsGlobal = value; OnPropertyChanged("IsGlobal"); } } }

    private Boolean _Readonly;
    /// <summary>只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改</summary>
    [DisplayName("只读")]
    [Description("只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Readonly", "只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改", "")]
    public Boolean Readonly { get => _Readonly; set { if (OnPropertyChanging("Readonly", value)) { _Readonly = value; OnPropertyChanged("Readonly"); } } }

    private Boolean _EnableWorkerId;
    /// <summary>雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增</summary>
    [DisplayName("雪花标识")]
    [Description("雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EnableWorkerId", "雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增", "")]
    public Boolean EnableWorkerId { get => _EnableWorkerId; set { if (OnPropertyChanging("EnableWorkerId", value)) { _EnableWorkerId = value; OnPropertyChanged("EnableWorkerId"); } } }

    private Boolean _EnableApollo;
    /// <summary>阿波罗</summary>
    [Category("阿波罗")]
    [DisplayName("阿波罗")]
    [Description("阿波罗")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EnableApollo", "阿波罗", "")]
    public Boolean EnableApollo { get => _EnableApollo; set { if (OnPropertyChanging("EnableApollo", value)) { _EnableApollo = value; OnPropertyChanged("EnableApollo"); } } }

    private String _ApolloMetaServer;
    /// <summary>阿波罗地址</summary>
    [Category("阿波罗")]
    [DisplayName("阿波罗地址")]
    [Description("阿波罗地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ApolloMetaServer", "阿波罗地址", "")]
    public String ApolloMetaServer { get => _ApolloMetaServer; set { if (OnPropertyChanging("ApolloMetaServer", value)) { _ApolloMetaServer = value; OnPropertyChanged("ApolloMetaServer"); } } }

    private String _ApolloAppId;
    /// <summary>阿波罗账号</summary>
    [Category("阿波罗")]
    [DisplayName("阿波罗账号")]
    [Description("阿波罗账号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ApolloAppId", "阿波罗账号", "")]
    public String ApolloAppId { get => _ApolloAppId; set { if (OnPropertyChanging("ApolloAppId", value)) { _ApolloAppId = value; OnPropertyChanged("ApolloAppId"); } } }

    private String _ApolloNameSpace;
    /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
    [Category("阿波罗")]
    [DisplayName("阿波罗命名空间")]
    [Description("阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ApolloNameSpace", "阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。", "")]
    public String ApolloNameSpace { get => _ApolloNameSpace; set { if (OnPropertyChanging("ApolloNameSpace", value)) { _ApolloNameSpace = value; OnPropertyChanged("ApolloNameSpace"); } } }

    private String _UsedKeys;
    /// <summary>已使用。用过的配置项</summary>
    [Category("配置项")]
    [DisplayName("已使用")]
    [Description("已使用。用过的配置项")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("UsedKeys", "已使用。用过的配置项", "")]
    public String UsedKeys { get => _UsedKeys; set { if (OnPropertyChanging("UsedKeys", value)) { _UsedKeys = value; OnPropertyChanged("UsedKeys"); } } }

    private String _MissedKeys;
    /// <summary>缺失键。没有读取到的配置项</summary>
    [Category("配置项")]
    [DisplayName("缺失键")]
    [Description("缺失键。没有读取到的配置项")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("MissedKeys", "缺失键。没有读取到的配置项", "")]
    public String MissedKeys { get => _MissedKeys; set { if (OnPropertyChanging("MissedKeys", value)) { _MissedKeys = value; OnPropertyChanged("MissedKeys"); } } }

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
            "ProjectId" => _ProjectId,
            "Category" => _Category,
            "AppId" => _AppId,
            "Name" => _Name,
            "DisplayName" => _DisplayName,
            "Enable" => _Enable,
            "Version" => _Version,
            "NextVersion" => _NextVersion,
            "PublishTime" => _PublishTime,
            "CanBeQuoted" => _CanBeQuoted,
            "Quotes" => _Quotes,
            "IsGlobal" => _IsGlobal,
            "Readonly" => _Readonly,
            "EnableWorkerId" => _EnableWorkerId,
            "EnableApollo" => _EnableApollo,
            "ApolloMetaServer" => _ApolloMetaServer,
            "ApolloAppId" => _ApolloAppId,
            "ApolloNameSpace" => _ApolloNameSpace,
            "UsedKeys" => _UsedKeys,
            "MissedKeys" => _MissedKeys,
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
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Version": _Version = value.ToInt(); break;
                case "NextVersion": _NextVersion = value.ToInt(); break;
                case "PublishTime": _PublishTime = value.ToDateTime(); break;
                case "CanBeQuoted": _CanBeQuoted = value.ToBoolean(); break;
                case "Quotes": _Quotes = Convert.ToString(value); break;
                case "IsGlobal": _IsGlobal = value.ToBoolean(); break;
                case "Readonly": _Readonly = value.ToBoolean(); break;
                case "EnableWorkerId": _EnableWorkerId = value.ToBoolean(); break;
                case "EnableApollo": _EnableApollo = value.ToBoolean(); break;
                case "ApolloMetaServer": _ApolloMetaServer = Convert.ToString(value); break;
                case "ApolloAppId": _ApolloAppId = Convert.ToString(value); break;
                case "ApolloNameSpace": _ApolloNameSpace = Convert.ToString(value); break;
                case "UsedKeys": _UsedKeys = Convert.ToString(value); break;
                case "MissedKeys": _MissedKeys = Convert.ToString(value); break;
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
    /// <summary>项目</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Platform.GalaxyProject Project => Extends.Get(nameof(Project), k => Stardust.Data.Platform.GalaxyProject.FindById(ProjectId));

    /// <summary>项目</summary>
    [Map(nameof(ProjectId), typeof(Stardust.Data.Platform.GalaxyProject), "Id")]
    public String ProjectName => Project?.Name;

    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目。资源归属的团队</param>
    /// <param name="appId">应用。对应StarApp</param>
    /// <param name="canBeQuoted">可被依赖。打开后，才能被其它应用依赖</param>
    /// <param name="isGlobal">全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</param>
    /// <param name="readonly">只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改</param>
    /// <param name="enableWorkerId">雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增</param>
    /// <param name="enableApollo">阿波罗</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppConfig> Search(Int32 projectId, Int32 appId, Boolean? canBeQuoted, Boolean? isGlobal, Boolean? @readonly, Boolean? enableWorkerId, Boolean? enableApollo, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appId >= 0) exp &= _.AppId == appId;
        if (canBeQuoted != null) exp &= _.CanBeQuoted == canBeQuoted;
        if (isGlobal != null) exp &= _.IsGlobal == isGlobal;
        if (@readonly != null) exp &= _.Readonly == @readonly;
        if (enableWorkerId != null) exp &= _.EnableWorkerId == enableWorkerId;
        if (enableApollo != null) exp &= _.EnableApollo == enableApollo;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>应用。对应StarApp</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>显示名</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
        public static readonly Field NextVersion = FindByName("NextVersion");

        /// <summary>定时发布。在指定时间自动发布新版本</summary>
        public static readonly Field PublishTime = FindByName("PublishTime");

        /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
        public static readonly Field CanBeQuoted = FindByName("CanBeQuoted");

        /// <summary>依赖应用。所依赖应用的集合</summary>
        public static readonly Field Quotes = FindByName("Quotes");

        /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
        public static readonly Field IsGlobal = FindByName("IsGlobal");

        /// <summary>只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改</summary>
        public static readonly Field Readonly = FindByName("Readonly");

        /// <summary>雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增</summary>
        public static readonly Field EnableWorkerId = FindByName("EnableWorkerId");

        /// <summary>阿波罗</summary>
        public static readonly Field EnableApollo = FindByName("EnableApollo");

        /// <summary>阿波罗地址</summary>
        public static readonly Field ApolloMetaServer = FindByName("ApolloMetaServer");

        /// <summary>阿波罗账号</summary>
        public static readonly Field ApolloAppId = FindByName("ApolloAppId");

        /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
        public static readonly Field ApolloNameSpace = FindByName("ApolloNameSpace");

        /// <summary>已使用。用过的配置项</summary>
        public static readonly Field UsedKeys = FindByName("UsedKeys");

        /// <summary>缺失键。没有读取到的配置项</summary>
        public static readonly Field MissedKeys = FindByName("MissedKeys");

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

    /// <summary>取得应用配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>应用。对应StarApp</summary>
        public const String AppId = "AppId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>显示名</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>版本。应用正在使用的版本号，返回小于等于该版本的配置</summary>
        public const String Version = "Version";

        /// <summary>下一版本。下一个将要发布的版本，发布后两者相同</summary>
        public const String NextVersion = "NextVersion";

        /// <summary>定时发布。在指定时间自动发布新版本</summary>
        public const String PublishTime = "PublishTime";

        /// <summary>可被依赖。打开后，才能被其它应用依赖</summary>
        public const String CanBeQuoted = "CanBeQuoted";

        /// <summary>依赖应用。所依赖应用的集合</summary>
        public const String Quotes = "Quotes";

        /// <summary>全局。该应用下的配置数据作为全局数据，请求任意应用配置都返回</summary>
        public const String IsGlobal = "IsGlobal";

        /// <summary>只读。只读应用，不支持客户端上传配置数据，可用于保护数据避免被错误修改</summary>
        public const String Readonly = "Readonly";

        /// <summary>雪花标识。给应用端分配唯一工作站标识，用于生成雪花Id，随着使用递增</summary>
        public const String EnableWorkerId = "EnableWorkerId";

        /// <summary>阿波罗</summary>
        public const String EnableApollo = "EnableApollo";

        /// <summary>阿波罗地址</summary>
        public const String ApolloMetaServer = "ApolloMetaServer";

        /// <summary>阿波罗账号</summary>
        public const String ApolloAppId = "ApolloAppId";

        /// <summary>阿波罗命名空间。默认application，也可以填依赖的公共命名空间，但建议为公共命名空间建立应用依赖。</summary>
        public const String ApolloNameSpace = "ApolloNameSpace";

        /// <summary>已使用。用过的配置项</summary>
        public const String UsedKeys = "UsedKeys";

        /// <summary>缺失键。没有读取到的配置项</summary>
        public const String MissedKeys = "MissedKeys";

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
