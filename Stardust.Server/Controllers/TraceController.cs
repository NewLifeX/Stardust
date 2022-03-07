using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Monitors;
using Stardust.Server.Common;
using Stardust.Server.Services;
using XCode;
using XCode.Membership;

namespace Stardust.Server.Controllers
{
    //[ApiController]
    [Route("[controller]")]
    public class TraceController : ControllerBase
    {
        private readonly TokenService _service;
        private readonly ITraceStatService _stat;
        private readonly IAppDayStatService _appStat;
        private readonly ITraceItemStatService _itemStat;
        private static readonly ICache _cache = new MemoryCache();

        public TraceController(ITraceStatService stat, IAppDayStatService appStat, ITraceItemStatService itemStat, TokenService appService)
        {
            _stat = stat;
            _appStat = appStat;
            _itemStat = itemStat;
            _service = appService;
        }

        [ApiFilter]
        [HttpPost(nameof(Report))]
        public TraceResponse Report([FromBody] TraceModel model, String token)
        {
            var builders = model?.Builders;
            if (model == null || model.AppId.IsNullOrEmpty()) return null;

            var ip = HttpContext.GetUserHost();
            if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;

            var set = Setting.Current;

            // 新版验证方式，访问令牌
            App ap = null;
            var clientId = model.ClientId;
            if (!token.IsNullOrEmpty() && token.Split(".").Length == 3)
            {
                var (jwt, app2) = _service.DecodeToken(token, set.TokenSecret);
                //if (ap == null || ap.Name != model.AppId) throw new InvalidOperationException($"授权不匹配[{model.AppId}]!=[{ap?.Name}]！");
                if (app2 == null) throw new InvalidOperationException($"授权不匹配[{model.AppId}]!=[{app2?.Name}]！");

                ap = app2;
                if (clientId.IsNullOrEmpty()) clientId = jwt.Id;
            }
            App.UpdateInfo(model, ip);
            AppOnline.UpdateOnline(ap, clientId, ip, token, model.Info);

            // 该应用的追踪配置信息
            var app = AppTracer.FindByName(model.AppId);
            if (app == null)
            {
                app = new AppTracer
                {
                    Name = model.AppId,
                    DisplayName = model.AppName,
                    AppId = ap.Id,
                    Enable = set.AutoRegister,
                };
                app.Save();
            }

            // 校验应用
            if (app == null || !app.Enable) throw new Exception($"无效应用[{model.AppId}/{model.AppName}]");

            // 插入数据
            if (builders != null && builders.Length > 0) Task.Run(() => ProcessData(app, model, ip, builders));

            // 构造响应
            var rs = new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
                Timeout = app.Timeout,
                //Excludes = app.Excludes?.Split(",", ";"),
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

        private void ProcessData(AppTracer app, TraceModel model, String ip, ISpanBuilder[] builders)
        {
            // 排除项
            var excludes = app.Excludes.Split(",", ";") ?? new String[0];
            var timeoutExcludes = app.TimeoutExcludes.Split(",", ";") ?? new String[0];

            var now = DateTime.Now;
            var startTime = now.AddDays(-Setting.Current.DataRetention);
            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                // 剔除指定项
                if (item.Name.IsNullOrEmpty()) continue;
                if (excludes != null && excludes.Any(e => e.IsMatch(item.Name))) continue;
                //if (item.Name.EndsWithIgnoreCase("/Trace/Report")) continue;

                // 拒收超期数据
                if (item.StartTime.ToDateTime().ToLocalTime() < startTime) continue;

                // 检查跟踪项
                var ti = app.GetOrAddItem(item.Name);
                if (ti == null || !ti.Enable) continue;

                // 拒收超长项
                if (item.Name.Length > TraceData._.Name.Length) continue;

                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ItemId = ti.Id;
                td.ClientId = model.ClientId ?? ip;
                td.CreateIP = ip;
                td.CreateTime = now;

                traces.Add(td);

                //samples.AddRange(SampleData.Create(td, item.Samples, true));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples, false));

                var isTimeout = app.Timeout > 0 && !timeoutExcludes.Any(e => e.IsMatch(item.Name));
                if (item.Samples != null && item.Samples.Count > 0)
                {
                    // 超时处理为异常，累加到错误数之中
                    if (isTimeout) td.Errors += item.Samples.Count(e => e.EndTime - e.StartTime > app.Timeout);

                    samples.AddRange(SampleData.Create(td, item.Samples, true));
                }

                // 如果最小耗时都超过了超时设置，则全部标记为错误
                if (isTimeout && td.MinCost >= app.Timeout && td.Errors < td.Total) td.Errors = td.Total;
            }

            // 分表
            var time = builders[0].StartTime.ToDateTime().ToLocalTime();
            {
                using var split = TraceData.Meta.CreateShard(time);

                traces.Insert(true);
            }
            {
                using var split = SampleData.Meta.CreateShard(time);

                samples.Insert(true);
            }

            // 更新统计
            _stat.Add(traces);
            _appStat.Add(now.Date);
            if (now.Hour == 0 && now.Minute <= 10) _appStat.Add(now.Date.AddDays(-1));
            _itemStat.Add(app.ID);

            if (!ip.IsNullOrEmpty() && ip.Length >= 3)
            {
                // 应用节点数
                var nodes = app.Nodes?.Split(",").ToList() ?? new List<String>();
                if (!nodes.Contains(ip))
                {
                    // 如果超过一定时间没有更新，则刷新它
                    if (_cache.Add("appNodes:" + app.ID, 1, 3600)) nodes.Clear();

                    nodes.Insert(0, ip);
                    if (nodes.Count > 32) nodes = nodes.Take(32).ToList();

                    // 排序，避免Nodes字段频繁更新
                    app.Nodes = nodes.OrderBy(e => e).Join();
                    app.SaveAsync();
                }
            }
        }
    }
}