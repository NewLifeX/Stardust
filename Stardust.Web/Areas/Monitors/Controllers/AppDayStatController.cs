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
using Stardust.Server.Services;
using XCode.Membership;
using static Stardust.Data.Monitors.AppDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AppDayStatController : ReadOnlyEntityController<AppDayStat>
    {
        public static IAppDayStatService Service { get; set; }

        static AppDayStatController()
        {
            MenuOrder = 80;
        }

        protected override IEnumerable<AppDayStat> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            // 默认排序
            if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
            {
                p.Sort = __.StatDate;
                p.Desc = true;
                p.PageSize = 100;
            }

            p.RetrieveState = true;

            var list = AppDayStat.Search(appId, start, end, p["Q"], p);

            if (list.Count > 0 && appId > 0)
            {
                var list2 = list.OrderBy(e => e.StatDate).ToList();

                // 绘制日期曲线图
                var ar = AppTracer.FindByID(appId);
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Height = 400,
                    };
                    chart.SetX(list2, _.StatDate, e => e.StatDate.ToString("MM-dd"));
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
                    chart.SetX(list2, _.StatDate, e => e.StatDate.ToString("MM-dd"));
                    chart.SetY("耗时");
                    chart.AddLine(list2, _.Cost, null, true);
                    chart.Add(list2, _.MaxCost);
                    chart.Add(list2, _.MinCost);
                    chart.SetTooltip();
                    ViewBag.Charts2 = new[] { chart };
                }
            }

            return list;
        }

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult RetryStat(Int32 id)
        {
            var stat = AppDayStat.FindByID(id);
            if (stat == null) throw new InvalidDataException("找不到统计数据");

            Service.Add(stat.StatDate);

            return RedirectToAction("Index");
        }
    }
}