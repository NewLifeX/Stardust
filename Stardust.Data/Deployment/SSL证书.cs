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

namespace Stardust.Data.Deployment;

/// <summary>SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出</summary>
[Serializable]
[DataObject]
[Description("SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出")]
[BindIndex("IX_SslCertificate_Domain_NotAfter", false, "Domain,NotAfter")]
[BindIndex("IX_SslCertificate_NotAfter", false, "NotAfter")]
[BindTable("SslCertificate", Description = "SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class SslCertificate
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Domain;
    /// <summary>域名。支持通配符，如*.newlifex.com，用于匹配应用Urls</summary>
    [DisplayName("域名")]
    [Description("域名。支持通配符，如*.newlifex.com，用于匹配应用Urls")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Domain", "域名。支持通配符，如*.newlifex.com，用于匹配应用Urls", "", Master = true)]
    public String Domain { get => _Domain; set { if (OnPropertyChanging("Domain", value)) { _Domain = value; OnPropertyChanged("Domain"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _PfxFile;
    /// <summary>PFX文件。Windows/IIS使用，包含私钥</summary>
    [DisplayName("PFX文件")]
    [Description("PFX文件。Windows/IIS使用，包含私钥")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PfxFile", "PFX文件。Windows/IIS使用，包含私钥", "", ItemType = "file")]
    public String PfxFile { get => _PfxFile; set { if (OnPropertyChanging("PfxFile", value)) { _PfxFile = value; OnPropertyChanged("PfxFile"); } } }

    private String _PfxPassword;
    /// <summary>PFX密码。加密存储</summary>
    [DisplayName("PFX密码")]
    [Description("PFX密码。加密存储")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PfxPassword", "PFX密码。加密存储", "")]
    public String PfxPassword { get => _PfxPassword; set { if (OnPropertyChanging("PfxPassword", value)) { _PfxPassword = value; OnPropertyChanged("PfxPassword"); } } }

    private String _CrtFile;
    /// <summary>证书文件。Linux/Nginx使用</summary>
    [DisplayName("证书文件")]
    [Description("证书文件。Linux/Nginx使用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CrtFile", "证书文件。Linux/Nginx使用", "", ItemType = "file")]
    public String CrtFile { get => _CrtFile; set { if (OnPropertyChanging("CrtFile", value)) { _CrtFile = value; OnPropertyChanged("CrtFile"); } } }

    private String _KeyFile;
    /// <summary>私钥文件。Linux/Nginx使用</summary>
    [DisplayName("私钥文件")]
    [Description("私钥文件。Linux/Nginx使用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("KeyFile", "私钥文件。Linux/Nginx使用", "", ItemType = "file")]
    public String KeyFile { get => _KeyFile; set { if (OnPropertyChanging("KeyFile", value)) { _KeyFile = value; OnPropertyChanged("KeyFile"); } } }

    private String _PemFile;
    /// <summary>PEM文件。通用格式，合并crt和key</summary>
    [DisplayName("PEM文件")]
    [Description("PEM文件。通用格式，合并crt和key")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("PemFile", "PEM文件。通用格式，合并crt和key", "", ItemType = "file")]
    public String PemFile { get => _PemFile; set { if (OnPropertyChanging("PemFile", value)) { _PemFile = value; OnPropertyChanged("PemFile"); } } }

    private String _Issuer;
    /// <summary>颁发者。如Let's Encrypt Authority X3</summary>
    [DisplayName("颁发者")]
    [Description("颁发者。如Let's Encrypt Authority X3")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Issuer", "颁发者。如Let's Encrypt Authority X3", "")]
    public String Issuer { get => _Issuer; set { if (OnPropertyChanging("Issuer", value)) { _Issuer = value; OnPropertyChanged("Issuer"); } } }

    private String _Subject;
    /// <summary>使用者。证书主题DN</summary>
    [DisplayName("使用者")]
    [Description("使用者。证书主题DN")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Subject", "使用者。证书主题DN", "")]
    public String Subject { get => _Subject; set { if (OnPropertyChanging("Subject", value)) { _Subject = value; OnPropertyChanged("Subject"); } } }

    private DateTime _NotBefore;
    /// <summary>生效时间</summary>
    [DisplayName("生效时间")]
    [Description("生效时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("NotBefore", "生效时间", "")]
    public DateTime NotBefore { get => _NotBefore; set { if (OnPropertyChanging("NotBefore", value)) { _NotBefore = value; OnPropertyChanged("NotBefore"); } } }

    private DateTime _NotAfter;
    /// <summary>过期时间。用于自动告警和续期</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。用于自动告警和续期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("NotAfter", "过期时间。用于自动告警和续期", "")]
    public DateTime NotAfter { get => _NotAfter; set { if (OnPropertyChanging("NotAfter", value)) { _NotAfter = value; OnPropertyChanged("NotAfter"); } } }

    private String _Thumbprint;
    /// <summary>指纹。SHA1哈希，用于唯一标识</summary>
    [DisplayName("指纹")]
    [Description("指纹。SHA1哈希，用于唯一标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Thumbprint", "指纹。SHA1哈希，用于唯一标识", "")]
    public String Thumbprint { get => _Thumbprint; set { if (OnPropertyChanging("Thumbprint", value)) { _Thumbprint = value; OnPropertyChanged("Thumbprint"); } } }

    private Boolean _AutoRenew;
    /// <summary>自动续期。集成Let's Encrypt/阿里云等</summary>
    [DisplayName("自动续期")]
    [Description("自动续期。集成Let's Encrypt/阿里云等")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AutoRenew", "自动续期。集成Let's Encrypt/阿里云等", "")]
    public Boolean AutoRenew { get => _AutoRenew; set { if (OnPropertyChanging("AutoRenew", value)) { _AutoRenew = value; OnPropertyChanged("AutoRenew"); } } }

    private String _Provider;
    /// <summary>证书提供商。letsencrypt/aliyun/tencent/manual</summary>
    [DisplayName("证书提供商")]
    [Description("证书提供商。letsencrypt/aliyun/tencent/manual")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "证书提供商。letsencrypt/aliyun/tencent/manual", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private Int32 _RenewDays;
    /// <summary>续期提前天数。过期前N天自动续期</summary>
    [DisplayName("续期提前天数")]
    [Description("续期提前天数。过期前N天自动续期")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RenewDays", "续期提前天数。过期前N天自动续期", "", DefaultValue = "30")]
    public Int32 RenewDays { get => _RenewDays; set { if (OnPropertyChanging("RenewDays", value)) { _RenewDays = value; OnPropertyChanged("RenewDays"); } } }

    private DateTime _LastRenew;
    /// <summary>最后续期时间</summary>
    [DisplayName("最后续期时间")]
    [Description("最后续期时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastRenew", "最后续期时间", "")]
    public DateTime LastRenew { get => _LastRenew; set { if (OnPropertyChanging("LastRenew", value)) { _LastRenew = value; OnPropertyChanged("LastRenew"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者", "")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

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

    private Int32 _UpdateUserId;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "更新者", "")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

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
            "Domain" => _Domain,
            "Enable" => _Enable,
            "PfxFile" => _PfxFile,
            "PfxPassword" => _PfxPassword,
            "CrtFile" => _CrtFile,
            "KeyFile" => _KeyFile,
            "PemFile" => _PemFile,
            "Issuer" => _Issuer,
            "Subject" => _Subject,
            "NotBefore" => _NotBefore,
            "NotAfter" => _NotAfter,
            "Thumbprint" => _Thumbprint,
            "AutoRenew" => _AutoRenew,
            "Provider" => _Provider,
            "RenewDays" => _RenewDays,
            "LastRenew" => _LastRenew,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserId" => _UpdateUserId,
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
                case "Domain": _Domain = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "PfxFile": _PfxFile = Convert.ToString(value); break;
                case "PfxPassword": _PfxPassword = Convert.ToString(value); break;
                case "CrtFile": _CrtFile = Convert.ToString(value); break;
                case "KeyFile": _KeyFile = Convert.ToString(value); break;
                case "PemFile": _PemFile = Convert.ToString(value); break;
                case "Issuer": _Issuer = Convert.ToString(value); break;
                case "Subject": _Subject = Convert.ToString(value); break;
                case "NotBefore": _NotBefore = value.ToDateTime(); break;
                case "NotAfter": _NotAfter = value.ToDateTime(); break;
                case "Thumbprint": _Thumbprint = Convert.ToString(value); break;
                case "AutoRenew": _AutoRenew = value.ToBoolean(); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "RenewDays": _RenewDays = value.ToInt(); break;
                case "LastRenew": _LastRenew = value.ToDateTime(); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
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
    public static SslCertificate FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="autoRenew">自动续期。集成Let's Encrypt/阿里云等</param>
    /// <param name="enable">启用</param>
    /// <param name="start">过期时间开始</param>
    /// <param name="end">过期时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SslCertificate> Search(Boolean? autoRenew, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (autoRenew != null) exp &= _.AutoRenew == autoRenew;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.NotAfter.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得SSL证书字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>域名。支持通配符，如*.newlifex.com，用于匹配应用Urls</summary>
        public static readonly Field Domain = FindByName("Domain");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>PFX文件。Windows/IIS使用，包含私钥</summary>
        public static readonly Field PfxFile = FindByName("PfxFile");

        /// <summary>PFX密码。加密存储</summary>
        public static readonly Field PfxPassword = FindByName("PfxPassword");

        /// <summary>证书文件。Linux/Nginx使用</summary>
        public static readonly Field CrtFile = FindByName("CrtFile");

        /// <summary>私钥文件。Linux/Nginx使用</summary>
        public static readonly Field KeyFile = FindByName("KeyFile");

        /// <summary>PEM文件。通用格式，合并crt和key</summary>
        public static readonly Field PemFile = FindByName("PemFile");

        /// <summary>颁发者。如Let's Encrypt Authority X3</summary>
        public static readonly Field Issuer = FindByName("Issuer");

        /// <summary>使用者。证书主题DN</summary>
        public static readonly Field Subject = FindByName("Subject");

        /// <summary>生效时间</summary>
        public static readonly Field NotBefore = FindByName("NotBefore");

        /// <summary>过期时间。用于自动告警和续期</summary>
        public static readonly Field NotAfter = FindByName("NotAfter");

        /// <summary>指纹。SHA1哈希，用于唯一标识</summary>
        public static readonly Field Thumbprint = FindByName("Thumbprint");

        /// <summary>自动续期。集成Let's Encrypt/阿里云等</summary>
        public static readonly Field AutoRenew = FindByName("AutoRenew");

        /// <summary>证书提供商。letsencrypt/aliyun/tencent/manual</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>续期提前天数。过期前N天自动续期</summary>
        public static readonly Field RenewDays = FindByName("RenewDays");

        /// <summary>最后续期时间</summary>
        public static readonly Field LastRenew = FindByName("LastRenew");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得SSL证书字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>域名。支持通配符，如*.newlifex.com，用于匹配应用Urls</summary>
        public const String Domain = "Domain";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>PFX文件。Windows/IIS使用，包含私钥</summary>
        public const String PfxFile = "PfxFile";

        /// <summary>PFX密码。加密存储</summary>
        public const String PfxPassword = "PfxPassword";

        /// <summary>证书文件。Linux/Nginx使用</summary>
        public const String CrtFile = "CrtFile";

        /// <summary>私钥文件。Linux/Nginx使用</summary>
        public const String KeyFile = "KeyFile";

        /// <summary>PEM文件。通用格式，合并crt和key</summary>
        public const String PemFile = "PemFile";

        /// <summary>颁发者。如Let's Encrypt Authority X3</summary>
        public const String Issuer = "Issuer";

        /// <summary>使用者。证书主题DN</summary>
        public const String Subject = "Subject";

        /// <summary>生效时间</summary>
        public const String NotBefore = "NotBefore";

        /// <summary>过期时间。用于自动告警和续期</summary>
        public const String NotAfter = "NotAfter";

        /// <summary>指纹。SHA1哈希，用于唯一标识</summary>
        public const String Thumbprint = "Thumbprint";

        /// <summary>自动续期。集成Let's Encrypt/阿里云等</summary>
        public const String AutoRenew = "AutoRenew";

        /// <summary>证书提供商。letsencrypt/aliyun/tencent/manual</summary>
        public const String Provider = "Provider";

        /// <summary>续期提前天数。过期前N天自动续期</summary>
        public const String RenewDays = "RenewDays";

        /// <summary>最后续期时间</summary>
        public const String LastRenew = "LastRenew";

        /// <summary>创建者</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
