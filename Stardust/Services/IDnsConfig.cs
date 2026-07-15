namespace Stardust.Services;

/// <summary>DNS供应商配置接口。由DomainProvider实体类实现，统一传递配置</summary>
public interface IDnsConfig
{
    /// <summary>AppKey。AccessKeyId/SecretId/PublicKey</summary>
    String? AppKey { get; }

    /// <summary>AppSecret。AccessKeySecret/SecretKey/PrivateKey</summary>
    String? AppSecret { get; }

    /// <summary>管理的根域名，如newlifex.com</summary>
    String? Domain { get; }

    /// <summary>API端点。为空使用供应商默认值</summary>
    String? Endpoint { get; }
}
