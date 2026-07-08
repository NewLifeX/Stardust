using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Gateway;

/// <summary>网关证书。SSL证书配置，用于HTTPS终止和SNI匹配</summary>
/// <remarks>已废弃：请使用 Stardust.Data.Deployment.SslCertificate 统一管理证书。</remarks>
[Obsolete("已废弃，请使用 SslCertificate（星尘部署中心证书管理）")]
[Serializable]
[DataObject]
[Description("网关证书。SSL证书配置，用于HTTPS终止和SNI匹配")]
[BindIndex("IU_GatewayCert_Name", true, "Name")]
[BindIndex("IX_GatewayCert_Domain", false, "Domain")]
[BindTable("GatewayCert", Description = "网关证书。SSL证书配置，用于HTTPS终止和SNI匹配", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class GatewayCert
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "", DefaultValue = "True")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _Domain;
    /// <summary>域名。匹配SNI，支持通配符 *.example.com</summary>
    [DisplayName("域名")]
    [Description("域名。匹配SNI，支持通配符 *.example.com")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Domain", "域名。匹配SNI，支持通配符 *.example.com", "")]
    public String Domain { get => _Domain; set { if (OnPropertyChanging("Domain", value)) { _Domain = value; OnPropertyChanged("Domain"); } } }

    private String _CertFile;
    /// <summary>证书文件路径。PEM格式</summary>
    [DisplayName("证书文件路径")]
    [Description("证书文件路径。PEM格式")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("CertFile", "证书文件路径。PEM格式", "")]
    public String CertFile { get => _CertFile; set { if (OnPropertyChanging("CertFile", value)) { _CertFile = value; OnPropertyChanged("CertFile"); } } }

    private String _KeyFile;
    /// <summary>私钥文件路径。PEM格式</summary>
    [DisplayName("私钥文件路径")]
    [Description("私钥文件路径。PEM格式")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("KeyFile", "私钥文件路径。PEM格式", "")]
    public String KeyFile { get => _KeyFile; set { if (OnPropertyChanging("KeyFile", value)) { _KeyFile = value; OnPropertyChanged("KeyFile"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private String _CreateUser;
    /// <summary>创建者</summary>
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _UpdateUser;
    /// <summary>更新者</summary>
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
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
            "Name" => _Name,
            "Enable" => _Enable,
            "Domain" => _Domain,
            "CertFile" => _CertFile,
            "KeyFile" => _KeyFile,
            "Remark" => _Remark,
            "CreateUser" => _CreateUser,
            "CreateTime" => _CreateTime,
            "UpdateUser" => _UpdateUser,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Domain": _Domain = Convert.ToString(value); break;
                case "CertFile": _CertFile = Convert.ToString(value); break;
                case "KeyFile": _KeyFile = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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
    public static GatewayCert FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static GatewayCert FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as GatewayCert;

        //return Find(_.Name == name);
    }

    /// <summary>根据域名查找</summary>
    /// <param name="domain">域名</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayCert> FindAllByDomain(String domain)
    {
        if (domain.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Domain.EqualIgnoreCase(domain));

        return FindAll(_.Domain == domain);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="domain">域名。匹配SNI，支持通配符 *.example.com</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<GatewayCert> Search(String domain, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!domain.IsNullOrEmpty()) exp &= _.Domain == domain;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得网关证书字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>域名。匹配SNI，支持通配符 *.example.com</summary>
        public static readonly Field Domain = FindByName("Domain");

        /// <summary>证书文件路径。PEM格式</summary>
        public static readonly Field CertFile = FindByName("CertFile");

        /// <summary>私钥文件路径。PEM格式</summary>
        public static readonly Field KeyFile = FindByName("KeyFile");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得网关证书字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>域名。匹配SNI，支持通配符 *.example.com</summary>
        public const String Domain = "Domain";

        /// <summary>证书文件路径。PEM格式</summary>
        public const String CertFile = "CertFile";

        /// <summary>私钥文件路径。PEM格式</summary>
        public const String KeyFile = "KeyFile";

        /// <summary>备注</summary>
        public const String Remark = "Remark";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
