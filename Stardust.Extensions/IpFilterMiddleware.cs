using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Web;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace Stardust.Extensions;

/// <summary>IP过滤中间件</summary>
public class IpFilterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly String _requestPath;
    private readonly String _whiteIPs;

    /// <summary>实例化</summary>
    /// <param name="next"></param>
    /// <param name="requestPath"></param>
    /// <param name="whiteIPs"></param>
    public IpFilterMiddleware(RequestDelegate next, String requestPath, String whiteIPs)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _requestPath = requestPath;
        _whiteIPs = whiteIPs;
    }

    /// <summary>调用</summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext ctx)
    {
        if (_requestPath.IsNullOrEmpty() || ctx.Request.Path.StartsWithSegments(_requestPath))
        {
            var ip = ctx.GetUserHost();
            if (!ValidIP(ip))
            {
                ctx.Response.StatusCode = (Int32)HttpStatusCode.Forbidden;
                ctx.Response.ContentLength = 0;
                ctx.Response.Body = Stream.Null;

                return;
            }
        }

        await _next.Invoke(ctx).ConfigureAwait(false);
    }

    Boolean ValidIP(String ip)
    {
        if (ip.IsNullOrEmpty()) return false;

        var whites = _whiteIPs;
        if (whites.IsNullOrEmpty()) return true;

        // 白名单里面有的，直接通过
        var ws = (whites + "").Split(",", ";");
        if (ws.Length > 0)
        {
            return ws.Any(e => e.IsMatch(ip));
        }

        // 未设置白名单，黑名单里面没有的，直接通过
        return true;
    }
}