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
        private readonly ITraceStatService _stat;

        public TraceController(ITraceStatService stat) => _stat = stat;

        [ApiFilter]
        [HttpPost(nameof(Report))]
        public TraceResponse Report([FromBody] MyTraceModel model)
        {
            var builders = model?.Builders.Cast<ISpanBuilder>().ToArray();
            //var builders = new ISpanBuilder[0];
            if (model == null || model.AppId.IsNullOrEmpty() || builders == null || builders.Length == 0) return null;

            // 校验应用
            var app = AppTracer.FindByName(model.AppId);
            if (app == null)
            {
                app = new AppTracer
                {
                    Name = model.AppId,
                    DisplayName = model.AppName,
                };
                app.Save();
            }
            if (!app.Enable) throw new Exception($"无效应用[{model.AppId}/{model.AppName}]");

            // 插入数据
            var ip = HttpContext.GetUserHost();
            if (ip.IsNullOrEmpty()) ip = ManageProvider.UserHost;

            Task.Run(() => ProcessData(app, ip, builders));

            _stat.Add(app.ID);

            // 构造响应
            return new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
            };
        }

        private void ProcessData(AppTracer app, String ip, ISpanBuilder[] builders)
        {
            //var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ClientId = ip;
                td.CreateIP = ip;
                td.CreateTime = DateTime.Now;

                td.Insert();
                //traces.Add(td);

                samples.AddRange(SampleData.Create(td, item.Samples));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples));

                //if (samples.Count > 0) samples.Insert(true);
            }

            //traces.Insert(true);
            samples.Insert(true);
        }
    }
}