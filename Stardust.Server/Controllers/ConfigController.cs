using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Data;
using Stardust.Data.ConfigCenter;
using Stardust.Data.Models;
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
        public Object GetAll(String appId, String keys, Int32 version, String scope = null)
        {
            if (keys.IsNullOrEmpty()) throw new ArgumentNullException(nameof(keys));

            // 验证
            //var ap = Check(app, out var ip);
            var ap = App.FindByName(appId);
            var ip = HttpContext.Connection?.RemoteIpAddress + "";

            // 作用域为空时重写
            scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(ap.ID, ip) : scope;

            var list = new List<ConfigItem>();
            var ks = keys.Split(",", ";");
            //var vs = version.SplitAsInt(",", ";");
            for (var i = 0; i < ks.Length; i++)
            {
                var ver = -1;
                //if (i < vs.Length) ver = vs[i];

                // 申请配置
                var ci = _configService.Acquire(ks[i], ap, scope, ver, ip);
                // 只返回新版本
                if (ci != null && ver != ci.Version) list.Add(ci);
            }
            //if (list.Count == 0) throw new InvalidOperationException("配置未找到或未启用！");

            return list;
        }
    }
}