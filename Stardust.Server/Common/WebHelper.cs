using Microsoft.AspNetCore.Http;
using System;

namespace Stardust.Server.Common
{
    /// <summary>Web助手</summary>
    public static class WebHelper
    {
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