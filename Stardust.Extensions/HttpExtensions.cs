using System;
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
            DefaultSpan.Current?.AppendTag($"GetRawUrl：{url} 失败：{ex.Message}");
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

    /// <summary>获取用户主机</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static String? GetUserHost(this HttpContext context)
    {
        var request = context.Request;

        var str = "";
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
        //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
        if (str.IsNullOrEmpty())
        {
            var addr = context.Connection?.RemoteIpAddress;
            if (addr != null)
            {
                if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
                str = addr + "";
            }
        }

        return str;
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
