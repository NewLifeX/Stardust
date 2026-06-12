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

/// <summary>产品版本。产品发布版本，一个版本包含多个面向不同.NET运行时的包</summary>
[Serializable]
[DataObject]
[Description("产品版本。产品发布版本，一个版本包含多个面向不同.NET运行时的包")]
[BindIndex("IU_ProductRelease_Version", true, "Version")]
[BindIndex("IX_ProductRelease_ProductCode", false, "ProductCode")]
[BindTable("ProductRelease", Description = "产品版本。产品发布版本，一个版本包含多个面向不同.NET运行时的包", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class ProductRelease
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Version;
    /// <summary>版本号。完整版本号，如 3.7.2026.0611</summary>
    [DisplayName("版本号")]
    [Description("版本号。完整版本号，如 3.7.2026.0611")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本号。完整版本号，如 3.7.2026.0611", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _ProductCode;
    /// <summary>产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品</summary>
    [DisplayName("产品编码")]
    [Description("产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ProductCode", "产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品", "")]
    public String ProductCode { get => _ProductCode; set { if (OnPropertyChanging("ProductCode", value)) { _ProductCode = value; OnPropertyChanged("ProductCode"); } } }

    private Boolean _Enable;
    /// <summary>启用。启用/停用</summary>
    [DisplayName("启用")]
    [Description("启用。启用/停用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。启用/停用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Force;
    /// <summary>强制。强制升级</summary>
    [DisplayName("强制")]
    [Description("强制。强制升级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Force", "强制。强制升级", "")]
    public Boolean Force { get => _Force; set { if (OnPropertyChanging("Force", value)) { _Force = value; OnPropertyChanged("Force"); } } }

    private NodeChannels _Channel;
    /// <summary>升级通道</summary>
    [DisplayName("升级通道")]
    [Description("升级通道")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Channel", "升级通道", "")]
    public NodeChannels Channel { get => _Channel; set { if (OnPropertyChanging("Channel", value)) { _Channel = value; OnPropertyChanged("Channel"); } } }

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
            "Version" => _Version,
            "ProductCode" => _ProductCode,
            "Enable" => _Enable,
            "Force" => _Force,
            "Channel" => _Channel,
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
                case "Version": _Version = Convert.ToString(value); break;
                case "ProductCode": _ProductCode = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Force": _Force = value.ToBoolean(); break;
                case "Channel": _Channel = (NodeChannels)value.ToInt(); break;
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
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ProductRelease FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据版本号查找</summary>
    /// <param name="version">版本号</param>
    /// <returns>实体对象</returns>
    public static ProductRelease FindByVersion(String version)
    {
        if (version.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Version.EqualIgnoreCase(version));

        return Find(_.Version == version);
    }

    /// <summary>根据产品编码查找</summary>
    /// <param name="productCode">产品编码</param>
    /// <returns>实体列表</returns>
    public static IList<ProductRelease> FindAllByProductCode(String productCode)
    {
        if (productCode.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProductCode.EqualIgnoreCase(productCode));

        return FindAll(_.ProductCode == productCode);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="version">版本号。完整版本号，如 3.7.2026.0611</param>
    /// <param name="productCode">产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品</param>
    /// <param name="force">强制。强制升级</param>
    /// <param name="channel">升级通道</param>
    /// <param name="enable">启用。启用/停用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ProductRelease> Search(String version, String productCode, Boolean? force, NodeChannels channel, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!version.IsNullOrEmpty()) exp &= _.Version == version;
        if (!productCode.IsNullOrEmpty()) exp &= _.ProductCode == productCode;
        if (force != null) exp &= _.Force == force;
        if (channel >= 0) exp &= _.Channel == channel;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得产品版本字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>版本号。完整版本号，如 3.7.2026.0611</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品</summary>
        public static readonly Field ProductCode = FindByName("ProductCode");

        /// <summary>启用。启用/停用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>强制。强制升级</summary>
        public static readonly Field Force = FindByName("Force");

        /// <summary>升级通道</summary>
        public static readonly Field Channel = FindByName("Channel");

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

    /// <summary>取得产品版本字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>版本号。完整版本号，如 3.7.2026.0611</summary>
        public const String Version = "Version";

        /// <summary>产品编码。StarAgent/CrazyCoder/XCoder 等，用于区分不同类型产品</summary>
        public const String ProductCode = "ProductCode";

        /// <summary>启用。启用/停用</summary>
        public const String Enable = "Enable";

        /// <summary>强制。强制升级</summary>
        public const String Force = "Force";

        /// <summary>升级通道</summary>
        public const String Channel = "Channel";

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
