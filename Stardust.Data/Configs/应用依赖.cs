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

/// <summary>应用依赖。应用可以引用依赖另一个应用</summary>
[Serializable]
[DataObject]
[Description("应用依赖。应用可以引用依赖另一个应用")]
[BindIndex("IX_AppQuote_AppId", false, "AppId")]
[BindIndex("IX_AppQuote_TargetAppId", false, "TargetAppId")]
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
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "AppId" => _AppId,
            "TargetAppId" => _TargetAppId,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "TargetAppId": _TargetAppId = value.ToInt(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    #endregion

    #region 字段名
    /// <summary>取得应用依赖字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用。原始应用，该应用依赖别人</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>目标应用。被原始应用依赖的应用</summary>
        public static readonly Field TargetAppId = FindByName("TargetAppId");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用依赖字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用。原始应用，该应用依赖别人</summary>
        public const String AppId = "AppId";

        /// <summary>目标应用。被原始应用依赖的应用</summary>
        public const String TargetAppId = "TargetAppId";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
