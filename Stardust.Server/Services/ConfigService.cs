using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using Stardust.Data;
using Stardust.Data.ConfigCenter;
using Stardust.Data.Models;

namespace Stardust.Server.Services
{
    public class ConfigService
    {
        public ConfigItem Acquire(String key, App app, String scope, Int32 ver, String ip, Int32 layer = 1)
        {
            var cfg = ConfigData.Acquire(app.ID, key, scope);
            if (cfg == null || !cfg.Enable) return null;

            // 内部的配置只能用于内嵌，而不能直接请求使用
            if (layer == 1 && cfg.Internal) throw new Exception($"安全起见，内部配置[{key}]只能用于内嵌而不能直接请求使用，需要在应用[{app}]下建立配置来引用它[{key}]");

            // 版本判断
            if (ver > 0 && ver == cfg.Version) return null;

            var ci = cfg.ToItem();

            // 分析内嵌
            ci.Value = BracketHelper.Build(ci.Value, (k, a, s) =>
            {
                /*
                 * 1，在内嵌标签指定应用内找
                 * 2，在当前Key所在应用内找。可能是其它应用共享Key给当前应用
                 * 3，在当前应用内找
                 */

                var app2 = App.FindByName(a) ?? cfg.App ?? app;
                var s2 = s.IsNullOrEmpty() ? scope : s;

                // 内嵌配置不再比较版本
                var ci2 = Acquire(k, app2, s2, 0, ip, layer + 1);
                if (ci2 == null) throw new Exception($"找不到[{key}]的内嵌配置[{k}]，[app={app}][scope={scope}]");

                // 不必取最大值，需要刷新配置时，直接刷新所有配置项版本号
                //// 取版本号最大值
                //if (ci2.Version > ci.Version) ci.Version = ci2.Version;

                return ci2.Value;
            });

            return ci;
        }
    }
}