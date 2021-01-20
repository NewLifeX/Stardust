using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Server.Common;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>配置中心服务。向应用提供配置服务</summary>
    [Route("[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _configService;

        public ConfigController(ConfigService configService) => _configService = configService;

        [ApiFilter]
        public IDictionary<String, String> GetAll(String appId, String secrect, String scope)
        {
            if (appId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));

            // 验证
            //var ap = Check(app, out var ip);
            var app = App.FindByName(appId);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.ID, ip) : scope;

            var list = ConfigData.FindAllValid(app.ID);

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
            var qs = AppQuote.FindAllByAppId(app.ID);
            foreach (var item in qs)
            {
                foreach (var cfg in ConfigData.FindAllValid(item.TargetAppId))
                {
                    // 跳过内部
                    if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                    // 仅添加还没有的键
                    if (dic.ContainsKey(cfg.Key)) continue;

                    // 为该key选择最合适的值，解析内嵌
                    dic[cfg.Key] = _configService.Resolve(App.FindByID(item.TargetAppId), cfg, scope);
                }
            }

            return dic;
        }
    }
}