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
using XCode.Membership;
using static Stardust.Data.Monitors.TraceMinuteStat;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class TraceMinuteStatController : EntityController<TraceMinuteStat>
    {
        static TraceMinuteStatController()
        {
            MenuOrder = 70;
        }

        protected override IEnumerable<TraceMinuteStat> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["name"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();
            var date = p["date"].ToDateTime();
            if (start.Year < 2000 && end.Year < 2000) start = end = date;

            // 选了应用，没有选时间，按照统计日期升序
            if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
            {
                p.Sort = __.StatTime;
                p.Desc = true;
                p.PageSize = 24 * 60 / 5;
            }
            // 选了应用和时间，按照接口调用次数降序
            else if (appId >= 0 && start.Year > 2000 && p.Sort.IsNullOrEmpty())
            {
                p.Sort = __.Total;
                p.Desc = true;
                p.PageSize = 100;
            }

            p.RetrieveState = true;

            var list = TraceMinuteStat.Search(appId, name, start, end, p["Q"], p);

            if (list.Count > 0 && appId >= 0 && !name.IsNullOrEmpty())
            {
                var list2 = list.OrderBy(e => e.StatTime).ToList();

                // 绘制日期曲线图
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Height = 400,
                    };
                    chart.SetX(list2, _.StatTime, e => e.StatTime.ToString("HH:mm"));
                    chart.SetY("调用次数");
                    chart.AddLine(list2, _.Total, null, true);
                    chart.Add(list2, _.Errors);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Height = 400,
                    };
                    chart.SetX(list2, _.StatTime, e => e.StatTime.ToString("HH:mm"));
                    chart.SetY("耗时");
                    chart.AddLine(list2, _.Cost, null, true);
                    chart.Add(list2, _.MaxCost);
                    chart.Add(list2, _.MinCost);
                    chart.SetTooltip();
                    ViewBag.Charts2 = new[] { chart };
                }
            }

            var ar = AppTracer.FindByID(appId);
            if (ar != null) ViewBag.Title = $"{ar}分钟统计";

            return list;
        }

        [EntityAuthorize(PermissionFlags.Detail)]
        public ActionResult Trace(Int32 id)
        {
            var st = TraceMinuteStat.FindByID(id);
            if (st == null) throw new InvalidDataException("找不到统计数据");

            var ds = TraceData.Search(st.AppId, st.Name, "minute", st.StatTime, 20);
            if (ds.Count == 0) throw new InvalidDataException("找不到跟踪数据");

            var list = SampleData.FindAllByDataIds(ds.Select(e => e.Id).ToArray());
            if (list.Count == 0) throw new InvalidDataException("找不到采样数据");

            return RedirectToAction("Index", "SampleData", new { traceId = list[0].TraceId });
        }
    }
}