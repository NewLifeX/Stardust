using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using NewLife;
using NewLife.Web;
using System;

namespace Stardust.Server.Common
{
    /// <summary>Web助手</summary>
    public static class WebHelper
    {
        /// <summary>获取原始请求Url，支持反向代理</summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static Uri GetRawUrl(this HttpRequest request)
        {
            Uri uri = null;

            // 取请求头
            if (uri == null)
            {
                var url = request.GetEncodedUrl();
                uri = new Uri(url);
            }

            return GetRawUrl(uri, k => request.Headers[k]);
        }

        private static Uri GetRawUrl(Uri uri, Func<String, String> headers)
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

            var uriInfo = new UriInfo(uri.ToString());            
            //经反代时需要处理非80或443默认端口
            var port = headers("X-Forwarded-Port").ToInt();
            if (port > 0 && port !=80 && port != 443) 
            {
                uriInfo.Port = port;
                uri = new Uri(uriInfo.ToString()); 
            }
            
            return uri;
        }

        /// <summary>获取用户主机</summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static String GetUserHost(this HttpContext context)
        {
            var request = context.Request;

            var str = "";
            if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
            if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
            if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
            if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
            //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
            //if (str.IsNullOrEmpty()) str = context.Connection?.RemoteIpAddress?.MapToIPv4() + "";
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
    }
}
