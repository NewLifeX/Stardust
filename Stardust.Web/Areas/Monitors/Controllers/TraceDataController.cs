using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceData;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class TraceDataController : EntityController<TraceData>
    {
        static TraceDataController() => MenuOrder = 60;

        protected override IEnumerable<TraceData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["name"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            //p.RetrieveState = true;

            var list = TraceData.Search(appId, name, start, end, p["Q"], p);

            if (list.Count > 0 && appId > 0)
            {
                //var hasDate = start.Year > 2000 || end.Year > 2000;
                // 绘制日期曲线图
                var app = AppTracer.FindByID(appId);
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = "调用次数" },
                        Height = 400,
                    };
                    chart.SetX(list, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToString("HH:mm:ss"));
                    chart.SetY("次数");
                    chart.AddLine(list, _.Total, null, true);
                    chart.Add(list, _.Errors);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = "耗时" },
                        Height = 400,
                    };
                    chart.SetX(list, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToString("HH:mm:ss"));
                    chart.SetY("耗时");
                    chart.AddLine(list, _.Cost, null, true);
                    chart.Add(list, _.MaxCost);
                    chart.Add(list, _.MinCost);
                    chart.SetTooltip();
                    ViewBag.Charts2 = new[] { chart };
                }
            }

            return list;
        }

        [EntityAuthorize(PermissionFlags.Detail)]
        public ActionResult Trace(Int32 id)
        {
            var list = SampleData.FindAllByDataId(id);
            if (list.Count == 0) throw new InvalidDataException("找不到采样数据");

            return RedirectToAction("Index", "SampleData", new { traceId = list[0].TraceId });
        }
    }
}