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
    /// <summary>是否信任转发头（X-Forwarded-For 等）。默认true保持兼容；公网直连场景建议设为false，改用真实远端地址，防止伪造转发头绕过白名单</summary>
    public static Boolean TrustForwardHeaders { get; set; } = true;

    private readonly RequestDelegate _next;
    private readonly String _requestPath;
    private readonly String[] _whiteIPs;

    /// <summary>实例化</summary>
    /// <param name="next"></param>
    /// <param name="requestPath"></param>
    /// <param name="whiteIPs"></param>
    public IpFilterMiddleware(RequestDelegate next, String requestPath, String whiteIPs)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _requestPath = requestPath;
        // 预解析白名单，避免每次请求重复拆分。为空表示不校验（全部放行）
        _whiteIPs = (whiteIPs + "").Split(",", ";").Where(e => !e.IsNullOrEmpty()).ToArray();
    }

    /// <summary>调用</summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext ctx)
    {
        if (_requestPath.IsNullOrEmpty() || ctx.Request.Path.StartsWithSegments(_requestPath))
        {
            // 默认信任转发头保持兼容；关闭后使用真实远端地址，防止伪造 X-Forwarded-For 绕过白名单
            var ip = TrustForwardHeaders ? ctx.GetUserHost() : ctx.Connection.RemoteIpAddress?.MapToIPv4() + "";
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

    Boolean ValidIP(String? ip)
    {
        if (ip.IsNullOrEmpty()) return false;

        // 未设置白名单，全部放行
        if (_whiteIPs.Length == 0) return true;

        // 白名单里面有的，直接通过
        return _whiteIPs.Any(e => e.IsMatch(ip));
    }
}