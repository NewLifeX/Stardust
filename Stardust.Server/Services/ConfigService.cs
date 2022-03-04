using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Configs;

namespace Stardust.Server.Services
{
    public class ConfigService
    {
        private TimerX _timer;
        private readonly ITracer _tracer;
        public String WorkerIdName { get; set; } = "NewLife.WorkerId";

        public ConfigService(ITracer tracer)
        {
            _tracer = tracer;

            StartTimer();
        }

        /// <summary>为应用解析指定键的值，处理内嵌</summary>
        /// <param name="app"></param>
        /// <param name="cfg"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public String Resolve(AppConfig app, ConfigData cfg, String scope)
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
                    var ap2 = AppConfig.FindByName(app2);
                    var scope3 = !scope2.IsNullOrEmpty() ? scope2 : scope;
                    var cfg2 = ConfigData.Acquire(ap2 ?? app, key2, scope3);
                    if (cfg2 == null) throw new Exception($"在应用[{app}]的[{cfg.Key}]中无法解析[{item}]");

                    item = Resolve(ap2 ?? app, cfg2, scope3);
                }

                // 重新组合
                var left = value[..p1];
                var right = value[(p2 + 1)..];
                value = left + item + right;

                // 移动游标，加速下一次处理
                p = left.Length + item.Length;
            }

            return value;
        }

        public void StartTimer()
        {
            if (_timer == null)
            {
                lock (this)
                {
                    if (_timer == null) _timer = new TimerX(DoConfigWork, null, 15_000, 60_000) { Async = true };
                }
            }
        }

        private void DoConfigWork(Object state)
        {
            var list = AppConfig.FindAll();
            var next = DateTime.MinValue;
            foreach (var item in list)
            {
                if (!item.Enable || item.PublishTime.Year < 2000) continue;

                using var span = _tracer?.NewSpan("AutoPublish", item);

                // 时间到了，发布，或者计算最近一个到期应用
                if (item.PublishTime <= DateTime.Now)
                    item.Publish();
                else if (item.PublishTime < next || next.Year < 2000)
                    next = item.PublishTime;
            }

            // 如果下一个到期应用时间很短，主动调整定时器
            if (next.Year > 2000)
            {
                var ts = next - DateTime.Now;
                if (ts.TotalMilliseconds < _timer.Period) _timer.SetNext((Int32)ts.TotalMilliseconds);
            }
        }

        public IDictionary<String, String> GetConfigs(AppConfig app, String scope)
        {
            using var span = _tracer?.NewSpan(nameof(GetConfigs), $"{app} {scope}");

            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            var list = app.Configs;
            list = ConfigData.SelectScope(list, scope);

            // 本应用
            foreach (var cfg in list)
            {
                // 跳过内部
                if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                // 为该key选择最合适的值，解析内嵌
                dic[cfg.Key] = Resolve(app, cfg, scope);
            }

            // 共享应用
            var qs = app.GetQuotes();
            foreach (var item in qs)
            {
                var list2 = item.Configs;
                list2 = ConfigData.SelectScope(list2, scope);
                foreach (var cfg in list2)
                {
                    // 跳过内部
                    if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                    // 仅添加还没有的键
                    if (dic.ContainsKey(cfg.Key)) continue;

                    // 为该key选择最合适的值，解析内嵌
                    dic[cfg.Key] = Resolve(item, cfg, scope);
                }
            }

            // 全局应用
            foreach (var item in AppConfig.GetValids().Where(e => e.IsGlobal))
            {
                var list2 = item.Configs;
                list2 = ConfigData.SelectScope(list2, scope);
                foreach (var cfg in list2)
                {
                    // 跳过内部
                    if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

                    // 仅添加还没有的键
                    if (dic.ContainsKey(cfg.Key)) continue;

                    // 为该key选择最合适的值，解析内嵌
                    dic[cfg.Key] = Resolve(item, cfg, scope);
                }
            }

            return dic;
        }

        /// <summary>刷新WorkerId</summary>
        /// <param name="app"></param>
        /// <param name="online"></param>
        /// <returns></returns>
        public Int32 RefreshWorkerId(AppConfig app, ConfigOnline online)
        {
            if (!app.EnableWorkerId) return -1;

            var cfg = app.Configs.FirstOrDefault(e => e.Key.EqualIgnoreCase(WorkerIdName));
            if (cfg == null)
            {
                cfg = new ConfigData
                {
                    AppId = app.Id,
                    Key = WorkerIdName,
                    Version = app.Version,
                    Enable = true
                };
            }

            var id = cfg.Value.ToInt();
            id++;

            cfg.Value = id + "";
            cfg.Save();

            online.WorkerId = id;
            online.Save();

            return id;
        }

        public Int32 SetConfigs(AppConfig app, IDictionary<String, Object> configs)
        {
            var ver = app.AcquireNewVersion();

            // 开启事务，整体完成
            using var tran = ConfigData.Meta.CreateTrans();

            var list = ConfigData.FindAllByApp(app.Id);

            // 逐行插入数据，不能覆盖已存在数据
            foreach (var item in configs)
            {
                var data = list.FirstOrDefault(e => e.Key.EqualIgnoreCase(item.Key));
                if (data == null)
                {
                    data = new ConfigData
                    {
                        AppId = app.Id,
                        Key = item.Key,
                        //Value = item.Value + "",
                        Version = ver,
                        Enable = true,
                    };

                    if (item.Value is IDictionary<String, Object> dic)
                    {
                        if (dic.TryGetValue("Value", out var v)) data.Value = v + "";
                        if (dic.TryGetValue("Comment", out v)) data.Remark = v + "";
                    }
                    else if (item.Value is JsonElement json && json.ValueKind == JsonValueKind.Object)
                    {
                        if (json.TryGetProperty("Value", out var v)) data.Value = v + "";
                        if (json.TryGetProperty("Comment", out v)) data.Remark = v + "";
                    }
                    else
                        data.Value = item.Value + "";

                    data.Insert();
                }
                else
                {
                    if (item.Value is IDictionary<String, Object> dic)
                    {
                        if (dic.TryGetValue("Comment", out var v)) data.Remark = v + "";
                    }
                    else if (item.Value is JsonElement json && json.ValueKind == JsonValueKind.Object)
                    {
                        if (json.TryGetProperty("Comment", out var v)) data.Remark = v + "";
                    }

                    data.Update();
                }
            }

            //// 整体发布
            //app.Publish();

            tran.Commit();

            return configs.Count;
        }
    }
}