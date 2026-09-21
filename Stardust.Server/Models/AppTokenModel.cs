using System.Runtime.Serialization;
using NewLife.Web;

namespace Stardust.Server.Models;

/// <summary>应用令牌响应。在标准令牌字段基础上增加应用密钥，供自助接入的客户端保存后自动收敛</summary>
/// <remarks>仅在按重注册模式（收敛窗口/开放）放行时携带密钥；旧客户端忽略该字段，保持兼容</remarks>
public class AppTokenModel : TokenModel
{
    /// <summary>应用密钥</summary>
    [DataMember(Name = "secret")]
    public String? Secret { get; set; }
}
