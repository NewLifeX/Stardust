using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Server;

namespace Stardust.Web.Services;

/// <summary>调用链分享令牌。无状态HMAC签名，绑定单个追踪编号，可设置有效期</summary>
/// <remarks>
/// 令牌形如 <c>{过期时间}.{签名}</c>，过期时间为Unix秒，0表示长期有效。
/// 无状态的好处是服务重启与多实例部署都不需要额外存储；代价是不支持单条撤销，轮换分享密钥将使全部分享链接失效。
/// </remarks>
/// <example>
/// <code>
/// var tokens = new TraceShareTokenService();
/// var token = tokens.Issue(traceId);
/// if (tokens.Validate(traceId, token)) { ... }
/// </code>
/// </example>
public class TraceShareTokenService
{
    private static readonly Object _lock = new();

    #region 签发与校验
    /// <summary>为指定追踪编号签发分享令牌</summary>
    /// <param name="traceId">追踪编号</param>
    /// <returns>分享令牌，追踪编号为空时返回null</returns>
    public String Issue(String traceId)
    {
        if (traceId.IsNullOrEmpty()) return null;

        return CreateToken(traceId, GetSecret(), StarServerSetting.Current.TraceShareExpire, DateTime.UtcNow);
    }

    /// <summary>校验分享令牌是否有效</summary>
    /// <param name="traceId">追踪编号</param>
    /// <param name="token">分享令牌</param>
    /// <returns>是否有效</returns>
    public Boolean Validate(String traceId, String token)
    {
        if (traceId.IsNullOrEmpty() || token.IsNullOrEmpty()) return false;

        return CheckToken(traceId, token, GetSecret(), DateTime.UtcNow);
    }
    #endregion

    #region 静态算法
    /// <summary>生成令牌</summary>
    /// <param name="traceId">追踪编号</param>
    /// <param name="secret">签名密钥</param>
    /// <param name="expire">有效期（分钟），小于等于0表示长期有效</param>
    /// <param name="now">当前时间，便于单元测试</param>
    /// <returns>令牌字符串</returns>
    public static String CreateToken(String traceId, String secret, Int32 expire, DateTime now)
    {
        if (traceId.IsNullOrEmpty() || secret.IsNullOrEmpty()) return null;

        var exp = expire > 0 ? new DateTimeOffset(now.ToUniversalTime()).AddMinutes(expire).ToUnixTimeSeconds() : 0;
        return $"{exp}.{Sign(traceId, exp, secret)}";
    }

    /// <summary>校验令牌。签名不符、已过期或绑定其它追踪编号时均返回false</summary>
    /// <param name="traceId">追踪编号</param>
    /// <param name="token">令牌</param>
    /// <param name="secret">签名密钥</param>
    /// <param name="now">当前时间，便于单元测试</param>
    /// <returns>是否有效</returns>
    public static Boolean CheckToken(String traceId, String token, String secret, DateTime now)
    {
        if (traceId.IsNullOrEmpty() || token.IsNullOrEmpty() || secret.IsNullOrEmpty()) return false;

        var p = token.IndexOf('.');
        if (p <= 0 || p >= token.Length - 1) return false;

        if (!Int64.TryParse(token[..p], out var exp)) return false;
        if (exp > 0 && exp <= new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds()) return false;

        var signature = Encoding.UTF8.GetBytes(token[(p + 1)..]);
        var expected = Encoding.UTF8.GetBytes(Sign(traceId, exp, secret));

        return signature.Length == expected.Length && CryptographicOperations.FixedTimeEquals(signature, expected);
    }

    /// <summary>获取令牌的过期时间</summary>
    /// <param name="token">令牌</param>
    /// <returns>过期时间（本地时区），长期有效或无法解析时返回null</returns>
    public static DateTime? GetExpire(String token)
    {
        if (token.IsNullOrEmpty()) return null;

        var p = token.IndexOf('.');
        if (p <= 0) return null;

        if (!Int64.TryParse(token[..p], out var exp) || exp <= 0) return null;

        return DateTimeOffset.FromUnixTimeSeconds(exp).LocalDateTime;
    }

    private static String Sign(String traceId, Int64 exp, String secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{traceId}|{exp}"));

        return Convert.ToBase64String(hash.AsSpan(0, 16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    #endregion

    #region 辅助
    /// <summary>获取分享密钥。为空时自动生成并持久化，避免服务重启后全部分享链接失效</summary>
    /// <returns>分享密钥</returns>
    private static String GetSecret()
    {
        var set = StarServerSetting.Current;
        if (!set.TraceShareSecret.IsNullOrEmpty()) return set.TraceShareSecret;

        lock (_lock)
        {
            if (!set.TraceShareSecret.IsNullOrEmpty()) return set.TraceShareSecret;

            set.TraceShareSecret = Rand.NextString(32);
            try
            {
                set.Save();
            }
            catch (Exception ex)
            {
                // 持久化失败时仍使用内存密钥，本次进程内分享链接有效
                XTrace.WriteException(ex);
            }

            return set.TraceShareSecret;
        }
    }
    #endregion
}
