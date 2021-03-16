using System;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;

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

            // 验证
            var app = Valid(appId, secret);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip) : scope;

            var dic = _configService.GetConfigs(app, scope);

            return new ConfigInfo
            {
                Version = app.Version,
                Scope = scope,
                NextVersion = app.NextVersion,
                NextPublish = app.PublishTime.ToFullString(""),
                UpdateTime = app.UpdateTime,
                Configs = dic,
            };
        }

        private AppConfig Valid(String appId, String secrect)
        {
            var app = AppConfig.FindByName(appId);
            if (app == null)
            {
                var ap = Authorize(appId, secrect, true);

                app = new AppConfig
                {
                    Name = ap.Name,
                    Enable = ap.Enable,
                };

                app.Insert();
            }

            return app;
        }

        private App Authorize(String username, String password, Boolean autoRegister)
        {
            if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
            //if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

            // 查找应用
            var app = App.FindByName(username);
            if (app == null) return null;

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(username), $"应用[{username}]已禁用！");
            if (!app.Secret.IsNullOrEmpty() && password != app.Secret) throw new InvalidOperationException($"非法访问应用[{username}]！");

            return app;
        }
    }
}