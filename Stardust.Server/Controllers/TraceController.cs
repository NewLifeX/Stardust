using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
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
        public TraceResponse Report(TraceModel model)
        {
            var builders = model?.Builders;
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
            var ip = ManageProvider.UserHost;

            using var tran = TraceData.Meta.CreateTrans();
            foreach (var item in builders)
            {
                var td = TraceData.Create(item);
                td.AppId = app.ID;
                td.ClientId = ip;
                td.CreateIP = ip;
                td.CreateTime = DateTime.Now;

                td.Insert();

                var list = new List<SampleData>();
                list.AddRange(SampleData.Create(item.Samples, td.ID));
                list.AddRange(SampleData.Create(item.ErrorSamples, td.ID));

                if (list.Count > 0) list.Insert(true);
            }

            tran.Commit();

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