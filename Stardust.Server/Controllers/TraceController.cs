using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
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
        private readonly AppService _service = new AppService();
        private readonly ITraceStatService _stat;
        private readonly IAppDayStatService _appStat;

        public TraceController(ITraceStatService stat, IAppDayStatService appStat)
        {
            _stat = stat;
            _appStat = appStat;
        }

        [ApiFilter]
        [HttpPost(nameof(Report))]
        public TraceResponse Report([FromBody] MyTraceModel model, String token)
        {
            var builders = model?.Builders.Cast<ISpanBuilder>().ToArray();
            //var builders = new ISpanBuilder[0];
            if (model == null || model.AppId.IsNullOrEmpty() || builders == null || builders.Length == 0) return null;

            var set = Setting.Current;

            // 新版验证方式，访问令牌
            Data.App ap = null;
            //var token = HttpContext.Items["Token"] as String;
            if (!token.IsNullOrEmpty())
            {
                ap = _service.DecodeToken(token, set);
                if (ap == null || ap.Name != model.AppId) throw new InvalidOperationException($"授权不匹配[{model.AppId}]!=[{ap.Name}]！");

                // 更新应用名
                if (ap.DisplayName.IsNullOrEmpty())
                {
                    ap.DisplayName = model.AppName;
                    ap.Update();
                }
            }

            // 该应用的跟踪配置信息
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

            // 修复数据
            if (ap == null)
            {
                ap = Data.App.FindByName(app.Name);
                if (ap == null) ap = new Data.App { Name = app.Name, DisplayName = app.DisplayName, Enable = true };
                if (ap.DisplayName.IsNullOrEmpty()) ap.DisplayName = app.DisplayName;
                ap.Save();
            }

            // 插入数据
            var ip = HttpContext.GetUserHost();
            if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;

            Task.Run(() => ProcessData(app, ip, builders));

            _stat.Add(app.ID);
            _appStat.Add(app.ID);

            // 构造响应
            return new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
                Timeout = app.Timeout,
                Excludes = app.Excludes?.Split(",", ";"),
            };
        }

        private void ProcessData(AppTracer app, String ip, ISpanBuilder[] builders)
        {
            // 排除项
            var excludes = app.Excludes.Split(",", ";") ?? new String[0];
            builders = builders.Where(e => !e.Name.EndsWithIgnoreCase("/Trace/Report")).ToArray();

            var flow = TraceData.Meta.Factory.FlowId;
            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                // 剔除指定项
                if (excludes != null && excludes.Contains(item.Name)) continue;
                if (item.Name.EndsWithIgnoreCase("/Trace/Report")) continue;

                var td = TraceData.Create(item);
                td.Id = flow.NewId();
                td.AppId = app.ID;
                td.ClientId = ip;
                td.CreateIP = ip;
                td.CreateTime = DateTime.Now;

                //td.Insert();
                traces.Add(td);

                samples.AddRange(SampleData.Create(td, item.Samples, true, app.Timeout));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples, false, app.Timeout));

                //if (samples.Count > 0) samples.Insert(true);
            }

            traces.Insert(true);
            samples.Insert(true);
        }
    }
}