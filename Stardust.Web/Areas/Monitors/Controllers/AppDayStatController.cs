using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using static Stardust.Data.Monitors.AppDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AppDayStatController : EntityController<AppDayStat>
    {
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
                start = DateTime.Today.AddDays(-30);
                p["dtStart"] = start.ToString("yyyy-MM-dd");

                p.Sort = AppDayStat.__.StatDate;
                p.Desc = false;
                p.PageSize = 100;
            }

            p.RetrieveState = true;

            var list = AppDayStat.Search(appId, start, end, p["Q"], p);

            if (list.Count > 0)
            {
                var hasDate = start.Year > 2000 || end.Year > 2000;
                // 绘制日期曲线图
                var ar = AppTracer.FindByID(appId);
                if (appId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = ar + "" },
                        Height = 400,
                    };
                    chart.SetX(list, _.StatDate, e => e.StatDate.ToString("MM-dd"));
                    chart.SetY("数量");
                    chart.AddLine(list, _.Total, null, true);
                    chart.Add(list, _.Total);
                    chart.Add(list, _.Errors);
                    chart.Add(list, _.Cost);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
            }

            return list;
        }
    }
}