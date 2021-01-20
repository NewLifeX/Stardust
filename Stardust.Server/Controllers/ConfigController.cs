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
            //return list.ToDictionary(_ => _.Key, _ => _.Value);

            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            // 本应用
            foreach (var cfg in list)
            {
                // 跳过内部
                if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                // 解析内嵌
                dic[cfg.Key] = _configService.Acquire(app, cfg.Key, cfg.Value, scope);
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
                    if (!dic.ContainsKey(cfg.Key))
                        dic[cfg.Key] = _configService.Acquire(App.FindByID(item.TargetAppId), cfg.Key, cfg.Value, scope);
                }
            }

            return dic;

            //var list = new List<ConfigItem>();
            //var ks = keys.Split(",", ";");
            ////var vs = version.SplitAsInt(",", ";");
            //for (var i = 0; i < ks.Length; i++)
            //{
            //    var ver = -1;
            //    //if (i < vs.Length) ver = vs[i];

            //    // 申请配置
            //    var ci = _configService.Acquire(ks[i], app, scope, ver, ip);
            //    // 只返回新版本
            //    if (ci != null && ver != ci.Version) list.Add(ci);
            //}
            ////if (list.Count == 0) throw new InvalidOperationException("配置未找到或未启用！");

            //return list;
        }
    }
}