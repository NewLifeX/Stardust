using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
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
        public ConfigInfo GetAll(String appId, String secrect, String scope, Int32 version)
        {
            if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));

            // 验证
            var app = Valid(appId, secrect);
            //var app = App.FindByName(appId);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 版本没有变化时，不做计算处理，不返回配置数据
            if (version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip) : scope;

            var list = ConfigData.FindAllValid(app.Id);
            list = ConfigData.SelectVersion(list, app.Version);

            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            // 本应用
            foreach (var cfg in list)
            {
                // 跳过内部
                if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                // 为该key选择最合适的值，解析内嵌
                dic[cfg.Key] = _configService.Resolve(app, cfg, scope);
            }

            // 共享应用
            var qs = AppQuote.FindAllByAppId(app.Id);
            foreach (var item in qs)
            {
                var list2 = ConfigData.FindAllValid(item.TargetAppId);
                list2 = ConfigData.SelectVersion(list2, item.TargetApp.Version);
                foreach (var cfg in list2)
                {
                    // 跳过内部
                    if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                    // 仅添加还没有的键
                    if (dic.ContainsKey(cfg.Key)) continue;

                    // 为该key选择最合适的值，解析内嵌
                    dic[cfg.Key] = _configService.Resolve(item.TargetApp, cfg, scope);
                }
            }

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
                var ap = _appService.Authorize(appId, secrect, true);

                app = new AppConfig
                {
                    Id = ap.ID,
                    Name = ap.Name,
                    Enable = ap.Enable,
                };
            }

            return app;
        }
    }
}