using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>浏览器埋点短期令牌服务</summary>
/// <remarks>
/// 浏览器不能持有应用密钥，因此由服务端按应用现场签发短期令牌，仅供埋点上报使用。
/// 令牌由应用名、页面来源与过期时间组成，使用服务端密钥做 HMAC 签名，校验不依赖共享缓存，
/// 多实例部署无需 Redis；令牌不是 JWT，即使被窃取也无法调用配置、部署等其它受保护接口。
/// 同一应用与来源IP在有效期内限制签发次数，上报侧另有限流，避免公开写通道被刷。
/// 兼容旧版随机不透明令牌：不是签名结构的 bw_ 令牌继续按缓存校验，自然过期后退出。
/// </remarks>
public class BrowserTokenService(ICacheProvider cacheProvider)
{
    private static readonly Object _lock = new();

    /// <summary>令牌前缀。用于快速识别浏览器令牌，避免与 JWT 混淆</summary>
    public const String Prefix = "bw_";

    /// <summary>令牌有效期（秒）。默认 600</summary>
    public Int32 Expire { get; set; } = 600;

    /// <summary>同一应用与来源IP在有效期内的最大签发次数。默认 300</summary>
    /// <remarks>签名令牌无服务端存储，限流只用于挡住脚本批量申请；数值需容纳同一出口IP下的正常多标签页访问</remarks>
    public Int32 MaxIssue { get; set; } = 300;

    /// <summary>浏览器令牌每分钟允许的上报次数（按应用与来源IP）。默认 600</summary>
    public Int32 MaxReport { get; set; } = 600;

    /// <summary>单次上报允许的最大埋点项数量。默认 100</summary>
    public Int32 MaxBuilders { get; set; } = 100;

    private String _secret;

    /// <summary>签名密钥。为空时自动生成并持久化</summary>
    public String Secret { get => _secret ??= GetSecret(); set => _secret = value; }

    /// <summary>缓存。来自 ICacheProvider，单机默认内存 / 分布式部署为 Redis</summary>
    private ICache Cache => cacheProvider.Cache;

    /// <summary>为指定应用签发浏览器埋点令牌</summary>
    /// <param name="app">应用。调用方需自行校验应用存在且启用</param>
    /// <param name="ip">来源IP，用于限流</param>
    /// <param name="origin">页面来源，绑定到令牌；为空表示不绑定</param>
    /// <returns>令牌字符串</returns>
    public String Issue(App app, String ip, String origin = null)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        CheckLimit(app.Name, ip);

