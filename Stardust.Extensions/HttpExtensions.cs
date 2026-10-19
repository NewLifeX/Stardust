using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using NewLife;
using NewLife.Log;
using NewLife.Web;

namespace Stardust.Extensions;

/// <summary>Http扩展</summary>
public static class HttpExtensions
{
    #region Http请求
    /// <summary>获取原始请求Url，支持反向代理</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static UriInfo GetRawUrl(this HttpRequest request)
    {
        UriInfo? uri = null;

        // 取请求头
        var url = request.GetEncodedUrl();
        try
        {
            uri = new UriInfo(url);
        }
        catch (Exception ex)
        {
            DefaultSpan.Current?.AppendTag(L.TLog("GetRawUrl: {0} failed: {1}", url, ex.Message));
            var port = request.Scheme switch
            {
                "https" => 443,
                "http" => 80,
                _ => 0
            };
            uri = new UriInfo
            {
                Scheme = request.Scheme,
                Host = request.Host.Host,
                Port = request.Host.Port ?? port,
                AbsolutePath = request.PathBase + request.Path,
                Query = request.QueryString.ToUriComponent()
            };
        }

        return GetRawUrl(uri, k => request.Headers[k]);
    }

    private static UriInfo GetRawUrl(UriInfo uri, Func<String, String?> headers)
    {
        var str = headers("HTTP_X_REQUEST_URI");
        if (str.IsNullOrEmpty()) str = headers("X-Request-Uri");

        if (!str.IsNullOrEmpty()) uri = new UriInfo(str);

        // 阿里云CDN默认支持 X-Client-Scheme: https
        var scheme = headers("HTTP_X_CLIENT_SCHEME");
        if (scheme.IsNullOrEmpty()) scheme = headers("X-Client-Scheme");

        // nginx
        if (scheme.IsNullOrEmpty()) scheme = headers("HTTP_X_FORWARDED_PROTO");
        if (scheme.IsNullOrEmpty()) scheme = headers("X-Forwarded-Proto");

        //if (!scheme.IsNullOrEmpty()) str = scheme + "://" + uri.ToString().Substring("://");
        if (!scheme.IsNullOrEmpty()) uri.Scheme = scheme;

        //if (!str.IsNullOrEmpty()) uri = new Uri(uri, str);

        return uri;
    }

    /// <summary>可信代理列表。来自这些代理的转发头才被信任，支持精确地址、*通配和IPv4 CIDR网段；为空时兼容旧行为信任全部转发头</summary>
    public static String?[]? TrustedProxies { get; set; }

    /// <summary>获取用户主机。优先从转发头解析；配置可信代理后按代理链解析并防伪造，最终返回单一IP地址</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static String? GetUserHost(this HttpContext context)
    {
        var request = context.Request;

        // 直连地址
        var remote = context.Connection?.RemoteIpAddress;
        if (remote != null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        var remoteIp = remote + "";

        var str = "";
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];

        if (!str.IsNullOrEmpty())
        {
            var trusted = TrustedProxies;
            if (trusted == null || trusted.Length == 0)
            {
                // 未配置可信代理，兼容旧行为信任全部转发头；多层反代时取链首地址，并折叠为单一IP
                var first = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).FirstOrDefault(e => e.Length > 0);
                if (!first.IsNullOrEmpty()) str = first;
            }
            else if (remoteIp.IsNullOrEmpty() || !IsTrustedProxy(remoteIp, trusted))
            {
                // 配置了可信代理但直连来源不可信，转发头可被伪造，直接使用直连地址
                str = remoteIp;
            }
            else
            {
                // 从链尾向左跳过可信代理，取第一个不可信地址作为客户端地址
                var ips = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                var found = (String?)null;
                for (var i = ips.Count - 1; i >= 0; i--)
                {
                    if (!IsTrustedProxy(ips[i], trusted))
                    {
                        found = ips[i];
                        break;
                    }
                }
                str = found ?? (ips.Count > 0 ? ips[0] : remoteIp);
            }
        }

