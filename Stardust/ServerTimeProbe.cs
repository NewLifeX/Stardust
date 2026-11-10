using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;

namespace Stardust;

/// <summary>服务端时间探测</summary>
/// <remarks>
/// 客户端用服务端时间生成鉴权盐值（<see cref="ServerTimePasswordProvider"/>），
/// 但首次登录前还没有机会从登录响应拿到 ServerTime。这里提前请求一次服务端匿名健康接口 <c>/api</c>，
/// 取其 UTC 毫秒时间戳写入客户端时钟偏移，使首次登录的盐值就落在服务端的 SaltTime 窗口内。
/// <para>探测失败不影响正常登录：保持原有偏移，按本机时间签名，与改动前行为一致。</para>
/// </remarks>
public static class ServerTimeProbe
{
    /// <summary>探测失败后的重试间隔。登录失败会反复重试，避免每次都发起一次 HTTP 请求</summary>
    public static TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>请求超时</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    private static readonly ConcurrentDictionary<String, DateTime> _lastTries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>获取服务端 UTC 时间戳（毫秒）</summary>
    /// <param name="server">服务端地址，支持逗号分隔的多个地址</param>
    /// <param name="force">是否忽略重试间隔</param>
    /// <returns>服务端 UTC 毫秒时间戳；无法获取时返回0</returns>
    public static Int64 GetServerTime(String? server, Boolean force = false)
    {
        var url = GetTimeUrl(server);
        if (url.IsNullOrEmpty()) return 0;

        if (!force && _lastTries.TryGetValue(url, out var last) && last.Add(RetryInterval) > DateTime.Now) return 0;

        _lastTries[url] = DateTime.Now;
        try
        {
            using var http = new HttpClient { Timeout = Timeout };

            var json = http.GetStringAsync(url).GetAwaiter().GetResult();

            return ParseServerTime(json);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Server time probe failed: {0} {1}", url, ex.Message);

            return 0;
        }
    }

    /// <summary>构造服务端时间探测地址</summary>
    /// <param name="server">服务端地址，支持逗号分隔的多个地址</param>
    /// <returns>探测地址；无法解析时返回null</returns>
    public static String? GetTimeUrl(String? server)
    {
        if (server.IsNullOrEmpty()) return null;

        // 与 ApiHttpClient 保持一致：服务端地址支持逗号分隔的多个节点，这里取第一个
        var url = server.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (url.IsNullOrEmpty()) return null;

        return url.TrimEnd('/') + "/api";
    }

    /// <summary>从 /api 响应中解析服务端 UTC 毫秒时间戳</summary>
    /// <param name="json">响应文本</param>
    /// <returns>UTC 毫秒时间戳；解析不到时返回0</returns>
    public static Int64 ParseServerTime(String? json)
    {
        if (json.IsNullOrEmpty()) return 0;

        try
        {
            var dic = JsonParser.Decode(json);
            if (dic == null) return 0;

            foreach (var item in dic)
            {
                if (item.Key.EqualIgnoreCase("serverTime")) return item.Value.ToLong();
            }
        }
        catch
        {
            // 非 JSON 响应（反代错误页等）视为探测失败
        }

        return 0;
    }
}
