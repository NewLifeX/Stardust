using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using NewLife.Log;

namespace NewLife.Web;

/// <summary>网页工具类</summary>
static class WebHelper
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
            uri = new UriInfo("")
            {
                Scheme = request.Scheme,
                Host = request.Host.Host,
                Port = request.Host.Port ?? (request.Scheme == "https" ? 443 : 80),
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
}