using System;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Remoting;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;
using XCode.Membership;

namespace Stardust.Web.Controllers
{
    /// <summary>配置中心服务。向应用提供配置服务</summary>
    [Route("[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _configService;

        public ConfigController(ConfigService configService) => _configService = configService;

        [ApiFilter]
        public ConfigInfo GetAll(String appId, String secret, String scope, Int32 version)
        {
            if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));
            if (ManageProvider.User == null) throw new ApiException(403, "未登录！");

            // 验证
            var app = Valid(appId, secret, out var online);
            var ip = HttpContext.GetUserHost();

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip) : scope;
            online.Scope = scope;

            var dic = _configService.GetConfigs(app, scope);

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

        private AppConfig Valid(String appId, String secret, out ConfigOnline online)
        {
            var ap = Authorize(appId, secret, true);

            var app = AppConfig.FindByName(appId);
            if (app == null) app = AppConfig.Find(AppConfig._.Name == appId);
            if (app == null)
            {
                app = new AppConfig
                {
                    Name = ap.Name,
                    Enable = ap.Enable,
                };

                app.Insert();
            }

            // 更新心跳信息
            var ip = HttpContext.GetUserHost();
            online = app.UpdateInfo(ap, ip);

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

            return app;
        }

        private App Authorize(String appId, String secret, Boolean autoRegister)
        {
            if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));
            //if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

            // 查找应用
            var app = App.FindByName(appId);
            //if (app == null) return null;
            if (app == null) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]不存在！");

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");
            if (!app.Secret.IsNullOrEmpty() && secret != app.Secret) throw new InvalidOperationException($"非法访问应用[{appId}]！");

            return app;
        }
    }
}