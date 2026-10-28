using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server.Services;
using XCode.Membership;

namespace Stardust.Server.Controllers;

/// <summary>浏览器埋点接口。为前端 SDK 提供配置下发与短期令牌签发</summary>
/// <remarks>
/// 两个端点都面向浏览器匿名访问：
/// - GET /stardust/browser-config 返回同源配置（不含任何密钥），可缓存 5 分钟；
/// - POST /stardust/browser-token 校验应用与来源后签发 10 分钟短期令牌，仅用于埋点上报。
/// 令牌为不透明随机串，不能当作应用令牌去访问其它受保护接口。
/// </remarks>
[ApiController]
[Route("stardust")]
[ApiFilter]
public class BrowserController(BrowserTokenService browserToken, StarServerSetting setting) : ControllerBase
{
    /// <summary>获取浏览器埋点配置</summary>
    /// <param name="appId">应用名。为空时返回通用配置，不校验应用</param>
    /// <returns>前端 SDK 所需配置，含服务地址、令牌地址与采样率</returns>
    [HttpGet("browser-config")]
    public Object GetConfig(String appId)
    {
        App app = null;
        if (!appId.IsNullOrEmpty())
        {
            app = App.FindByName(appId);
            if (app == null) throw new ApiException(ApiCode.NotFound, L.T("Invalid application [{0}]", appId));
            if (!app.Enable) throw new ApiException(ApiCode.Forbidden, L.T("Application [{0}] is disabled", appId));
        }

        // 配置不含密钥，允许浏览器与 CDN 缓存 5 分钟
        Response.Headers.CacheControl = "public, max-age=300";

        return new
        {
            appId = app?.Name ?? appId,
            server = $"{Request.Scheme}://{Request.Host}",
            release = "",
            environment = "",
            tokenUrl = "/stardust/browser-token",
            sampleRate = 1.0,
            tracePropagationTargets = new String[0],
            ignoreUrls = new String[0],
            disabled = new String[0],
        };
    }

    /// <summary>签发浏览器埋点短期令牌</summary>
    /// <param name="model">令牌申请模型，含应用名</param>
    /// <returns>短期令牌与有效期（秒）</returns>
    [HttpPost("browser-token")]
    public Object PostToken([FromBody] BrowserTokenModel model)
    {
        var appId = model?.AppId;
        if (appId.IsNullOrEmpty()) throw new ApiException(ApiCode.BadRequest, L.T("Invalid application [{0}]", appId));

        // 浏览器上下文必须带 Origin 或 Referer，且命中 CORS 白名单（未配置白名单时放行任意来源）
        var origin = GetOrigin();
        if (origin.IsNullOrEmpty()) throw new ApiException(ApiCode.Forbidden, L.T("Illegal source; registration is forbidden."));

        var app = App.FindByName(appId);
        if (app == null) throw new ApiException(ApiCode.Forbidden, L.T("Invalid application [{0}]", appId));
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, L.T("Application [{0}] is disabled", appId));

        var ip = HttpContext.GetUserHost();
        if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;
        if (!app.MatchIp(ip)) throw new ApiException(ApiCode.Forbidden, L.T("Application [{0}] forbids access from {1}.", appId, ip));

        var token = browserToken.Issue(app, ip);

        return new
        {
            appId = app.Name,
            token,
            expire = browserToken.Expire,
            server = $"{Request.Scheme}://{Request.Host}",
        };
    }

    /// <summary>获取并校验请求来源。返回规范化后的来源（协议+主机+端口），不合法时返回 null</summary>
    /// <returns>来源字符串或 null</returns>
    private String GetOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (origin.IsNullOrEmpty()) origin = Request.Headers["Referer"].ToString();
        if (origin.IsNullOrEmpty()) return null;

        // Origin 形如 https://a.com[:port]，Referer 为完整地址，统一取协议+主机+端口比较
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return null;

        var source = $"{uri.Scheme}://{uri.Authority}";

        // 未配置白名单时与 CORS 策略保持一致，放行任意来源
        var allowed = setting.CorsOrigins;
        if (allowed.IsNullOrEmpty()) return source;

        foreach (var item in allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item == "*" || item.EqualIgnoreCase(source)) return source;
        }

        return null;
    }
}
