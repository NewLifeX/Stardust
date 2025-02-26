using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Remoting.Extensions;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Monitors;
using Stardust.Server.Services;
using XCode;
using XCode.Membership;

namespace Stardust.Server.Controllers;

//[ApiController]
[Route("[controller]")]
public class TraceController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly AppOnlineService _appOnline;
    private readonly UplinkService _uplink;
    private readonly MonitorService _monitorService;
    private readonly StarServerSetting _setting;
    private readonly ICacheProvider _cacheProvider;
    private readonly ITracer _tracer;
    private readonly ITraceStatService _stat;
    private readonly IAppDayStatService _appStat;
    private readonly ITraceItemStatService _itemStat;

    public TraceController(ITraceStatService stat, IAppDayStatService appStat, ITraceItemStatService itemStat, TokenService tokenService, AppOnlineService appOnline, UplinkService uplink, MonitorService monitorService, StarServerSetting setting, ICacheProvider cacheProvider, ITracer tracer)
    {
        _stat = stat;
        _appStat = appStat;
        _itemStat = itemStat;
        _tokenService = tokenService;
        _appOnline = appOnline;
        _uplink = uplink;
        _monitorService = monitorService;
        _setting = setting;
        _cacheProvider = cacheProvider;
        _tracer = tracer;
    }

    [ApiFilter]
    [HttpPost(nameof(Report))]
    public TraceResponse Report([FromBody] TraceModel model, String token)
    {
        var builders = model?.Builders;
        if (model == null || model.AppId.IsNullOrEmpty()) return null;

        var ip = HttpContext.GetUserHost();
        if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;

        using var span = _tracer?.NewSpan($"traceReport-{model.AppId}", new { ip, model.ClientId, count = model.Builders?.Length, names = model.Builders?.Join(",", e => e.Name) }, builders?.Length ?? 0);

        // 验证
        var (app, online) = Valid(model.AppId, model, model.ClientId, ip, token);

        // 插入数据
        //if (builders != null && builders.Length > 0) Task.Run(() => ProcessData(app, model, online?.NodeId ?? 0, ip, builders));
        if (builders != null && builders.Length > 0) ProcessData(app, model, online?.NodeId ?? 0, ip, builders);

        // 构造响应
        var rs = new TraceResponse
        {
            Period = app.Period,
            MaxSamples = app.MaxSamples,
            MaxErrors = app.MaxErrors,
            Timeout = app.Timeout,
            //Excludes = app.Excludes?.Split(",", ";"),
            MaxTagLength = app.MaxTagLength,
            RequestTagLength = app.RequestTagLength,
            EnableMeter = app.EnableMeter,
        };

        // Vip客户端。高频次大样本采样，10秒100次，逗号分割，支持*模糊匹配
        if (app.IsVip(model.ClientId))
        {
            rs.Period = 10;
            rs.MaxSamples = 100;
        }

        // 新版本才返回Excludes，老版本客户端在处理Excludes时有BUG，错误处理/
        if (!model.Version.IsNullOrEmpty()) rs.Excludes = app.Excludes?.Split(",", ";");

        return rs;
    }

    [ApiFilter]
    [HttpPost(nameof(ReportRaw))]
    public async Task<TraceResponse> ReportRaw(String token)
    {
        var req = Request;
        if (req.ContentLength <= 0) return null;

        var ms = Pool.MemoryStream.Get();
        if (req.ContentType == "application/x-gzip")
        {
            using var gs = new GZipStream(req.Body, CompressionMode.Decompress);
            await gs.CopyToAsync(ms);
        }
        else
        {
            await req.Body.CopyToAsync(ms);
        }

        ms.Position = 0;
        var body = ms.Return(true).ToStr();
        var model = body.ToJsonEntity<TraceModel>();

        return Report(model, token);
    }

    private (AppTracer, AppOnline) Valid(String appId, TraceModel model, String clientId, String ip, String token)
    {
        var set = _setting;

        // 新版验证方式，访问令牌
        App ap = null;
        if (!token.IsNullOrEmpty() && token.Split(".").Length == 3)
        {
            var (jwt, ap1) = _tokenService.DecodeToken(token, set.TokenSecret);
            if (appId.IsNullOrEmpty()) appId = ap1?.Name;
            if (clientId.IsNullOrEmpty()) clientId = jwt.Id;

            ap = ap1;
        }

        //ap = _tokenService.Authorize(appId, null, set.AutoRegister);
        ap ??= App.FindByName(model.AppId);

        // 新建应用配置
        var app = AppTracer.FindByName(appId);
        app ??= AppTracer.Find(AppTracer._.Name == appId);
        if (app == null)
        {
            var obj = AppTracer.Meta.Table;
            lock (obj)
            {
                app = AppTracer.FindByName(appId);
                if (app == null)
                {
                    app = new AppTracer
                    {
                        Name = model.AppId,
                        DisplayName = model.AppName,
                        //AppId = ap.Id,
                        //Enable = ap.Enable,
                    };
                    if (ap != null)
                    {
                        app.AppId = ap.Id;
                        app.Enable = ap.Enable;
                        app.Category = ap.Category;
                    }
                    else
                    {
                        app.Enable = set.AppAutoRegister;
                    }

                    app.Insert();
                }
            }
        }

        if (ap != null)
        {
            if (ap.DisplayName.IsNullOrEmpty() || ap.DisplayName == ap.Name) ap.DisplayName = model.AppName;

            // 双向同步应用分类
            if (!ap.Category.IsNullOrEmpty())
                app.Category = ap.Category;
            else if (!app.Category.IsNullOrEmpty())
            {
                ap.Category = app.Category;
                ap.Update();
            }

            if (app.AppId == 0) app.AppId = ap.Id;
            if (app.DisplayName.IsNullOrEmpty() || app.DisplayName == app.Name) app.DisplayName = ap.DisplayName;
            app.Update();
        }

        //var ip = HttpContext.GetUserHost();
        if (clientId.IsNullOrEmpty()) clientId = ip;

        // 收集应用性能信息
        if (app.EnableMeter) App.WriteMeter(model, ip);

        // 更新心跳信息
        var online = _appOnline.UpdateOnline(ap, clientId, ip, token, model.Info);

        // 检查应用有效性
        if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

        return (app, online);
    }

    private void ProcessData(AppTracer app, TraceModel model, Int32 nodeId, String ip, ISpanBuilder[] builders)
    {
        try
        {
            // 排除项
            var excludes = app.Excludes.Split(",", ";") ?? [];
            //var timeoutExcludes = app.TimeoutExcludes.Split(",", ";") ?? new String[0];

            var now = DateTime.Now;
            var startTime = now.AddDays(-_setting.DataRetention);
            var endTime = now.AddDays(1);
            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                // 剔除指定项
                if (item.Name.IsNullOrEmpty()) continue;

                // 跟踪规则
                var rule = TraceRule.Match(item.Name);
                if (rule != null && !rule.IsWhite)
                {
                    using var span = _tracer?.NewSpan("trace:BlackList", new { item.Name, rule.Rule, ip });
                    continue;
                }

                if (excludes != null && excludes.Any(e => e.IsMatch(item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    using var span = _tracer?.NewSpan("trace:Exclude", new { item.Name, ip });
                    continue;
                }
                //if (item.Name.EndsWithIgnoreCase("/Trace/Report")) continue;

                // 拒收超期数据，拒收未来数据
                var timestamp = item.StartTime.ToDateTime().ToLocalTime();
                if (timestamp < startTime || timestamp > endTime)
                {
                    using var span = _tracer?.NewSpan("trace:ErrorTime", new { item.Name, timestamp, ip, item });
                    continue;
                }

                // 拒收超长项
                if (item.Name.Length > TraceData._.Name.Length)
                {
                    using var span = _tracer?.NewSpan("trace:LongName", new { item.Name, ip });
                    continue;
                }

                // 检查跟踪项
                var key = $"trace:Item:{app.ID}-{item.Name}";
                var ti = _cacheProvider.InnerCache.Get<TraceItem>(key);
                try
                {
                    // 捕获异常，避免因为跟踪项错误导致整体跟踪失败
                    ti ??= app.GetOrAddItem(item.Name, rule?.IsWhite);
                }
                catch { }
                if (ti == null)
                {
                    using var span = _tracer?.NewSpan("trace:ErrorItem", item.Name);
                    continue;
                }
                _cacheProvider.InnerCache.Set(key, ti, 600);
                if (!ti.Enable) continue;

                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ItemId = ti.Id;
                td.NodeId = nodeId;
                td.ClientId = model.ClientId ?? ip;
                td.CreateIP = ip;
                td.CreateTime = now;

                traces.Add(td);

                //samples.AddRange(SampleData.Create(td, item.Samples, true));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples, false));

                // 超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时
                var timeout = ti.Timeout;
                //if (timeout == 0) timeout = app.Timeout;

                var isTimeout = timeout > 0;
                if (item.Samples != null && item.Samples.Count > 0)
                {
                    // 超时处理为异常，累加到错误数之中
                    if (isTimeout) td.Errors += item.Samples.Count(e => e.EndTime - e.StartTime > timeout);

                    samples.AddRange(SampleData.Create(td, item.Samples, true));
                }

                // 如果最小耗时都超过了超时设置，则全部标记为错误
                if (isTimeout && td.MinCost >= timeout && td.Errors < td.Total) td.Errors = td.Total;

                // 处理克隆。拷贝一份入库，归属新的跟踪项，但名称不变
                foreach (var elm in app.GetClones(item.Name, model.ClientId))
                {
                    var td2 = td.CloneEntity(true);
                    td2.Id = 0;
                    td2.ItemId = elm.Id;
                    td2.LinkId = td.Id;

                    traces.Add(td2);
                }
            }

            // 更新XCode后，支持批量插入的自动分表，内部按照实体类所属分表进行分组插入
            traces.Insert(true);
            samples.Insert(true);

            // 更新统计
            _stat.Add(traces);
            _appStat.Add(now.Date);
            if (now.Hour == 0 && now.Minute <= 10) _appStat.Add(now.Date.AddDays(-1));
            _itemStat.Add(app.ID);

            // 发送给上联服务器
            _uplink.Report(app, model);

            // WebHook
            if (!app.WebHook.IsNullOrEmpty()) _monitorService.WebHook(app, model);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }
}