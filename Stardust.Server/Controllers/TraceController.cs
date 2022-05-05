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
        private readonly TokenService _tokenService;
        private readonly AppOnlineService _appOnline;
        private readonly ITraceStatService _stat;
        private readonly IAppDayStatService _appStat;
        private readonly ITraceItemStatService _itemStat;
        private static readonly ICache _cache = new MemoryCache();

        public TraceController(ITraceStatService stat, IAppDayStatService appStat, ITraceItemStatService itemStat, TokenService tokenService, AppOnlineService appOnline)
        {
            _stat = stat;
            _appStat = appStat;
            _itemStat = itemStat;
            _tokenService = tokenService;
            _appOnline = appOnline;
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

            // 验证
            var (app, online) = Valid(model.AppId, model, model.ClientId, token);

            // 插入数据
            if (builders != null && builders.Length > 0) Task.Run(() => ProcessData(app, model, ip, builders));

            // 构造响应
            var rs = new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
                Timeout = app.Timeout,
                Excludes = app.Excludes?.Split(",", ";"),
                MaxTagLength = app.MaxTagLength,
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

        private (AppTracer, AppOnline) Valid(String appId, TraceModel model, String clientId, String token)
        {
            var set = Setting.Current;

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
            if (ap == null) ap = App.FindByName(model.AppId);

            // 新建应用配置
            var app = AppTracer.FindByName(appId);
            if (app == null) app = AppTracer.Find(AppTracer._.Name == appId);
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
                            app.Enable = set.AutoRegister;
                        }

                        app.Insert();
                    }
                }
            }

            if (ap != null)
            {
                // 双向同步应用分类
                if (!ap.Category.IsNullOrEmpty())
                    app.Category = ap.Category;
                else if (!app.Category.IsNullOrEmpty())
                {
                    ap.Category = app.Category;
                    ap.Update();
                }

                if (app.AppId == 0) app.AppId = ap.Id;
                app.Update();
            }

            var ip = HttpContext.GetUserHost();
            if (clientId.IsNullOrEmpty()) clientId = ip;

            App.WriteMeter(model, ip);

            // 更新心跳信息
            var online = _appOnline.UpdateOnline(ap, clientId, ip, token, model.Info);

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

            return (app, online);
        }

        private void ProcessData(AppTracer app, TraceModel model, String ip, ISpanBuilder[] builders)
        {
            // 排除项
            var excludes = app.Excludes.Split(",", ";") ?? new String[0];
            var timeoutExcludes = app.TimeoutExcludes.Split(",", ";") ?? new String[0];

            var now = DateTime.Now;
            var startTime = now.AddDays(-Setting.Current.DataRetention);
            var endTime = now.AddDays(1);
            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                // 剔除指定项
                if (item.Name.IsNullOrEmpty()) continue;
                if (excludes != null && excludes.Any(e => e.IsMatch(item.Name))) continue;
                //if (item.Name.EndsWithIgnoreCase("/Trace/Report")) continue;

                // 拒收超期数据，拒收未来数据
                var timestamp = item.StartTime.ToDateTime().ToLocalTime();
                if (timestamp < startTime || timestamp > endTime) continue;

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

                // 超时时间。超过该时间时标记为异常，默认0表示使用应用设置，-1表示不判断超时
                var timeout = ti.Timeout;
                if (timeout == 0) timeout = app.Timeout;

                var isTimeout = timeout > 0 && !timeoutExcludes.Any(e => e.IsMatch(item.Name));
                if (item.Samples != null && item.Samples.Count > 0)
                {
                    // 超时处理为异常，累加到错误数之中
                    if (isTimeout) td.Errors += item.Samples.Count(e => e.EndTime - e.StartTime > timeout);

                    samples.AddRange(SampleData.Create(td, item.Samples, true));
                }

                // 如果最小耗时都超过了超时设置，则全部标记为错误
                if (isTimeout && td.MinCost >= timeout && td.Errors < td.Total) td.Errors = td.Total;
            }

            //// 分表
            //var time = builders[0].StartTime.ToDateTime().ToLocalTime();
            //{
            //    using var split = TraceData.Meta.CreateShard(time);

            //    traces.Insert(true);
            //}
            //{
            //    using var split = SampleData.Meta.CreateShard(time);

            //    samples.Insert(true);
            //}

            // 更新XCode后，支持批量插入的自动分表，内部按照实体类所属分表进行分组插入
            traces.Insert(true);
            samples.Insert(true);

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