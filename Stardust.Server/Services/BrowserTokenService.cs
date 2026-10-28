using NewLife;
using NewLife.Caching;
using NewLife.Remoting;
using NewLife.Security;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>浏览器埋点短期令牌服务</summary>
/// <remarks>
/// 浏览器不能持有应用密钥，因此由服务端按应用现场签发短期令牌，仅供埋点上报使用。
/// 令牌是不透明随机串，寄存于 ICacheProvider（默认内存，分布式部署为 Redis），默认 10 分钟过期；
/// 不使用 JWT，这样即使被窃取，也无法像应用令牌那样去调用配置、部署等其它受保护接口。
/// 同一应用与来源IP在有效期内限制签发次数，避免令牌接口被刷。
/// </remarks>
public class BrowserTokenService(ICacheProvider cacheProvider)
{
    /// <summary>令牌前缀。用于快速识别浏览器令牌，避免与 JWT 混淆</summary>
    public const String Prefix = "bw_";

    /// <summary>令牌有效期（秒）。默认 600</summary>
    public Int32 Expire { get; set; } = 600;

    /// <summary>同一应用与来源IP在有效期内的最大签发次数。默认 60</summary>
    public Int32 MaxIssue { get; set; } = 60;

    /// <summary>缓存。来自 ICacheProvider，单机默认内存 / 分布式部署为 Redis</summary>
    private ICache Cache => cacheProvider.Cache;

    /// <summary>为指定应用签发浏览器埋点令牌</summary>
    /// <param name="app">应用。调用方需自行校验应用存在且启用</param>
    /// <param name="ip">来源IP，用于限流</param>
    /// <returns>令牌字符串</returns>
    public String Issue(App app, String ip)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        CheckLimit(app.Name, ip);

        var token = Prefix + Rand.NextString(24);
        Cache.Set($"bw:{token}", app.Name, TimeSpan.FromSeconds(Expire));

        return token;
    }

    /// <summary>校验浏览器埋点令牌，返回对应应用</summary>
    /// <param name="token">令牌字符串</param>
    /// <returns>令牌有效且应用启用时返回应用，否则返回 null</returns>
    public App Validate(String token)
    {
        if (token.IsNullOrEmpty() || !token.StartsWith(Prefix)) return null;

        var name = Cache.Get<String>($"bw:{token}");
        if (name.IsNullOrEmpty()) return null;

        var app = App.FindByName(name);

        return app != null && app.Enable ? app : null;
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
}
