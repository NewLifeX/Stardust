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

namespace Stardust.Data.Monitors;

/// <summary>跟踪规则。全局黑白名单，白名单放行，黑名单拦截</summary>
[Serializable]
[DataObject]
[Description("跟踪规则。全局黑白名单，白名单放行，黑名单拦截")]
[BindTable("TraceRule", Description = "跟踪规则。全局黑白名单，白名单放行，黑名单拦截", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class TraceRule
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Rule;
    /// <summary>规则。支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）</summary>
    [DisplayName("规则")]
    [Description("规则。支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Rule", "规则。支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）", "")]
    public String Rule { get => _Rule; set { if (OnPropertyChanging("Rule", value)) { _Rule = value; OnPropertyChanged("Rule"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _IsWhite;
    /// <summary>白名单。否则是黑名单</summary>
    [DisplayName("白名单")]
    [Description("白名单。否则是黑名单")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsWhite", "白名单。否则是黑名单", "")]
    public Boolean IsWhite { get => _IsWhite; set { if (OnPropertyChanging("IsWhite", value)) { _IsWhite = value; OnPropertyChanged("IsWhite"); } } }

    private Boolean _IsRegex;
    /// <summary>正则。是否使用正则表达式，此时区分大小写</summary>
    [DisplayName("正则")]
    [Description("正则。是否使用正则表达式，此时区分大小写")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsRegex", "正则。是否使用正则表达式，此时区分大小写", "")]
    public Boolean IsRegex { get => _IsRegex; set { if (OnPropertyChanging("IsRegex", value)) { _IsRegex = value; OnPropertyChanged("IsRegex"); } } }

    private Int32 _Priority;
    /// <summary>优先级。越大越在前面</summary>
    [Category("扩展")]
    [DisplayName("优先级")]
    [Description("优先级。越大越在前面")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "优先级。越大越在前面", "")]
    public Int32 Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新人</summary>
    [Category("扩展")]
    [DisplayName("更新人")]
    [Description("更新人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新人", "")]
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
            "Rule" => _Rule,
            "Enable" => _Enable,
            "IsWhite" => _IsWhite,
            "IsRegex" => _IsRegex,
            "Priority" => _Priority,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
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
                case "Rule": _Rule = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "IsWhite": _IsWhite = value.ToBoolean(); break;
                case "IsRegex": _IsRegex = value.ToBoolean(); break;
                case "Priority": _Priority = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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
    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="isWhite">白名单。否则是黑名单</param>
    /// <param name="isRegex">正则。是否使用正则表达式，此时区分大小写</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TraceRule> Search(Boolean? isWhite, Boolean? isRegex, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (isWhite != null) exp &= _.IsWhite == isWhite;
        if (isRegex != null) exp &= _.IsRegex == isRegex;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得跟踪规则字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>规则。支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）</summary>
        public static readonly Field Rule = FindByName("Rule");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>白名单。否则是黑名单</summary>
        public static readonly Field IsWhite = FindByName("IsWhite");

        /// <summary>正则。是否使用正则表达式，此时区分大小写</summary>
        public static readonly Field IsRegex = FindByName("IsRegex");

        /// <summary>优先级。越大越在前面</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新人</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得跟踪规则字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>规则。支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）</summary>
        public const String Rule = "Rule";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>白名单。否则是黑名单</summary>
        public const String IsWhite = "IsWhite";

        /// <summary>正则。是否使用正则表达式，此时区分大小写</summary>
        public const String IsRegex = "IsRegex";

        /// <summary>优先级。越大越在前面</summary>
        public const String Priority = "Priority";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新人</summary>
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
