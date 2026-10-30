using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using Stardust.Server;
using Stardust.Web.Services;
using XCode.Membership;

namespace Stardust.Web.Filters;

/// <summary>调用链访问授权。登录用户、合法分享令牌、匿名开关三者其一即可访问</summary>
/// <remarks>
/// 自定义页面不会自动继承实体控制器的鉴权，统一在此判定，避免各Action重复判断而漏检。
/// 未登录访问一律跳转登录页，登录后回到原始地址。
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class TraceAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>授权发生时触发</summary>
    /// <param name="context">授权上下文</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (Resolve(http, ManageProvider.User != null) == TraceAccessModes.None)
            context.Result = new RedirectResult($"/Admin/User/Login?r={Uri.EscapeDataString(http.Request.Path + http.Request.QueryString)}");
    }

    /// <summary>判定访问方式，并把访问上下文写入 <see cref="HttpContext.Items"/></summary>
    /// <param name="http">HTTP上下文</param>
    /// <param name="signedIn">是否已登录。由调用方传入，便于脱离会员体系做单元测试</param>
    /// <returns>访问方式</returns>
    public static TraceAccessModes Resolve(HttpContext http, Boolean signedIn)
    {
        var ids = TraceAccessPolicy.ParseTraceIds(http.Request.Query["id"] + "");
        var token = http.Request.Query["token"] + "";
        var tokens = http.RequestServices.GetService<TraceShareTokenService>();

        // 分享令牌只绑定单个追踪编号，多编号查询不适用
        var tokenValid = ids.Length == 1 && tokens != null && tokens.Validate(ids[0], token);

        var mode = TraceAccessPolicy.Evaluate(signedIn, tokenValid, StarServerSetting.Current.TraceAnonymous);
        if (mode == TraceAccessModes.None) return mode;

        // 未登录时只读：不写备份表，也不更新埋点最后访问的追踪编号
        http.Items[nameof(TraceAccessModes)] = mode;
        http.Items["TraceId"] = String.Join(",", ids);
        http.Items["TraceReadOnly"] = !signedIn;

        // 令牌过期时间。登录用户查看自己的分享链接时也要展示，便于确认链接有效期
        var expire = TraceShareTokenService.GetExpire(token);
        if (expire != null) http.Items["TraceExpire"] = expire;

        // 分享与匿名页面不应被搜索引擎收录，链路数据也不应被中间层缓存
        http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        http.Response.Headers["Cache-Control"] = "no-store";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";

        return mode;
    }
}
