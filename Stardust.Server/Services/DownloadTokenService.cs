using NewLife;
using NewLife.Caching;
using NewLife.Security;

namespace Stardust.Server.Services;

/// <summary>附件下载短时令牌服务。用于 /cube/file 附件下载鉴权（安全V-11）</summary>
/// <remarks>
/// URL 以相对路径长期存库、节点按 MD5 决定是否下载，因此令牌不在库中、不写死到 URL；
/// 而是在"下发下载 URL 的 RPC 响应时刻"由服务端现场签发，有效期只需覆盖"响应→下载完成"（默认10分钟）。
/// 节点离线/重启后的补拉 = 重新发起 RPC = 自动刷新，不存在过期死链。
/// 令牌存于 ICacheProvider.Cache：单机默认内存缓存，分布式部署配置 RedisCache 后为 Redis，多实例共享校验。
/// </remarks>
public class DownloadTokenService(ICacheProvider cacheProvider)
{
    /// <summary>缓存。来自 ICacheProvider，单机默认内存 / 分布式部署为 Redis</summary>
    private ICache Cache => cacheProvider.Cache;

    /// <summary>签发附件下载令牌</summary>
    /// <param name="attId">附件编号</param>
    /// <param name="expire">有效期（秒），默认600</param>
    /// <returns>令牌字符串</returns>
    public String Issue(Int64 attId, Int32 expire = 600)
    {
        var token = Rand.NextString(16);
        Cache.Set($"att:{attId}:{token}", 1, TimeSpan.FromSeconds(expire));
        return token;
    }

    /// <summary>校验附件下载令牌是否有效（未过期且绑定该附件）</summary>
    /// <param name="attId">附件编号</param>
    /// <param name="token">令牌</param>
    /// <returns>是否有效</returns>
    public Boolean Valid(Int64 attId, String token)
    {
        if (attId <= 0 || token.IsNullOrEmpty()) return false;
        return Cache.ContainsKey($"att:{attId}:{token}");
    }

    /// <summary>从下载URL提取附件编号。/cube/file?id=123.ext → 123；非附件链接返回0</summary>
    /// <param name="url">下载地址</param>
    /// <returns>附件编号</returns>
    public static Int64 GetAttachmentId(String url)
    {
        if (url.IsNullOrEmpty()) return 0;

        var p = url.IndexOf("id=", StringComparison.OrdinalIgnoreCase);
        if (p < 0) return 0;

        p += 3;
        Int64 id = 0;
        while (p < url.Length && url[p] >= '0' && url[p] <= '9')
        {
            id = id * 10 + (url[p] - '0');
            p++;
        }

        return id;
    }

    /// <summary>给附件下载URL附加短时令牌。非 /cube/file 链接原样返回</summary>
    /// <param name="url">下载地址</param>
    /// <param name="expire">有效期（秒）</param>
    /// <returns>附加令牌后的地址</returns>
    public String AppendToken(String url, Int32 expire = 600)
    {
        var id = GetAttachmentId(url);
        if (id <= 0) return url;

        var token = Issue(id, expire);
        var sep = url.Contains('?') ? '&' : '?';
        return url + sep + "token=" + token;
    }
}
