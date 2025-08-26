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
[Route("[controller]/[action]")]
public class ConfigController(ConfigService configService, AppTokenService tokenService, AppOnlineService appOnline) : ControllerBase
{
    [ApiFilter]
    public ConfigInfo GetAll(String appId, String secret, String scope, Int32 version)
    {
        if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));
        if (ManageProvider.User == null) throw new ApiException(ApiCode.Unauthorized, "未登录！");

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
        if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

        // 刷新WorkerId
        if (app.EnableWorkerId) configService.RefreshWorkerId(app, online);

        return app;
    }
}