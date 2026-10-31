using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NewLife;

namespace Stardust.Web.Filters;

/// <summary>登录落地页改写。用户登录后默认进入看板中心入口 /</summary>
/// <remarks>
/// 魔方登录由管道中间件完成（不经过 MVC 动作），因此只能在响应头写出前改写跳转目标；
/// 带返回地址（如从大屏被弹回登录）时保持原目标不变。
/// 管理后台首页仍由 Cube.StartPage 决定，保持魔方原生工作台。
/// </remarks>
public static class LoginLandingMiddleware
{
    /// <summary>登录提交地址</summary>
    public const String LoginPath = "/Admin/User/Login";

    /// <summary>注册登录落地页改写中间件</summary>
    /// <param name="app">应用管道</param>
    /// <returns>应用管道</returns>
    public static IApplicationBuilder UseLoginLanding(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        if (HttpMethods.IsPost(request.Method)
            && request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase)
            && request.Query["r"].ToString().IsNullOrEmpty())
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode is 301 or 302 or 303 or 307)
                {
                    var location = context.Response.Headers.Location.ToString();
                    if (location.EqualIgnoreCase("/Admin")) context.Response.Headers.Location = "/";
                }

                return Task.CompletedTask;
            });
        }

        await next();
    });
}