        //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
        if (str.IsNullOrEmpty()) str = remoteIp;

        return str;
    }

    /// <summary>判断地址是否在可信代理列表内。列表项支持精确IP、*通配和IPv4 CIDR网段</summary>
    /// <param name="ip">待判断IP</param>
    /// <param name="trusted">可信代理列表</param>
    /// <returns>是否可信</returns>
    private static Boolean IsTrustedProxy(String? ip, String?[] trusted)
    {
        if (ip.IsNullOrEmpty()) return false;

        foreach (var item in trusted)
        {
            var t = item?.Trim();
            if (t.IsNullOrEmpty()) continue;

            if (t == "*") return true;

            if (t.Contains('/'))
            {
                if (MatchCidr(ip!, t)) return true;
            }
            else if (t.IsMatch(ip))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>IPv4 CIDR网段匹配</summary>
    /// <param name="ip">IPv4地址</param>
    /// <param name="cidr">网段，如 10.0.0.0/8</param>
    /// <returns>是否属于该网段</returns>
    private static Boolean MatchCidr(String ip, String cidr)
    {
        var ss = cidr.Split('/');
        if (ss.Length != 2) return false;

        var mask = ss[1].ToInt();
        if (mask is < 0 or > 32) return false;

        var ip1 = ParseIpv4(ip);
        var ip2 = ParseIpv4(ss[0]);
        if (ip1 < 0 || ip2 < 0) return false;

        if (mask == 0) return true;

        var bits = (~((1L << (32 - mask)) - 1)) & 0xFFFFFFFFL;
        return (ip1 & bits) == (ip2 & bits);
    }

    /// <summary>解析IPv4地址为整数，非法返回-1</summary>
    /// <param name="ip">IPv4地址</param>
    /// <returns>整型地址</returns>
    private static Int64 ParseIpv4(String? ip)
    {
        if (ip.IsNullOrEmpty()) return -1;

        var ss = ip.Split('.');
        if (ss.Length != 4) return -1;

        var n = 0L;
        foreach (var item in ss)
        {
            var b = item.ToInt();
            if (b is < 0 or > 255) return -1;

            n = (n << 8) | (UInt32)b;
        }

        return n;
    }
    #endregion

    #region Http响应
    /// <summary>设置文件哈希相关的响应头</summary>
    /// <param name="response">Http响应</param>
    /// <param name="hash">文件哈希值，格式：[算法名$]哈希值，如MD5$abc123或abc123</param>
    public static void SetFileHashHeaders(this HttpResponse response, String hash) => SetFileHashHeaders(response.Headers, hash);

    /// <summary>设置文件哈希相关的响应头</summary>
    /// <param name="headers">Http响应头</param>
    /// <param name="hash">文件哈希值，格式：[算法名$]哈希值，如MD5$abc123或abc123</param>
    public static void SetFileHashHeaders(this IHeaderDictionary headers, String hash)
    {
        if (hash.IsNullOrEmpty()) return;

        // 解析哈希算法名称和哈希值
        var algorithm = "MD5";
        var hashValue = hash;

        var dollarIndex = hash.IndexOf('$');
        if (dollarIndex > 0)
        {
            algorithm = hash[..dollarIndex];
            hashValue = hash[(dollarIndex + 1)..];
        }

        // 1. RFC 3230 标准 Digest 头
        headers["Digest"] = $"{algorithm}={hashValue}";

        // 2. X-Content-MD5（兼容某些客户端，总是用MD5）
        if (algorithm.EqualIgnoreCase("MD5"))
            headers["X-Content-MD5"] = hashValue;

        // 3. ETag（用于缓存验证）
        headers["ETag"] = $"\"{hashValue}\"";

        // 4. 自定义头（易于识别）
        headers["X-File-Hash"] = $"{algorithm}:{hashValue}";
    }
    #endregion
}
