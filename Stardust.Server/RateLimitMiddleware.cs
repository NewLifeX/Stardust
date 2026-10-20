using System.Net;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Caching;

namespace Stardust.Server;

/// <summary>接口限流中间件。按 IP + 路径前缀做固定窗口限流，保护登录/令牌等认证接口免遭暴力破解与滥用</summary>
/// <remarks>
/// 命中配置的路径前缀后，统计该来源 IP 在 60 秒窗口内的请求次数，超过阈值返回 429。
/// 路径与阈值来自 <see cref="StarServerSetting.RateLimitPaths"/> / <see cref="StarServerSetting.RateLimitTimes"/>。
/// </remarks>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly String[] _paths;
    private readonly Int32 _times;
    private readonly ICache _cache = MemoryCache.Instance;

    /// <summary>实例化限流中间件</summary>
    /// <param name="next">后续管道</param>
    /// <param name="paths">限流路径前缀，逗号分隔</param>
    /// <param name="times">每 IP 每分钟允许次数</param>
    public RateLimitMiddleware(RequestDelegate next, String paths, Int32 times)
    {
        _next = next;
        _paths = paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _times = times > 0 ? times : 60;
    }

    /// <summary>处理请求</summary>
    /// <param name="context">HTTP上下文</param>
    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (!path.IsNullOrEmpty())
        {
            foreach (var item in _paths)
            {
                if (!path.StartsWith(item, StringComparison.OrdinalIgnoreCase)) continue;

                // 无代理头且直连回环/空来源（本地调试、单元与集成测试）跳过限流，避免同机内部调用被误伤
                var hasProxy = !context.Request.Headers["X-Forwarded-For"].ToString().IsNullOrEmpty()
                    || !context.Request.Headers["X-Real-IP"].ToString().IsNullOrEmpty();
                var remote = context.Connection.RemoteIpAddress;
                if (!hasProxy && (remote == null || IPAddress.IsLoopback(remote))) break;

                var ip = GetClientIP(context);                var key = $"rate:{item}:{ip}";
                var count = _cache.Increment(key, 1);
                if (count <= 1) _cache.SetExpire(key, TimeSpan.FromSeconds(60));

                if (count > _times)
                {
                    context.Response.StatusCode = 429;
                    await context.Response.WriteAsJsonAsync(new { code = 429, message = L.T("Too many requests; please try again later.") });
                    return;
                }
                break;
            }
        }

        await _next(context);
    }

    /// <summary>获取客户端IP。优先取 X-Forwarded-For / X-Real-IP（反代场景），否则直连地址</summary>
    private static String GetClientIP(HttpContext context)
    {
        var fwd = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!fwd.IsNullOrEmpty())
        {
            var p = fwd.IndexOf(',');
            if (p > 0) fwd = fwd[..p];
            return fwd.Trim();
        }

        var real = context.Request.Headers["X-Real-IP"].ToString();
        if (!real.IsNullOrEmpty()) return real.Trim();

        return context.Connection.RemoteIpAddress + "";
    }
}
