using System;
using System.Security.Cryptography;
using NewLife;

namespace Stardust.Data.Deployment;

/// <summary>webhook 签名校验。兼容行业常见形式：HMAC-SHA256 十六进制摘要（GitHub/Gitea）、
/// Base64 摘要（自定义发送方）、以及请求头明文令牌（GitLab/Gitee）</summary>
public static class WebhookSignature
{
    /// <summary>校验签名。配置了 Secret 的流水线才会走到这里</summary>
    /// <param name="secret">流水线密钥</param>
    /// <param name="request">webhook 入参</param>
    /// <returns>是否通过</returns>
    public static Boolean Verify(String secret, WebhookRequest request)
    {
        if (secret.IsNullOrEmpty() || request == null) return false;

        // 明文令牌比对
        if (!request.Token.IsNullOrEmpty()) return request.Token == secret;

        var signature = request.Signature;
        if (signature.IsNullOrEmpty()) return false;

        // 形如 sha256=xxx，剥离算法前缀。Base64 摘要末尾自带 '=' 填充，不能当作分隔符
        var index = signature.IndexOf('=');
        if (IsAlgorithmTag(signature, index)) signature = signature.Substring(index + 1);

        var body = request.Body ?? "";
        using var hmac = new HMACSHA256(secret.GetBytes());
        var hash = hmac.ComputeHash(body.GetBytes());

        // 十六进制摘要（GitHub/Gitea 标准）
        var hex = BitConverter.ToString(hash).Replace("-", "");
        if (hex.EqualIgnoreCase(signature)) return true;

        // Base64 摘要（自定义发送方沿用旧协议）
        return hash.ToBase64().EqualIgnoreCase(signature);
    }

    /// <summary>判断等号前是否为算法标识（如 sha256）。用字母数字限定长度，避免把 Base64 的尾部填充当作分隔符</summary>
    private static Boolean IsAlgorithmTag(String value, Int32 index)
    {
        if (index <= 0 || index > 10) return false;

        for (var i = 0; i < index; i++)
        {
            var ch = value[i];
            if (!(ch >= 'a' && ch <= 'z') && !(ch >= 'A' && ch <= 'Z') && !(ch >= '0' && ch <= '9')) return false;
        }

        return true;
    }
}
