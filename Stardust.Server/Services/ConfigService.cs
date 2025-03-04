using System.Text.Json;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Configs;

namespace Stardust.Server.Services;

public class ConfigService
{
    private TimerX _timer;
    private readonly StarFactory _starFactory;
    private readonly ICacheProvider _cacheService;
    private readonly ITracer _tracer;
    public String WorkerIdName { get; set; } = "NewLife.WorkerId";

    public ConfigService(StarFactory starFactory, ICacheProvider cacheService, ITracer tracer)
    {
        _starFactory = starFactory;
        _cacheService = cacheService;
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
            var ss = item.Split('@');
            var key2 = ss[0];
            var app2 = ss.Length > 1 ? ss[1] : "";

            var scope2 = "";
            ss = app2.Split(':');
            if (ss != null && ss.Length > 1)
            {
                app2 = ss[0];
                scope2 = ss[1];
            }

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
                _timer ??= new TimerX(DoConfigWork, null, 15_000, 60_000) { Async = true };
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

    public IDictionary<String, String> GetConfigs(AppConfig config, String scope)
    {
        using var span = _tracer?.NewSpan(nameof(GetConfigs), $"{config} {scope}");

        var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        var list = config.Configs;
        list = ConfigData.SelectScope(list, scope);

        // 本应用
        foreach (var cfg in list)
        {
            // 跳过内部
            if (cfg.Key.IsNullOrEmpty() || cfg.Key[0] == '_') continue;

            // 为该key选择最合适的值，解析内嵌
            dic[cfg.Key] = Resolve(config, cfg, scope);
        }

        // 去重
        var ids = new List<Int32> { config.Id };

        // 共享应用
        var qs = config.GetQuotes();
        foreach (var item in qs)
        {
            if (ids.Contains(item.Id)) continue;
            ids.Add(item.Id);

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
            if (ids.Contains(item.Id)) continue;
            ids.Add(item.Id);

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

    /// <summary>刷新WorkerId，作为雪花Id唯一标识</summary>
    /// <param name="config"></param>
    /// <param name="online"></param>
    /// <returns></returns>
    public Int32 RefreshWorkerId(AppConfig config, AppOnline online)
    {
        if (!config.EnableWorkerId) return -1;

        using var span = _tracer?.NewSpan(nameof(RefreshWorkerId), new { config.Id, config.Name });

        // 分布式锁，避免抢占
        var key = $"{WorkerIdName}:{config.Id}";
        using var dlock = _cacheService.AcquireLock($"lock:{key}", 3_000);
        var cache = _cacheService.Cache;

        //// 打开事务保护，确保生成唯一标识
        //using var tran = ConfigData.Meta.CreateTrans();

        // 获取该应用所有在线实例，确保WorkerId唯一
        var force = false;
        var ins = AppOnline.FindAllByApp(config.AppId);
        if (ins.Any(e => e.Id != online.Id && e.WorkerId == online.WorkerId)) force = true;

        // 重新获取在线对象，可能位于对象缓存
        var id = 0;
        var olt = AppOnline.FindById(online.Id);
        if (force || olt.WorkerId <= 0)
        {
            // 找到该Key，不考虑Scope，做跨域全局配置
            var list = ConfigData.FindAllByConfigIdAndKey(config.Id, WorkerIdName);
            var cfg = list.FirstOrDefault();
            cfg ??= new ConfigData
            {
                ConfigId = config.Id,
                Key = WorkerIdName,
                Version = config.Version,
                Enable = true
            };

            // 生成新的唯一标识
            //id = cfg.Value.ToInt();
            //id++;
            // 借助分布式缓存生成递增的唯一标识，有可能缓存没有值，需要先设置一次
            cache.Add(key, cfg.Value.ToInt(), 24 * 3600);
            id = (Int32)cache.Increment(key, 1);

            cfg.Value = id + "";
            cfg.Save();

            olt.WorkerId = id;
            olt.Update();

            // 外部对象也要更新，可能是来自缓存的另一个实例。最终取该实例的数据返回给客户端应用
            online.WorkerId = id;
        }

        //// 提交事务
        //tran.Commit();

        return id;
    }

    public Int32 SetConfigs(AppConfig config, IDictionary<String, Object> configs)
    {
        var ver = config.AcquireNewVersion();

        // 开启事务，整体完成
        using var tran = ConfigData.Meta.CreateTrans();

        var list = ConfigData.FindAllByApp(config.Id);

        // 逐行插入数据，不能覆盖已存在数据
        foreach (var item in configs)
        {
            var data = list.FirstOrDefault(e => e.Key.EqualIgnoreCase(item.Key));
            if (data == null)
            {
                data = new ConfigData
                {
                    ConfigId = config.Id,
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

    public async Task<Int32> Publish(Int32 appId)
    {
        using var span = _tracer?.NewSpan(nameof(Publish), appId + "");
        try
        {
            var app = AppConfig.FindById(appId) ?? throw new ArgumentNullException(nameof(appId));

            if (app.Version >= app.NextVersion) throw new ApiException(701, "已经是最新版本！");
            app.Publish();

            await _starFactory.SendAppCommand(app.Name, null, "config/publish", "");
            var rs = 1;

            // 通知下游依赖应用
            if (app.CanBeQuoted)
            {
                foreach (var item in app.GetChilds())
                {
                    if (item.Enable)
                    {
                        _ = _starFactory.SendAppCommand(item.Name, null, "config/publish", "");
                        rs++;
                    }
                }
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
}