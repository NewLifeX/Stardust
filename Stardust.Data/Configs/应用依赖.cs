using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Configs;

/// <summary>应用依赖。应用可以引用依赖另一个应用</summary>
[Serializable]
[DataObject]
[Description("应用依赖。应用可以引用依赖另一个应用")]
[BindTable("AppQuote", Description = "应用依赖。应用可以引用依赖另一个应用", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppQuote
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AppId;
    /// <summary>应用。原始应用，该应用依赖别人</summary>
    [DisplayName("应用")]
    [Description("应用。原始应用，该应用依赖别人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用。原始应用，该应用依赖别人", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int32 _TargetAppId;
    /// <summary>目标应用。被原始应用依赖的应用</summary>
    [DisplayName("目标应用")]
    [Description("目标应用。被原始应用依赖的应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TargetAppId", "目标应用。被原始应用依赖的应用", "")]
    public Int32 TargetAppId { get => _TargetAppId; set { if (OnPropertyChanging("TargetAppId", value)) { _TargetAppId = value; OnPropertyChanged("TargetAppId"); } } }

    private Int32 _Kind;
    /// <summary>类型</summary>
    [DisplayName("类型")]
    [Description("类型")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Kind", "类型", "")]
    public Int32 Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值。一个实体类的属性可以直接对应数据库表的一个字段，也可以是数据库多个字段的聚合；实体属性也可以是数据库表不存在字段的封装</summary>
    /// <param name="name">字段名</param>
    /// <returns>字段值，如果不存在此字段则返回null</returns>
    public override Object this[String name]
    {
        get
        {
            switch (name)
            {
                case "Id": return _Id;
                case "AppId": return _AppId;
                case "TargetAppId": return _TargetAppId;
                case "Kind": return _Kind;
                case "Enable": return _Enable;
                case "CreateUserID": return _CreateUserID;
                case "CreateTime": return _CreateTime;
                case "CreateIP": return _CreateIP;
                default: return base[name];
            }
        }
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "TargetAppId": _TargetAppId = value.ToInt(); break;
                case "Kind": _Kind = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    /// <summary>字段名短路函数，提高表达式书写效率</summary>
    public class _
    {
        /// <summary>编号</summary>
        public static readonly FieldItem Id = FindField("Id");

        /// <summary>应用。原始应用，该应用依赖别人>
        public static readonly FieldItem AppId = FindField("AppId");

        /// <summary>目标应用。被原始应用依赖的应用</summary>
        public static readonly FieldItem TargetAppId = FindField("TargetAppId");

        /// <summary>类型</summary>
        public static readonly FieldItem Kind = FindField("Kind");

        /// <summary>启用</summary>
        public static readonly FieldItem Enable = FindField("Enable");

        /// <summary>创建者</summary>
        public static readonly FieldItem CreateUserID = FindField("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly FieldItem CreateTime = FindField("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly FieldItem CreateIP = FindField("CreateIP");

        static FieldItem FindField(String name) => Entity<AppQuote>.Meta.Factory.Fields.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
    }
    #endregion
}
