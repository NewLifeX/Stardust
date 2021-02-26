using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
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
        private readonly AppService _service ;
        private readonly ITraceStatService _stat;
        private readonly IAppDayStatService _appStat;
        private static readonly ICache _cache = new NewLife.Caching.MemoryCache();

        public TraceController(ITraceStatService stat, IAppDayStatService appStat, AppService appService)
        {
            _stat = stat;
            _appStat = appStat;
            _service = appService;
        }

        [ApiFilter]
        [HttpPost(nameof(Report))]
        public TraceResponse Report([FromBody] TraceModel model, String token)
        {
            var builders = model?.Builders.Cast<ISpanBuilder>().ToArray();
            //var builders = new ISpanBuilder[0];
            if (model == null || model.AppId.IsNullOrEmpty() || builders == null || builders.Length == 0) return null;

            var ip = HttpContext.GetUserHost();
            if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;

            var set = Setting.Current;

            // 新版验证方式，访问令牌
            Data.App ap = null;
            if (!token.IsNullOrEmpty() && token.Split(".").Length == 3)
            {
                ap = _service.DecodeToken(token, set);
                if (ap == null || ap.Name != model.AppId) throw new InvalidOperationException($"授权不匹配[{model.AppId}]!=[{ap.Name}]！");
            }
            Data.App.UpdateInfo(model, ip);

            // 该应用的追踪配置信息
            var app = AppTracer.FindByName(model.AppId);
            if (app == null)
            {
                app = new AppTracer
                {
                    Name = model.AppId,
                    DisplayName = model.AppName,
                    Enable = set.AutoRegister,
                };
                app.Save();
            }

            // 校验应用
            if (app == null || !app.Enable) throw new Exception($"无效应用[{model.AppId}/{model.AppName}]");

            // 插入数据
            Task.Run(() => ProcessData(app, model, ip, builders));

            // 构造响应
            var rs = new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
                Timeout = app.Timeout,
                //Excludes = app.Excludes?.Split(",", ";"),
            };

            // 新版本才返回Excludes，老版本客户端在处理Excludes时有BUG，错误处理/
            if (!model.Version.IsNullOrEmpty()) rs.Excludes = app.Excludes?.Split(",", ";");

            return rs;
        }

        private void ProcessData(AppTracer app, TraceModel model, String ip, ISpanBuilder[] builders)
        {
            // 排除项
            var excludes = app.Excludes.Split(",", ";") ?? new String[0];

            var now = DateTime.Now;
            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                // 剔除指定项
                if (item.Name.IsNullOrEmpty()) continue;
                if (excludes != null && excludes.Any(e => e.IsMatch(item.Name))) continue;
                if (item.Name.EndsWithIgnoreCase("/Trace/Report")) continue;

                // 拒收超长项
                if (item.Name.Length > TraceData._.Name.Length) continue;

                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ClientId = model.ClientId ?? ip;
                td.CreateIP = ip;
                td.CreateTime = now;

                traces.Add(td);

                samples.AddRange(SampleData.Create(td, item.Samples, true));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples, false));
            }

            traces.Insert(true);
            samples.Insert(true);

            // 更新统计
            _stat.Add(traces);
            _appStat.Add(now.Date);
            if (now.Hour == 0 && now.Minute <= 10) _appStat.Add(now.Date.AddDays(-1));

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