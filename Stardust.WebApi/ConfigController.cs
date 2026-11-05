using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Remoting;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Services;
using XCode.Membership;

namespace Stardust.Web.Controllers;

/// <summary>配置中心服务。向应用提供配置服务</summary>
/// <remarks>
/// 对照 MVC 真源 <c>Stardust.Web/Controllers/ConfigController.cs</c> 逐行补齐（2026-11-04 配置中心块 WebApi 补齐）。
/// 与 <c>Stardust.Server/Controllers/ConfigController.cs</c> 的生产向 GetAll 是两个不同鉴权语义的接口：
/// 本版（同 MVC 宿主）面向新前端预览（应用配置列表「预览」链 /config/getall）与指向 6681 宿主的 SDK，
/// 要求 Cube 后台登录态（ManageProvider.User，API 宿主经 Token/Session 自动登录）+ 应用密钥校验（autoRegister=false）；
/// Server 版支持令牌解码/POST 重载/自动注册，由 StarServer 宿主暴露。
/// 鉴权逻辑严格保持 MVC 语义，未做简化。
/// </remarks>
[Route("[controller]/[action]")]
public class ConfigController(ConfigService configService, AppTokenService tokenService, AppOnlineService appOnline) : ControllerBase
{
    [ApiFilter]
    public ConfigInfo GetAll(String appId, String secret, String scope, Int32 version)
    {
        if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));
        if (ManageProvider.User == null) throw new ApiException(ApiCode.Unauthorized, L.T("Not logged in."));

        // 验证
        var app = Valid(appId, secret, out var online);
        var ip = HttpContext.GetUserHost();

        // 版本没有变化时，不做计算处理，不返回配置数据
        if (version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

        // 作用域为空时重写
        scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip, null) : scope;
        online.Scope = scope;

        var dic = configService.GetConfigs(app, scope);

        // 返回WorkerId
        if (app.EnableWorkerId && dic.ContainsKey(configService.WorkerIdName))
            dic[configService.WorkerIdName] = online.WorkerId + "";

        return new ConfigInfo
        {
            Version = app.Version,
            Scope = scope,
            SourceIP = ip,
            NextVersion = app.NextVersion,
            NextPublish = app.PublishTime.ToFullString(""),
            UpdateTime = app.UpdateTime,
            Configs = dic,
        };
    }

    private AppConfig Valid(String appId, String secret, out AppOnline online)
    {
        var ip = HttpContext.GetUserHost();
        var ap = tokenService.Authorize(appId, secret, false, ip);

        var app = AppConfig.FindByName(appId);
        app ??= AppConfig.Find(AppConfig._.Name == appId);
        if (app == null)
        {
            app = new AppConfig
            {
                Name = ap.Name,
                AppId = ap.Id,
                Enable = ap.Enable,
            };

            app.Insert();
        }

        if (app.AppId == 0)
        {
            app.AppId = ap.Id;
            app.Update();
        }

        // 更新心跳信息
        //var ip = HttpContext.GetUserHost();
        online = appOnline.UpdateOnline(ap, null, ip, appId);

        // 检查应用有效性
        if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), L.T("Application [{0}] is disabled.", appId));

        // 刷新WorkerId
        if (app.EnableWorkerId) configService.RefreshWorkerId(app, online);

        return app;
    }
}