        return CreateToken(app.Name, NormalizeOrigin(origin), Secret, Expire, DateTime.UtcNow);
    }

    /// <summary>校验浏览器埋点令牌，返回对应应用</summary>
    /// <param name="token">令牌字符串</param>
    /// <param name="origin">请求来源。令牌绑定了来源且请求携带来源时进行比对</param>
    /// <returns>令牌有效且应用启用时返回应用，否则返回 null</returns>
    public App Validate(String token, String origin = null)
    {
        if (token.IsNullOrEmpty() || !token.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        // 优先按签名令牌校验；旧版随机令牌回退到缓存
        String name;
        if (!CheckToken(token, Secret, NormalizeOrigin(origin), DateTime.UtcNow, out name))
            name = Cache.Get<String>($"bw:{token}");

        if (name.IsNullOrEmpty()) return null;

        var app = App.FindByName(name);

        return app != null && app.Enable ? app : null;
    }

    /// <summary>浏览器令牌上报限流。同一应用与来源IP每分钟超过阈值时返回 false</summary>
    /// <param name="appName">应用名</param>
    /// <param name="ip">来源IP</param>
    /// <returns>是否允许上报</returns>
    public Boolean CheckReportLimit(String appName, String ip)
    {
        var key = $"bwrpt:{appName}:{ip}";
        var count = Cache.Increment(key, 1);
        if (count <= 1) Cache.SetExpire(key, TimeSpan.FromSeconds(60));

        return count <= MaxReport;
    }

    /// <summary>把来源地址规范化为协议+主机+端口。Origin 与 Referer 均可解析，非法或缺失时返回 null</summary>
    /// <param name="raw">来源地址</param>
    /// <returns>规范化来源或 null</returns>
    public static String NormalizeOrigin(String raw)
    {
        if (raw.IsNullOrEmpty()) return null;

        var text = raw.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != "http" && uri.Scheme != "https") return null;

        return $"{uri.Scheme}://{uri.Authority}";
    }

    /// <summary>生成签名令牌。格式：bw_{应用}.{来源}.{过期Unix秒}.{签名}</summary>
    /// <param name="appName">应用名</param>
    /// <param name="origin">页面来源，可为空</param>
    /// <param name="secret">签名密钥</param>
    /// <param name="expire">有效期（秒），小于等于0表示不过期</param>
    /// <param name="now">当前时间，便于单元测试</param>
    /// <returns>令牌字符串</returns>
    public static String CreateToken(String appName, String origin, String secret, Int32 expire, DateTime now)
    {
        if (appName.IsNullOrEmpty() || secret.IsNullOrEmpty()) return null;

        var exp = expire > 0 ? new DateTimeOffset(now.ToUniversalTime()).AddSeconds(expire).ToUnixTimeSeconds() : 0;
        var app = Base64Url(appName);
        var originText = Base64Url(origin);
        var signature = Sign(app, originText, exp + "", secret);

        return $"{Prefix}{app}.{originText}.{exp}.{signature}";
    }

    /// <summary>校验签名令牌。签名不符、已过期、来源不匹配或结构非法时均返回 false</summary>
    /// <param name="token">令牌字符串</param>
    /// <param name="secret">签名密钥</param>
    /// <param name="origin">请求来源，可为空</param>
    /// <param name="now">当前时间，便于单元测试</param>
    /// <param name="appName">解析出的应用名</param>
    /// <returns>是否有效</returns>
    public static Boolean CheckToken(String token, String secret, String origin, DateTime now, out String appName)
    {
        appName = null;
        if (token.IsNullOrEmpty() || secret.IsNullOrEmpty()) return false;
        if (!token.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var parts = token[Prefix.Length..].Split('.');
        if (parts.Length != 4) return false;

        if (!Int64.TryParse(parts[2], out var exp)) return false;
        if (exp > 0 && exp <= new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds()) return false;

        var expected = Sign(parts[0], parts[1], parts[2], secret);
        if (!FixedTimeEquals(parts[3], expected)) return false;

        if (!TryDecode(parts[0], out var name) || name.IsNullOrEmpty()) return false;
        if (!TryDecode(parts[1], out var tokenOrigin)) return false;

        // 令牌申请时绑定了来源，且请求携带来源时，二者必须一致，防止令牌被抄到其它站点使用
        if (!tokenOrigin.IsNullOrEmpty() && !origin.IsNullOrEmpty() && !tokenOrigin.EqualIgnoreCase(origin)) return false;

        appName = name;

        return true;
    }

    /// <summary>签发次数限流。同一应用与来源IP在有效期内超过阈值时拒绝</summary>
    /// <param name="appName">应用名</param>
    /// <param name="ip">来源IP</param>
    private void CheckLimit(String appName, String ip)
    {
        var key = $"bwcnt:{appName}:{ip}";
        var count = Cache.Increment(key, 1);
        if (count == 1) Cache.SetExpire(key, TimeSpan.FromSeconds(Expire));

        if (count > MaxIssue) throw new ApiException(ApiCode.Forbidden, L.T("Too many requests; please try again later."));
    }

    /// <summary>获取签名密钥。为空时自动生成并持久化，避免服务重启后存量令牌立即失效</summary>
    /// <returns>签名密钥</returns>
    private static String GetSecret()
    {
        var set = StarServerSetting.Current;
        if (!set.BrowserTokenSecret.IsNullOrEmpty()) return set.BrowserTokenSecret;

        lock (_lock)
        {
            if (!set.BrowserTokenSecret.IsNullOrEmpty()) return set.BrowserTokenSecret;

            set.BrowserTokenSecret = Rand.NextString(32);
            try
            {
                set.Save();
            }
            catch (Exception ex)
            {
                // 持久化失败时仍使用内存密钥，本次进程内签发的令牌有效
                XTrace.WriteException(ex);
            }

            return set.BrowserTokenSecret;
        }
    }

    private static String Sign(String app, String origin, String exp, String secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{app}|{origin}|{exp}"));

        return Convert.ToBase64String(hash.AsSpan(0, 16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Boolean FixedTimeEquals(String left, String right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? "");
        var b = Encoding.UTF8.GetBytes(right ?? "");

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static String Base64Url(String value)
    {
        if (value.IsNullOrEmpty()) return "";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Boolean TryDecode(String value, out String text)
    {
        text = null;
        if (value.IsNullOrEmpty())
        {
            text = "";
            return true;
        }

        try
        {
            var text2 = value.Replace('-', '+').Replace('_', '/');
            switch (text2.Length % 4)
            {
                case 2: text2 += "=="; break;
                case 3: text2 += "="; break;
                case 1: return false;
            }
            text = Encoding.UTF8.GetString(Convert.FromBase64String(text2));

            return true;
        }
        catch
        {
            return false;
        }
    }
}
