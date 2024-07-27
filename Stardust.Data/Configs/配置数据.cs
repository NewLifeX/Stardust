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

/// <summary>配置数据。配置名值对，发布后才能生效，支持多作用域划分生产和开发测试环境</summary>
[Serializable]
[DataObject]
[Description("配置数据。配置名值对，发布后才能生效，支持多作用域划分生产和开发测试环境")]
[BindIndex("IU_ConfigData_ConfigId_Key_Scope", true, "ConfigId,Key,Scope")]
[BindTable("ConfigData", Description = "配置数据。配置名值对，发布后才能生效，支持多作用域划分生产和开发测试环境", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class ConfigData
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ConfigId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 ConfigId { get => _ConfigId; set { if (OnPropertyChanging("ConfigId", value)) { _ConfigId = value; OnPropertyChanged("ConfigId"); } } }

    private String _Key;
    /// <summary>名称。下划线开头表示仅用于内嵌，不能返回给客户端；多级名称用冒号分隔</summary>
    [DisplayName("名称")]
    [Description("名称。下划线开头表示仅用于内嵌，不能返回给客户端；多级名称用冒号分隔")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Key", "名称。下划线开头表示仅用于内嵌，不能返回给客户端；多级名称用冒号分隔", "", Master = true)]
    public String Key { get => _Key; set { if (OnPropertyChanging("Key", value)) { _Key = value; OnPropertyChanged("Key"); } } }

    private String _Scope;
    /// <summary>作用域</summary>
    [DisplayName("作用域")]
    [Description("作用域")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Scope", "作用域", "")]
    public String Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private String _Value;
    /// <summary>数值。正在使用的值，支持内嵌 ${key@app:scope}</summary>
    [DisplayName("数值")]
    [Description("数值。正在使用的值，支持内嵌 ${key@app:scope}")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Value", "数值。正在使用的值，支持内嵌 ${key@app:scope}", "")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Int32 _Version;
    /// <summary>版本。当前版本号，每次修改都是应用版本加一</summary>
    [DisplayName("版本")]
    [Description("版本。当前版本号，每次修改都是应用版本加一")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Version", "版本。当前版本号，每次修改都是应用版本加一", "")]
    public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _NewValue;
    /// <summary>期望值。已被修改，尚未发布的值，支持内嵌 ${key@app:scope}</summary>
    [DisplayName("期望值")]
    [Description("期望值。已被修改，尚未发布的值，支持内嵌 ${key@app:scope}")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("NewValue", "期望值。已被修改，尚未发布的值，支持内嵌 ${key@app:scope}", "")]
    public String NewValue { get => _NewValue; set { if (OnPropertyChanging("NewValue", value)) { _NewValue = value; OnPropertyChanged("NewValue"); } } }

    private Int32 _NewVersion;
    /// <summary>新版本。下一个将要发布的版本，发布后两者相同</summary>
    [DisplayName("新版本")]
    [Description("新版本。下一个将要发布的版本，发布后两者相同")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NewVersion", "新版本。下一个将要发布的版本，发布后两者相同", "")]
    public Int32 NewVersion { get => _NewVersion; set { if (OnPropertyChanging("NewVersion", value)) { _NewVersion = value; OnPropertyChanged("NewVersion"); } } }

    private String _NewStatus;
    /// <summary>新状态。启用/禁用/删除</summary>
    [DisplayName("新状态")]
    [Description("新状态。启用/禁用/删除")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("NewStatus", "新状态。启用/禁用/删除", "")]
    public String NewStatus { get => _NewStatus; set { if (OnPropertyChanging("NewStatus", value)) { _NewStatus = value; OnPropertyChanged("NewStatus"); } } }

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
            "ConfigId" => _ConfigId,
            "Key" => _Key,
            "Scope" => _Scope,
            "Value" => _Value,
            "Version" => _Version,
            "Enable" => _Enable,
            "NewValue" => _NewValue,
            "NewVersion" => _NewVersion,
            "NewStatus" => _NewStatus,
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
                case "ConfigId": _ConfigId = value.ToInt(); break;
                case "Key": _Key = Convert.ToString(value); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Version": _Version = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "NewValue": _NewValue = Convert.ToString(value); break;
                case "NewVersion": _NewVersion = value.ToInt(); break;
                case "NewStatus": _NewStatus = Convert.ToString(value); break;
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
    /// <summary>应用</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AppConfig Config => Extends.Get(nameof(Config), k => AppConfig.FindById(ConfigId));

    /// <summary>应用</summary>
    [Map(nameof(ConfigId), typeof(AppConfig), "Id")]
    public String ConfigName => Config?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据应用查找</summary>
    /// <param name="configId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<ConfigData> FindAllByConfigId(Int32 configId)
    {
        if (configId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ConfigId == configId);

        return FindAll(_.ConfigId == configId);
    }
    #endregion

    #region 字段名
    /// <summary>取得配置数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用</summary>
        public static readonly Field ConfigId = FindByName("ConfigId");

        /// <summary>名称。下划线开头表示仅用于内嵌，不能返回给客户端；多级名称用冒号分隔</summary>
        public static readonly Field Key = FindByName("Key");

        /// <summary>作用域</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>数值。正在使用的值，支持内嵌 ${key@app:scope}</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>版本。当前版本号，每次修改都是应用版本加一</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>期望值。已被修改，尚未发布的值，支持内嵌 ${key@app:scope}</summary>
        public static readonly Field NewValue = FindByName("NewValue");

        /// <summary>新版本。下一个将要发布的版本，发布后两者相同</summary>
        public static readonly Field NewVersion = FindByName("NewVersion");

        /// <summary>新状态。启用/禁用/删除</summary>
        public static readonly Field NewStatus = FindByName("NewStatus");

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

    /// <summary>取得配置数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用</summary>
        public const String ConfigId = "ConfigId";

        /// <summary>名称。下划线开头表示仅用于内嵌，不能返回给客户端；多级名称用冒号分隔</summary>
        public const String Key = "Key";

        /// <summary>作用域</summary>
        public const String Scope = "Scope";

        /// <summary>数值。正在使用的值，支持内嵌 ${key@app:scope}</summary>
        public const String Value = "Value";

        /// <summary>版本。当前版本号，每次修改都是应用版本加一</summary>
        public const String Version = "Version";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>期望值。已被修改，尚未发布的值，支持内嵌 ${key@app:scope}</summary>
        public const String NewValue = "NewValue";

        /// <summary>新版本。下一个将要发布的版本，发布后两者相同</summary>
        public const String NewVersion = "NewVersion";

        /// <summary>新状态。启用/禁用/删除</summary>
        public const String NewStatus = "NewStatus";

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
