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

/// <summary>服务信息。服务提供者发布的服务</summary>
[Serializable]
[DataObject]
[Description("服务信息。服务提供者发布的服务")]
[BindIndex("IU_Service_Name", true, "Name")]
[BindIndex("IX_Service_ProjectId", false, "ProjectId")]
[BindTable("Service", Description = "服务信息。服务提供者发布的服务", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class Service
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
    /// <summary>名称。服务名，提供一个地址，包含多个接口</summary>
    [DisplayName("名称")]
    [Description("名称。服务名，提供一个地址，包含多个接口")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称。服务名，提供一个地址，包含多个接口", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _DisplayName;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "显示名", "")]
    public String DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

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

    private Boolean _Extranet;
    /// <summary>外网。外网服务使用提供者公网地址进行注册</summary>
    [DisplayName("外网")]
    [Description("外网。外网服务使用提供者公网地址进行注册")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Extranet", "外网。外网服务使用提供者公网地址进行注册", "")]
    public Boolean Extranet { get => _Extranet; set { if (OnPropertyChanging("Extranet", value)) { _Extranet = value; OnPropertyChanged("Extranet"); } } }

    private Boolean _Singleton;
    /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于设置权重</summary>
    [DisplayName("单例")]
    [Description("单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于设置权重")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Singleton", "单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于设置权重", "")]
    public Boolean Singleton { get => _Singleton; set { if (OnPropertyChanging("Singleton", value)) { _Singleton = value; OnPropertyChanged("Singleton"); } } }

    private Boolean _UseScope;
    /// <summary>作用域。使用作用域隔离后，消费者只能使用本作用域内的服务</summary>
    [DisplayName("作用域")]
    [Description("作用域。使用作用域隔离后，消费者只能使用本作用域内的服务")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UseScope", "作用域。使用作用域隔离后，消费者只能使用本作用域内的服务", "")]
    public Boolean UseScope { get => _UseScope; set { if (OnPropertyChanging("UseScope", value)) { _UseScope = value; OnPropertyChanged("UseScope"); } } }

    private String _Address;
    /// <summary>服务地址模版。固定的网关地址，或地址模版如http://{IP}:{Port}，默认不填写，自动识别地址</summary>
    [DisplayName("服务地址模版")]
    [Description("服务地址模版。固定的网关地址，或地址模版如http://{IP}:{Port}，默认不填写，自动识别地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Address", "服务地址模版。固定的网关地址，或地址模版如http://{IP}:{Port}，默认不填写，自动识别地址", "")]
    public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

    private Boolean _HealthCheck;
    /// <summary>健康监测。定时检测服务是否可用</summary>
    [DisplayName("健康监测")]
    [Description("健康监测。定时检测服务是否可用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("HealthCheck", "健康监测。定时检测服务是否可用", "")]
    public Boolean HealthCheck { get => _HealthCheck; set { if (OnPropertyChanging("HealthCheck", value)) { _HealthCheck = value; OnPropertyChanged("HealthCheck"); } } }

    private String _HealthAddress;
    /// <summary>监测地址。健康监测接口地址，相对地址或绝对地址</summary>
    [DisplayName("监测地址")]
    [Description("监测地址。健康监测接口地址，相对地址或绝对地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("HealthAddress", "监测地址。健康监测接口地址，相对地址或绝对地址", "")]
    public String HealthAddress { get => _HealthAddress; set { if (OnPropertyChanging("HealthAddress", value)) { _HealthAddress = value; OnPropertyChanged("HealthAddress"); } } }

    private Int32 _Providers;
    /// <summary>提供者。数量</summary>
    [DisplayName("提供者")]
    [Description("提供者。数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Providers", "提供者。数量", "")]
    public Int32 Providers { get => _Providers; set { if (OnPropertyChanging("Providers", value)) { _Providers = value; OnPropertyChanged("Providers"); } } }

    private Int32 _Consumers;
    /// <summary>消费者。数量</summary>
    [DisplayName("消费者")]
    [Description("消费者。数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Consumers", "消费者。数量", "")]
    public Int32 Consumers { get => _Consumers; set { if (OnPropertyChanging("Consumers", value)) { _Consumers = value; OnPropertyChanged("Consumers"); } } }

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

    private String _Remark;
    /// <summary>内容</summary>
    [Category("扩展")]
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "内容", "")]
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
            "DisplayName" => _DisplayName,
            "Category" => _Category,
            "Enable" => _Enable,
            "Extranet" => _Extranet,
            "Singleton" => _Singleton,
            "UseScope" => _UseScope,
            "Address" => _Address,
            "HealthCheck" => _HealthCheck,
            "HealthAddress" => _HealthAddress,
            "Providers" => _Providers,
            "Consumers" => _Consumers,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUser" => _UpdateUser,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Extranet": _Extranet = value.ToBoolean(); break;
                case "Singleton": _Singleton = value.ToBoolean(); break;
                case "UseScope": _UseScope = value.ToBoolean(); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "HealthCheck": _HealthCheck = value.ToBoolean(); break;
                case "HealthAddress": _HealthAddress = Convert.ToString(value); break;
                case "Providers": _Providers = value.ToInt(); break;
                case "Consumers": _Consumers = value.ToInt(); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
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

    #region 字段名
    /// <summary>取得服务信息字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目。资源归属的团队</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称。服务名，提供一个地址，包含多个接口</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>显示名</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>类别</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>外网。外网服务使用提供者公网地址进行注册</summary>
        public static readonly Field Extranet = FindByName("Extranet");

        /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于设置权重</summary>
        public static readonly Field Singleton = FindByName("Singleton");

        /// <summary>作用域。使用作用域隔离后，消费者只能使用本作用域内的服务</summary>
        public static readonly Field UseScope = FindByName("UseScope");

        /// <summary>服务地址模版。固定的网关地址，或地址模版如http://{IP}:{Port}，默认不填写，自动识别地址</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>健康监测。定时检测服务是否可用</summary>
        public static readonly Field HealthCheck = FindByName("HealthCheck");

        /// <summary>监测地址。健康监测接口地址，相对地址或绝对地址</summary>
        public static readonly Field HealthAddress = FindByName("HealthAddress");

        /// <summary>提供者。数量</summary>
        public static readonly Field Providers = FindByName("Providers");

        /// <summary>消费者。数量</summary>
        public static readonly Field Consumers = FindByName("Consumers");

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

        /// <summary>内容</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得服务信息字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>项目。资源归属的团队</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称。服务名，提供一个地址，包含多个接口</summary>
        public const String Name = "Name";

        /// <summary>显示名</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>类别</summary>
        public const String Category = "Category";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>外网。外网服务使用提供者公网地址进行注册</summary>
        public const String Extranet = "Extranet";

        /// <summary>单例。每个节点只部署一个实例，多节点多实例，此时使用本地IP作为唯一标识，便于设置权重</summary>
        public const String Singleton = "Singleton";

        /// <summary>作用域。使用作用域隔离后，消费者只能使用本作用域内的服务</summary>
        public const String UseScope = "UseScope";

        /// <summary>服务地址模版。固定的网关地址，或地址模版如http://{IP}:{Port}，默认不填写，自动识别地址</summary>
        public const String Address = "Address";

        /// <summary>健康监测。定时检测服务是否可用</summary>
        public const String HealthCheck = "HealthCheck";

        /// <summary>监测地址。健康监测接口地址，相对地址或绝对地址</summary>
        public const String HealthAddress = "HealthAddress";

        /// <summary>提供者。数量</summary>
        public const String Providers = "Providers";

        /// <summary>消费者。数量</summary>
        public const String Consumers = "Consumers";

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

        /// <summary>内容</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
