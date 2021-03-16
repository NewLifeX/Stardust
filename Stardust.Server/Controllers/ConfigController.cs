using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;
using AppService = Stardust.Server.Services.AppService;

namespace Stardust.Server.Controllers
{
    /// <summary>配置中心服务。向应用提供配置服务</summary>
    [Route("[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _configService;
        private readonly AppService _appService;

        public ConfigController(ConfigService configService, AppService appService)
        {
            _configService = configService;
            _appService = appService;
        }

        [ApiFilter]
        public ConfigInfo GetAll(String appId, String secret, String token, String scope, Int32 version)
        {
            if (appId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));

            // 验证
            var app = Valid(appId, secret, token);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (version > 0 && version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

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

        [ApiFilter]
        [HttpPost]
        public ConfigInfo GetAll([FromBody] ConfigInModel model, String token)
        {
            if (model.AppId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.AppId));

            // 验证
            var app = Valid(model.AppId, model.Secret, token);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 使用键和缺失键
            if (!model.UsedKeys.IsNullOrEmpty()) app.UsedKeys = model.UsedKeys;
            if (!model.MissedKeys.IsNullOrEmpty()) app.MissedKeys = model.MissedKeys;
            app.Update();

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (model.Version > 0 && model.Version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            var scope = model.Scope;
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

        private AppConfig Valid(String appId, String secret, String token)
        {
            if (appId.IsNullOrEmpty() && !token.IsNullOrEmpty())
            {
                var ap = _appService.DecodeToken(token, Setting.Current);
                appId = ap?.Name;
            }

            var app = AppConfig.FindByName(appId);
            if (app == null)
            {
                var ap = _appService.Authorize(appId, secret, true);

                app = new AppConfig
                {
                    Name = ap.Name,
                    Enable = ap.Enable,
                };

                app.Insert();
            }

            return app;
        }
    }
}