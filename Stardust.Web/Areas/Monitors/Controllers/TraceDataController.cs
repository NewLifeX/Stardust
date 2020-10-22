using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceData;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class TraceDataController : ReadOnlyEntityController<TraceData>
    {
        static TraceDataController()
        {
            MenuOrder = 60;

            ListFields.RemoveField("ID");
        }

        protected override IEnumerable<TraceData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["name"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            var kind = p["kind"];
            var date = p["date"].ToDateTime();
            if (start.Year < 2000 && end.Year < 2000) start = end = date;
            var time = p["time"].ToDateTime();
            if (start.Year < 2000 && end.Year < 2000) start = end = time;

            if (appId > 0 && p.PageSize == 20) p.PageSize = 100;
            if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

            var list = TraceData.Search(appId, name, kind, start, end, p["Q"], p);

            if (list.Count > 0 && appId > 0 && !name.IsNullOrEmpty())
            {
                var list2 = list.OrderBy(e => e.Id).ToList();

                // 绘制日期曲线图
                var app = AppTracer.FindByID(appId);
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = "调用次数" },
                        Height = 400,
                    };
                    chart.SetX(list2, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToString("HH:mm:ss"));
                    chart.SetY("次数");
                    chart.AddLine(list2, _.Total, null, true);
                    chart.Add(list2, _.Errors);
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
                    chart.SetX(list2, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToString("HH:mm:ss"));
                    chart.SetY("耗时");
                    chart.AddLine(list2, _.Cost, null, true);
                    chart.Add(list2, _.MaxCost);
                    chart.Add(list2, _.MinCost);
                    chart.SetTooltip();
                    ViewBag.Charts2 = new[] { chart };
                }
            }

            var ar = AppTracer.FindByID(appId);
            if (ar != null) ViewBag.Title = $"{ar}跟踪";

            return list;
        }

        [EntityAuthorize(PermissionFlags.Detail)]
        public ActionResult Trace(Int64 id)
        {
            var list = SampleData.FindAllByDataId(id);
            if (list.Count == 0) throw new InvalidDataException("找不到采样数据");

            return RedirectToAction("Index", "SampleData", new { traceId = list[0].TraceId });
        }

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult Exclude(Int64 id)
        {
            var td = TraceData.FindById(id);
            var app = td?.App;
            if (app != null && !td.Name.IsNullOrEmpty())
            {
                app.AddExclude(td.Name);

                app.Update();
            }

            var url = Request.Headers["Referer"].FirstOrDefault();
            if (!url.IsNullOrEmpty()) return Redirect(url);

            return RedirectToAction("Index");
        }
    }
}