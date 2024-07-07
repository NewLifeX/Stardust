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

namespace Stardust.Data;

/// <summary>应用系统。服务提供者和消费者</summary>
[Serializable]
[DataObject]
[Description("应用系统。服务提供者和消费者")]
[BindIndex("IU_StarApp_Name", true, "Name")]
[BindIndex("IX_StarApp_ProjectId", false, "ProjectId")]
[BindTable("StarApp", Description = "应用系统。服务提供者和消费者", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class App
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

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _DisplayName;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "显示名", "")]
    public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private String _Secret;
    /// <summary>密钥</summary>
    [DisplayName("密钥")]
    [Description("密钥")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Secret", "密钥", "")]
    public String Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

    private String _Category;
    /// <summary>类别</summary>
    [DisplayName("类别")]
    [Description("类别")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "类别", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _AutoActive;
    /// <summary>自动激活。自动激活启用该应用在注册中心提供的服务，供其它消费者使用</summary>
    [DisplayName("自动激活")]
    [Description("自动激活。自动激活启用该应用在注册中心提供的服务，供其它消费者使用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AutoActive", "自动激活。自动激活启用该应用在注册中心提供的服务，供其它消费者使用", "")]
    public Boolean AutoActive { get => _AutoActive; set { if (OnPropertyChanging("AutoActive", value)) { _AutoActive = value; OnPropertyChanged("AutoActive"); } } }

    private String _Version;
    /// <summary>版本。多版本实例使用时，仅记录最大版本</summary>
    [DisplayName("版本")]
    [Description("版本。多版本实例使用时，仅记录最大版本")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本。多版本实例使用时，仅记录最大版本", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private DateTime _Compile;
    /// <summary>编译时间。最大版本应用的编译时间</summary>
    [DisplayName("编译时间")]
    [Description("编译时间。最大版本应用的编译时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Compile", "编译时间。最大版本应用的编译时间", "")]
    public DateTime Compile { get => _Compile; set { if (OnPropertyChanging("Compile", value)) { _Compile = value; OnPropertyChanged("Compile"); } } }

    private Int32 _Period;
    /// <summary>采样周期。应用定期采样并上报性能数据，默认60秒</summary>
    [DisplayName("采样周期")]
    [Description("采样周期。应用定期采样并上报性能数据，默认60秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Period", "采样周期。应用定期采样并上报性能数据，默认60秒", "")]
    public Int32 Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

    private Boolean _Singleton;
    /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于管理实例</summary>
    [DisplayName("单例")]
    [Description("单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于管理实例")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Singleton", "单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于管理实例", "")]
    public Boolean Singleton { get => _Singleton; set { if (OnPropertyChanging("Singleton", value)) { _Singleton = value; OnPropertyChanged("Singleton"); } } }

    private String _WhiteIPs;
    /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
    [Category("安全告警")]
    [DisplayName("IP白名单")]
    [Description("IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("WhiteIPs", "IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开", "")]
    public String WhiteIPs { get => _WhiteIPs; set { if (OnPropertyChanging("WhiteIPs", value)) { _WhiteIPs = value; OnPropertyChanged("WhiteIPs"); } } }

    private String _BlackIPs;
    /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
    [Category("安全告警")]
    [DisplayName("IP黑名单")]
    [Description("IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("BlackIPs", "IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开", "")]
    public String BlackIPs { get => _BlackIPs; set { if (OnPropertyChanging("BlackIPs", value)) { _BlackIPs = value; OnPropertyChanged("BlackIPs"); } } }

    private String _WebHook;
    /// <summary>告警机器人。钉钉、企业微信等</summary>
    [Category("安全告警")]
    [DisplayName("告警机器人")]
    [Description("告警机器人。钉钉、企业微信等")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("WebHook", "告警机器人。钉钉、企业微信等", "")]
    public String WebHook { get => _WebHook; set { if (OnPropertyChanging("WebHook", value)) { _WebHook = value; OnPropertyChanged("WebHook"); } } }

    private Boolean _AlarmOnOffline;
    /// <summary>下线告警。节点下线时，发送告警</summary>
    [Category("安全告警")]
    [DisplayName("下线告警")]
    [Description("下线告警。节点下线时，发送告警")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AlarmOnOffline", "下线告警。节点下线时，发送告警", "")]
    public Boolean AlarmOnOffline { get => _AlarmOnOffline; set { if (OnPropertyChanging("AlarmOnOffline", value)) { _AlarmOnOffline = value; OnPropertyChanged("AlarmOnOffline"); } } }

    private DateTime _LastLogin;
    /// <summary>最后登录</summary>
    [DisplayName("最后登录")]
    [Description("最后登录")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastLogin", "最后登录", "")]
    public DateTime LastLogin { get => _LastLogin; set { if (OnPropertyChanging("LastLogin", value)) { _LastLogin = value; OnPropertyChanged("LastLogin"); } } }

    private String _LastIP;
    /// <summary>最后IP</summary>
    [DisplayName("最后IP")]
    [Description("最后IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("LastIP", "最后IP", "")]
    public String LastIP { get => _LastIP; set { if (OnPropertyChanging("LastIP", value)) { _LastIP = value; OnPropertyChanged("LastIP"); } } }

    private String _AllowControlNodes;
    /// <summary>节点控制。安全性设计，允许该应用发指令控制的目标节点编码或目标应用名称，多节点逗号分隔，*表示全部节点</summary>
    [Category("安全告警")]
    [DisplayName("节点控制")]
    [Description("节点控制。安全性设计，允许该应用发指令控制的目标节点编码或目标应用名称，多节点逗号分隔，*表示全部节点")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("AllowControlNodes", "节点控制。安全性设计，允许该应用发指令控制的目标节点编码或目标应用名称，多节点逗号分隔，*表示全部节点", "")]
    public String AllowControlNodes { get => _AllowControlNodes; set { if (OnPropertyChanging("AllowControlNodes", value)) { _AllowControlNodes = value; OnPropertyChanged("AllowControlNodes"); } } }

    private String _Remark;
    /// <summary>内容</summary>
    [Category("扩展")]
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "内容", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private String _CreateUser;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

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

    private String _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

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
            "DisplayName" => _DisplayName,
            "Secret" => _Secret,
            "Category" => _Category,
            "Enable" => _Enable,
            "AutoActive" => _AutoActive,
            "Version" => _Version,
            "Compile" => _Compile,
            "Period" => _Period,
            "Singleton" => _Singleton,
            "WhiteIPs" => _WhiteIPs,
            "BlackIPs" => _BlackIPs,
            "WebHook" => _WebHook,
            "AlarmOnOffline" => _AlarmOnOffline,
            "LastLogin" => _LastLogin,
            "LastIP" => _LastIP,
            "AllowControlNodes" => _AllowControlNodes,
            "Remark" => _Remark,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUser" => _UpdateUser,
            "UpdateUserID" => _UpdateUserID,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Secret": _Secret = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "AutoActive": _AutoActive = value.ToBoolean(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "Compile": _Compile = value.ToDateTime(); break;
                case "Period": _Period = value.ToInt(); break;
                case "Singleton": _Singleton = value.ToBoolean(); break;
                case "WhiteIPs": _WhiteIPs = Convert.ToString(value); break;
                case "BlackIPs": _BlackIPs = Convert.ToString(value); break;
                case "WebHook": _WebHook = Convert.ToString(value); break;
                case "AlarmOnOffline": _AlarmOnOffline = value.ToBoolean(); break;
                case "LastLogin": _LastLogin = value.ToDateTime(); break;
                case "LastIP": _LastIP = Convert.ToString(value); break;
                case "AllowControlNodes": _AllowControlNodes = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
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

    #region 字段名
    /// <summary>取得应用系统字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>显示名</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>密钥</summary>
        public static readonly Field Secret = FindByName("Secret");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>自动激活。自动激活启用该应用在注册中心提供的服务，供其它消费者使用</summary>
        public static readonly Field AutoActive = FindByName("AutoActive");

        /// <summary>版本。多版本实例使用时，仅记录最大版本</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>编译时间。最大版本应用的编译时间</summary>
        public static readonly Field Compile = FindByName("Compile");

        /// <summary>采样周期。应用定期采样并上报性能数据，默认60秒</summary>
        public static readonly Field Period = FindByName("Period");

        /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于管理实例</summary>
        public static readonly Field Singleton = FindByName("Singleton");

        /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
        public static readonly Field WhiteIPs = FindByName("WhiteIPs");

        /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
        public static readonly Field BlackIPs = FindByName("BlackIPs");

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public static readonly Field WebHook = FindByName("WebHook");

        /// <summary>下线告警。节点下线时，发送告警</summary>
        public static readonly Field AlarmOnOffline = FindByName("AlarmOnOffline");

        /// <summary>最后登录</summary>
        public static readonly Field LastLogin = FindByName("LastLogin");

        /// <summary>最后IP</summary>
        public static readonly Field LastIP = FindByName("LastIP");

        /// <summary>节点控制。安全性设计，允许该应用发指令控制的目标节点编码或目标应用名称，多节点逗号分隔，*表示全部节点</summary>
        public static readonly Field AllowControlNodes = FindByName("AllowControlNodes");

        /// <summary>内容</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用系统字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>显示名</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>密钥</summary>
        public const String Secret = "Secret";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>自动激活。自动激活启用该应用在注册中心提供的服务，供其它消费者使用</summary>
        public const String AutoActive = "AutoActive";

        /// <summary>版本。多版本实例使用时，仅记录最大版本</summary>
        public const String Version = "Version";

        /// <summary>编译时间。最大版本应用的编译时间</summary>
        public const String Compile = "Compile";

        /// <summary>采样周期。应用定期采样并上报性能数据，默认60秒</summary>
        public const String Period = "Period";

        /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于管理实例</summary>
        public const String Singleton = "Singleton";

        /// <summary>IP白名单。符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</summary>
        public const String WhiteIPs = "WhiteIPs";

        /// <summary>IP黑名单。符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</summary>
        public const String BlackIPs = "BlackIPs";

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public const String WebHook = "WebHook";

        /// <summary>下线告警。节点下线时，发送告警</summary>
        public const String AlarmOnOffline = "AlarmOnOffline";

        /// <summary>最后登录</summary>
        public const String LastLogin = "LastLogin";

        /// <summary>最后IP</summary>
        public const String LastIP = "LastIP";

        /// <summary>节点控制。安全性设计，允许该应用发指令控制的目标节点编码或目标应用名称，多节点逗号分隔，*表示全部节点</summary>
        public const String AllowControlNodes = "AllowControlNodes";

        /// <summary>内容</summary>
        public const String Remark = "Remark";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新者</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";
    }
    #endregion
}
