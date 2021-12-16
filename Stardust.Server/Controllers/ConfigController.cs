using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;
using TokenService = Stardust.Server.Services.TokenService;

namespace Stardust.Server.Controllers
{
    /// <summary>配置中心服务。向应用提供配置服务</summary>
    [Route("[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _configService;
        private readonly TokenService _tokenService;

        public ConfigController(ConfigService configService, TokenService tokenService)
        {
            _configService = configService;
            _tokenService = tokenService;
        }

        [ApiFilter]
        public ConfigInfo GetAll(String appId, String secret, String token, String scope, Int32 version)
        {
            if (appId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));

            // 验证
            var (app, online) = Valid(appId, secret, token);
            var ip = HttpContext.GetUserHost();

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (version > 0 && version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip) : scope;
            online.Scope = scope;
            online.SaveAsync();

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

        [ApiFilter]
        [HttpPost]
        public ConfigInfo GetAll([FromBody] ConfigInModel model, String token)
        {
            if (model.AppId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.AppId));

            // 验证
            var (app, online) = Valid(model.AppId, model.Secret, token);
            var ip = HttpContext.GetUserHost();

            // 使用键和缺失键
            if (!model.UsedKeys.IsNullOrEmpty()) app.UsedKeys = model.UsedKeys;
            if (!model.MissedKeys.IsNullOrEmpty()) app.MissedKeys = model.MissedKeys;
            app.Update();

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (model.Version > 0 && model.Version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            var scope = model.Scope;
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip) : scope;
            online.Scope = scope;
            online.SaveAsync();

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

        private (AppConfig, ConfigOnline) Valid(String appId, String secret, String token)
        {
            if (appId.IsNullOrEmpty() && !token.IsNullOrEmpty())
            {
                var ap1 = _tokenService.DecodeToken(token, Setting.Current);
                appId = ap1?.Name;
            }

            var ap = _tokenService.Authorize(appId, secret, Setting.Current.AutoRegister);

            var app = AppConfig.FindByName(appId);
            if (app == null) app = AppConfig.Find(AppConfig._.Name == appId);
            if (app == null)
            {
                var obj = AppConfig.Meta.Table;
                lock (obj)
                {
                    app = AppConfig.FindByName(appId);
                    if (app == null)
                    {
                        app = new AppConfig
                        {
                            Name = ap.Name,
                            Enable = ap.Enable,
                        };

                        app.Insert();
                    }
                }
            }

            // 更新心跳信息
            var ip = HttpContext.GetUserHost();
            var online = ConfigOnline.UpdateOnline(app, ip, token);

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

            return (app, online);
        }

        [ApiFilter]
        [HttpPost]
        public Int32 SetAll([FromBody] SetConfigModel model, String token)
        {
            if (model.AppId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.AppId));

            // 验证
            var (app, online) = Valid(model.AppId, model.Secret, token);
            if (app.Readonly) throw new Exception($"应用[{app}]处于只读模式，禁止修改");

            return _configService.SetConfigs(app, model.Configs);
        }
    }
}