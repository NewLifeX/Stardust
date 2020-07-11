using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using Stardust.Data.Monitors;
using Stardust.Monitors;
using Stardust.Server.Common;
using XCode;
using XCode.Membership;

namespace Stardust.Server.Controllers
{
    //[ApiController]
    [Route("[controller]")]
    public class TraceController : ControllerBase
    {
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

            var traces = new List<TraceData>();
            var samples = new List<SampleData>();
            foreach (var item in builders)
            {
                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ClientId = ip;
                td.CreateIP = ip;
                td.CreateTime = DateTime.Now;

                //td.Insert();
                traces.Add(td);

                samples.AddRange(SampleData.Create(td, item.Samples));
                samples.AddRange(SampleData.Create(td, item.ErrorSamples));

                //if (samples.Count > 0) samples.Insert(true);
            }

            //tran.Commit();
            traces.Insert(true);
            samples.Insert(true);

            // 构造响应
            return new TraceResponse
            {
                Period = app.Period,
                MaxSamples = app.MaxSamples,
                MaxErrors = app.MaxErrors,
            };
        }
    }
}