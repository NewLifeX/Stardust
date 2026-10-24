using System.ComponentModel;
using NewLife;
using NewLife.Configuration;

namespace StarAgent;

/// <summary>阿里云DNS动态域名解析配置</summary>
[Config("AliyunDns")]
public class AliyunDnsSetting : Config<AliyunDnsSetting>
{
    #region 属性
    /// <summary>AccessKeyId。用于动态域名解析</summary>
    [Description("AccessKeyId. Used for dynamic domain name resolution")]
    public String? AccessKeyId { get; set; }

    /// <summary>AccessKeySecret。用于动态域名解析</summary>
    [Description("AccessKeySecret. Used for dynamic domain name resolution")]
    public String? AccessKeySecret { get; set; }

    /// <summary>域名。例如：example.com</summary>
    [Description("Domain name. For example: example.com")]
    public String? Domain { get; set; }

    /// <summary>记录。例如：www，表示www.example.com。@表示根域名</summary>
    [Description("Record. For example, www means www.example.com. @ means the root domain")]
    public String? Record { get; set; }

    /// <summary>记录类型。默认A记录</summary>
    [Description("Record type. Default A record")]
    public String RecordType { get; set; } = "A";

    /// <summary>更新间隔。默认300秒</summary>
    [Description("Update interval. Default 300 seconds.")]
    public Int32 Interval { get; set; } = 300;
    #endregion

    #region 方法
    /// <summary>是否已配置。检查必要参数是否齐全</summary>
    public Boolean IsConfigured =>
        !AccessKeyId.IsNullOrEmpty() &&
        !AccessKeySecret.IsNullOrEmpty() &&
        !Domain.IsNullOrEmpty() &&
        !Record.IsNullOrEmpty();
    #endregion
}
