using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Cube;
using Stardust.Data.Platform;
using XCode.Membership;

namespace Stardust.Web.Infrastructure;

/// <summary>项目数据范围中间件。为每个已登录请求解析可见项目，供 XCode 项目拦截器使用</summary>
/// <remarks>
/// 必须注册在 UseCube 之前：Cube 未启用端点路由时会在 UseCube 内用旧式路由直接执行控制器，放在其后不会被执行。
/// 中间件内部通过 ManageProvider.TryLogin 用令牌解析用户，不依赖认证中间件先运行。
/// 服务端数据面与后台任务不经过该中间件，项目拦截器不会生效。
/// </remarks>
public class ProjectScopeMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>实例化</summary>
    /// <param name="next">后续中间件</param>
    public ProjectScopeMiddleware(RequestDelegate next) => _next = next;

    /// <summary>调用</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns></returns>
    public async Task Invoke(HttpContext context)
    {
        var old = ProjectScopeContext.Current;
        try
        {
            if (IsStaticFile(context))
            {
                ProjectScopeContext.Current = null;
            }
            else
            {
                // API 请求没有 Session，需要在中间件阶段用令牌解析当前用户；
                // 不能等到 EntityAuthorize 过滤器，那时业务查询已经开始。
                var user = ManageProvider.User;
                if (user == null && ManageProvider.Provider != null) user = ManageProvider.Provider.TryLogin(context) as IUser;

                var userId = user?.Id ?? 0;
                if (userId > 0)
                {
                    var scope = ProjectScope.Resolve(userId);

                    var selected = context.Request.Query["projectId"].ToString().ToInt(0);
                    if (selected <= 0 && context.Request.RouteValues.TryGetValue("projectId", out var route) && route != null)
                        selected = route.ToString().ToInt(0);

                    // 只有一个可见项目时默认选中，避免新增数据因缺少项目上下文被拒
                    if (selected <= 0 && !scope.IsAdmin && scope.ProjectIds.Length == 1) selected = scope.ProjectIds[0];

                    if (selected > 0 && (scope.IsAdmin || scope.Contains(selected))) scope.SelectedProjectId = selected;

                    ProjectScopeContext.Current = scope;
                }
                else
                {
                    ProjectScopeContext.Current = null;
                }
            }

            await _next(context);
        }
        finally
        {
            ProjectScopeContext.Current = old;
        }
    }

    private static Boolean IsStaticFile(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path.IsNullOrEmpty()) return false;

        return path.StartsWithIgnoreCase("/css/")
            || path.StartsWithIgnoreCase("/js/")
            || path.StartsWithIgnoreCase("/layui/")
            || path.StartsWithIgnoreCase("/skin/")
            || path.StartsWithIgnoreCase("/images/")
            || path.StartsWithIgnoreCase("/img/")
            || path.StartsWithIgnoreCase("/fonts/")
            || path.StartsWithIgnoreCase("/dist/")
            || path.StartsWithIgnoreCase("/assets/")
            || path.StartsWithIgnoreCase("/_content/")
            || path.EqualIgnoreCase("/favicon.ico");
    }
}
