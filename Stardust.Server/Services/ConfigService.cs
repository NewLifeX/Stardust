using System;
using NewLife;
using Stardust.Data;
using Stardust.Data.Configs;

namespace Stardust.Server.Services
{
    public class ConfigService
    {
        /// <summary>为应用解析指定键的值，处理内嵌</summary>
        /// <param name="app"></param>
        /// <param name="cfg"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public String Resolve(App app, ConfigData cfg, String scope)
        {
            var value = cfg?.Value;
            if (value.IsNullOrEmpty()) return value;

            // 要求内嵌全部解析
            var p = 0;
            while (true)
            {
                var p1 = value.IndexOf("${", p);
                if (p1 < 0) break;

                var p2 = value.IndexOf('}', p1 + 2);
                if (p2 < 0) break;

                // 替换
                var item = value.Substring(p1 + 2, p2 - p1 - 2);
                // 拆分 ${key@app:scope}
                var ss = item.Split("@", ":");
                var key2 = ss[0];
                var app2 = ss.Length > 1 ? ss[1] : "";
                var scope2 = ss.Length > 2 ? ss[2] : "";

                //item = replace(key, app, scope) + "";
                {
                    var ap2 = App.FindByName(app2);
                    var cfg2 = ConfigData.Acquire((ap2 ?? app).ID, key2, scope2 ?? scope);
                    if (cfg2 == null) throw new Exception($"在应用[{app}]的[{cfg.Key}]中无法解析[{item}]");

                    item = Resolve(ap2 ?? app, cfg2, scope2 ?? scope);
                }

                // 重新组合
                var left = value.Substring(0, p1);
                var right = value.Substring(p2 + 1);
                value = left + item + right;

                // 移动游标，加速下一次处理
                p = left.Length + item.Length;
            }

            return value;
        }

        //private String Find(App app1, App app2, String key, String scope)
        //{
        //    if (app2 != null)
        //    {
        //        var list = ConfigData.FindAllValid(app2.ID);
        //    }
        //}

        //public ConfigItem Acquire(String key, App app, String scope, Int32 ver, String ip, Int32 layer = 1)
        //{
        //    var cfg = ConfigData.Acquire(app.ID, key, scope);
        //    if (cfg == null || !cfg.Enable) return null;

        //    // 内部的配置只能用于内嵌，而不能直接请求使用
        //    if (layer == 1 && cfg.Internal) throw new Exception($"安全起见，内部配置[{key}]只能用于内嵌而不能直接请求使用，需要在应用[{app}]下建立配置来引用它[{key}]");

        //    // 版本判断
        //    if (ver > 0 && ver == cfg.Version) return null;

        //    var ci = cfg.ToItem();

        //    // 分析内嵌
        //    ci.Value = BracketHelper.Build(ci.Value, (k, a, s) =>
        //    {
        //        /*
        //         * 1，在内嵌标签指定应用内找
        //         * 2，在当前Key所在应用内找。可能是其它应用共享Key给当前应用
        //         * 3，在当前应用内找
        //         */

        //        var app2 = App.FindByName(a) ?? cfg.App ?? app;
        //        var s2 = s.IsNullOrEmpty() ? scope : s;

        //        // 内嵌配置不再比较版本
        //        var ci2 = Acquire(k, app2, s2, 0, ip, layer + 1);
        //        if (ci2 == null) throw new Exception($"找不到[{key}]的内嵌配置[{k}]，[app={app}][scope={scope}]");

        //        // 不必取最大值，需要刷新配置时，直接刷新所有配置项版本号
        //        //// 取版本号最大值
        //        //if (ci2.Version > ci.Version) ci.Version = ci2.Version;

        //        return ci2.Value;
        //    });

        //    return ci;
        //}
    }
}