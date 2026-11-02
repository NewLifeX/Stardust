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
[Description("应用依赖")]
[BindTable("AppQuote", Description = "应用依赖。应用可以引用依赖另一个应用", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class AppQuote : Entity<AppQuote>
{
    #region 属性
    private Int32 _Id;
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AppId;
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private Int32 _TargetAppId;
    [DisplayName("依赖应用")]
    [Description("依赖应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TargetAppId", "依赖应用", "")]
    public Int32 TargetAppId { get => _TargetAppId; set { if (OnPropertyChanging("TargetAppId", value)) { _TargetAppId = value; OnPropertyChanged("TargetAppId"); } } }

    private Int32 _Kind;
    [DisplayName("类型")]
    [Description("类型")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Kind", "类型", "")]
    public Int32 Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private Boolean _Enable;
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private DateTime _CreateTime;
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
    #endregion

    #region 扩展查询
    /// <summary>按高级条件搜索</summary>
    public static IList<AppQuote> Search(Int32 appId, Int32 targetAppId, DateTime start, DateTime end, String key, PageParameter p)
    {
        var list = FindAll();
        if (appId > 0) list = list.Where(e => e.AppId == appId).ToList();
        if (targetAppId > 0) list = list.Where(e => e.TargetAppId == targetAppId).ToList();
        if (start > DateTime.MinValue) list = list.Where(e => e.CreateTime >= start).ToList();
        if (end > DateTime.MinValue) list = list.Where(e => e.CreateTime < end.Date.AddDays(1)).ToList();
        return list;
    }
    #endregion
}
