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

/// <summary>节点规则。根据IP规则，自动识别匹配节点名称</summary>
[Serializable]
[DataObject]
[Description("节点规则。根据IP规则，自动识别匹配节点名称")]
[BindTable("NodeRule", Description = "节点规则。根据IP规则，自动识别匹配节点名称", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class NodeRule
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
    /// <summary>规则。支持*模糊匹配，比如10.0.*</summary>
    [DisplayName("规则")]
    [Description("规则。支持*模糊匹配，比如10.0.*")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Rule", "规则。支持*模糊匹配，比如10.0.*", "")]
    public String Rule { get => _Rule; set { if (OnPropertyChanging("Rule", value)) { _Rule = value; OnPropertyChanged("Rule"); } } }

    private String _Name;
    /// <summary>名称。匹配规则的节点所应该具有的名称</summary>
    [DisplayName("名称")]
    [Description("名称。匹配规则的节点所应该具有的名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。匹配规则的节点所应该具有的名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>分类。匹配规则的节点所应该具有的分类</summary>
    [DisplayName("分类")]
    [Description("分类。匹配规则的节点所应该具有的分类")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类。匹配规则的节点所应该具有的分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Priority;
    /// <summary>优先级。数字越大优先级越高</summary>
    [DisplayName("优先级")]
    [Description("优先级。数字越大优先级越高")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Priority", "优先级。数字越大优先级越高", "")]
    public Int32 Priority { get => _Priority; set { if (OnPropertyChanging("Priority", value)) { _Priority = value; OnPropertyChanged("Priority"); } } }

    private Boolean _NewNode;
    /// <summary>新节点。新匹配IP如果不存在节点，则新建节点</summary>
    [DisplayName("新节点")]
    [Description("新节点。新匹配IP如果不存在节点，则新建节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NewNode", "新节点。新匹配IP如果不存在节点，则新建节点", "")]
    public Boolean NewNode { get => _NewNode; set { if (OnPropertyChanging("NewNode", value)) { _NewNode = value; OnPropertyChanged("NewNode"); } } }

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
            "Rule" => _Rule,
            "Name" => _Name,
            "Category" => _Category,
            "Enable" => _Enable,
            "Priority" => _Priority,
            "NewNode" => _NewNode,
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
                case "Rule": _Rule = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Priority": _Priority = value.ToInt(); break;
                case "NewNode": _NewNode = value.ToBoolean(); break;
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
    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="newNode">新节点。新匹配IP如果不存在节点，则新建节点</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<NodeRule> Search(Boolean? newNode, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (newNode != null) exp &= _.NewNode == newNode;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得节点规则字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>规则。支持*模糊匹配，比如10.0.*</summary>
        public static readonly Field Rule = FindByName("Rule");

        /// <summary>名称。匹配规则的节点所应该具有的名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>分类。匹配规则的节点所应该具有的分类</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>优先级。数字越大优先级越高</summary>
        public static readonly Field Priority = FindByName("Priority");

        /// <summary>新节点。新匹配IP如果不存在节点，则新建节点</summary>
        public static readonly Field NewNode = FindByName("NewNode");

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

    /// <summary>取得节点规则字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>规则。支持*模糊匹配，比如10.0.*</summary>
        public const String Rule = "Rule";

        /// <summary>名称。匹配规则的节点所应该具有的名称</summary>
        public const String Name = "Name";

        /// <summary>分类。匹配规则的节点所应该具有的分类</summary>
        public const String Category = "Category";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>优先级。数字越大优先级越高</summary>
        public const String Priority = "Priority";

        /// <summary>新节点。新匹配IP如果不存在节点，则新建节点</summary>
        public const String NewNode = "NewNode";

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
