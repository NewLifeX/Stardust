using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace NewLife.Web;

/// <summary>网页工具类</summary>
static class WebHelper
{
    #region Http请求
    /// <summary>获取原始请求Url，支持反向代理</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static Uri GetRawUrl(this HttpRequest request)
    {
        Uri? uri = null;

        // 取请求头
        if (uri == null)
        {
            var url = request.GetEncodedUrl();
            uri = new Uri(url);
        }

        return GetRawUrl(uri, k => request.Headers[k]);
    }

    private static Uri GetRawUrl(Uri uri, Func<String, String?> headers)
    {
        var str = headers("HTTP_X_REQUEST_URI");
        if (str.IsNullOrEmpty()) str = headers("X-Request-Uri");

        if (str.IsNullOrEmpty())
        {
            // 阿里云CDN默认支持 X-Client-Scheme: https
            var scheme = headers("HTTP_X_CLIENT_SCHEME");
            if (scheme.IsNullOrEmpty()) scheme = headers("X-Client-Scheme");

            // nginx
            if (scheme.IsNullOrEmpty()) scheme = headers("HTTP_X_FORWARDED_PROTO");
            if (scheme.IsNullOrEmpty()) scheme = headers("X-Forwarded-Proto");

            if (!scheme.IsNullOrEmpty()) str = scheme + "://" + uri.ToString().Substring("://");
        }

        if (!str.IsNullOrEmpty()) uri = new Uri(uri, str);

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