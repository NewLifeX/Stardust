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

/// <summary>域名供应商。域名对应的DNS供应商凭据配置</summary>
[Serializable]
[DataObject]
[Description("域名供应商。域名对应的DNS供应商凭据配置")]
[BindIndex("IX_DomainProvider_Domain", false, "Domain")]
[BindIndex("IX_DomainProvider_UpdateTime", false, "UpdateTime")]
[BindTable("DomainProvider", Description = "域名供应商。域名对应的DNS供应商凭据配置", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class DomainProvider
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Provider;
    /// <summary>供应商。阿里云Aliyun，腾讯云TencentCloud，优刻得UCloud</summary>
    [DisplayName("供应商")]
    [Description("供应商。阿里云Aliyun，腾讯云TencentCloud，优刻得UCloud")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "供应商。阿里云Aliyun，腾讯云TencentCloud，优刻得UCloud", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String _Name;
    /// <summary>名称。凭据名称，如阿里云主账号</summary>
    [DisplayName("名称")]
    [Description("名称。凭据名称，如阿里云主账号")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "名称。凭据名称，如阿里云主账号", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _AppKey;
    /// <summary>AppKey。AccessKeyId/SecretId</summary>
    [DisplayName("AppKey")]
    [Description("AppKey。AccessKeyId/SecretId")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("AppKey", "AppKey。AccessKeyId/SecretId", "")]
    public String AppKey { get => _AppKey; set { if (OnPropertyChanging("AppKey", value)) { _AppKey = value; OnPropertyChanged("AppKey"); } } }

    private String _AppSecret;
    /// <summary>AppSecret。AccessKeySecret/SecretKey</summary>
    [DisplayName("AppSecret")]
    [Description("AppSecret。AccessKeySecret/SecretKey")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AppSecret", "AppSecret。AccessKeySecret/SecretKey", "")]
    public String AppSecret { get => _AppSecret; set { if (OnPropertyChanging("AppSecret", value)) { _AppSecret = value; OnPropertyChanged("AppSecret"); } } }

    private String _Domain;
    /// <summary>域名。管理的根域名，如newlifex.com</summary>
    [DisplayName("域名")]
    [Description("域名。管理的根域名，如newlifex.com")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Domain", "域名。管理的根域名，如newlifex.com", "")]
    public String Domain { get => _Domain; set { if (OnPropertyChanging("Domain", value)) { _Domain = value; OnPropertyChanged("Domain"); } } }

    private String _Endpoint;
    /// <summary>API端点。为空使用供应商默认值</summary>
    [DisplayName("API端点")]
    [Description("API端点。为空使用供应商默认值")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Endpoint", "API端点。为空使用供应商默认值", "")]
    public String Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private String _DNSZoneId;
    /// <summary>DNS Zone ID。UCloud必填，其他供应商留空</summary>
    [DisplayName("DNSZoneID")]
    [Description("DNS Zone ID。UCloud必填，其他供应商留空")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("DNSZoneId", "DNS Zone ID。UCloud必填，其他供应商留空", "")]
    public String DNSZoneId { get => _DNSZoneId; set { if (OnPropertyChanging("DNSZoneId", value)) { _DNSZoneId = value; OnPropertyChanged("DNSZoneId"); } } }

    private String _Region;
    /// <summary>地域。UCloud使用，默认cn-bj2</summary>
    [DisplayName("地域")]
    [Description("地域。UCloud使用，默认cn-bj2")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Region", "地域。UCloud使用，默认cn-bj2", "")]
    public String Region { get => _Region; set { if (OnPropertyChanging("Region", value)) { _Region = value; OnPropertyChanged("Region"); } } }

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
            "Provider" => _Provider,
            "Name" => _Name,
            "AppKey" => _AppKey,
            "AppSecret" => _AppSecret,
            "Domain" => _Domain,
            "Endpoint" => _Endpoint,
            "DNSZoneId" => _DNSZoneId,
            "Region" => _Region,
            "Enable" => _Enable,
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
                case "Provider": _Provider = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "AppKey": _AppKey = Convert.ToString(value); break;
                case "AppSecret": _AppSecret = Convert.ToString(value); break;
                case "Domain": _Domain = Convert.ToString(value); break;
                case "Endpoint": _Endpoint = Convert.ToString(value); break;
                case "DNSZoneId": _DNSZoneId = Convert.ToString(value); break;
                case "Region": _Region = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
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
    public static DomainProvider FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据域名查找</summary>
    /// <param name="domain">域名</param>
    /// <returns>实体列表</returns>
    public static IList<DomainProvider> FindAllByDomain(String domain)
    {
        if (domain.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Domain.EqualIgnoreCase(domain));

        return FindAll(_.Domain == domain);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="domain">域名。管理的根域名，如newlifex.com</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DomainProvider> Search(String domain, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
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
    /// <summary>取得域名供应商字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>供应商。阿里云Aliyun，腾讯云TencentCloud，优刻得UCloud</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>名称。凭据名称，如阿里云主账号</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>AppKey。AccessKeyId/SecretId</summary>
        public static readonly Field AppKey = FindByName("AppKey");

        /// <summary>AppSecret。AccessKeySecret/SecretKey</summary>
        public static readonly Field AppSecret = FindByName("AppSecret");

        /// <summary>域名。管理的根域名，如newlifex.com</summary>
        public static readonly Field Domain = FindByName("Domain");

        /// <summary>API端点。为空使用供应商默认值</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>DNS Zone ID。UCloud必填，其他供应商留空</summary>
        public static readonly Field DNSZoneId = FindByName("DNSZoneId");

        /// <summary>地域。UCloud使用，默认cn-bj2</summary>
        public static readonly Field Region = FindByName("Region");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

    /// <summary>取得域名供应商字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>供应商。阿里云Aliyun，腾讯云TencentCloud，优刻得UCloud</summary>
        public const String Provider = "Provider";

        /// <summary>名称。凭据名称，如阿里云主账号</summary>
        public const String Name = "Name";

        /// <summary>AppKey。AccessKeyId/SecretId</summary>
        public const String AppKey = "AppKey";

        /// <summary>AppSecret。AccessKeySecret/SecretKey</summary>
        public const String AppSecret = "AppSecret";

        /// <summary>域名。管理的根域名，如newlifex.com</summary>
        public const String Domain = "Domain";

        /// <summary>API端点。为空使用供应商默认值</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>DNS Zone ID。UCloud必填，其他供应商留空</summary>
        public const String DNSZoneId = "DNSZoneId";

        /// <summary>地域。UCloud使用，默认cn-bj2</summary>
        public const String Region = "Region";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
