using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using static Stardust.Data.Monitors.AppMinuteStat;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AppMinuteStatController : ReadOnlyEntityController<AppMinuteStat>
    {
        static AppMinuteStatController()
        {
            MenuOrder = 78;
        }

        protected override IEnumerable<AppMinuteStat> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            // 选了应用，没有选时间，按照统计日期升序
            if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
            {
                p.Sort = __.StatTime;
                p.Desc = true;
                p.PageSize = 2 * 60 / 5;
            }
            // 选了应用和时间，按照接口调用次数降序
            else if (appId >= 0 && start.Year > 2000 && p.Sort.IsNullOrEmpty())
            {
                p.Sort = __.Total;
                p.Desc = true;
                p.PageSize = 24 * 60 / 5;
            }
            // 监控视图，没有选应用
            else if (appId < 0 && p["t"] == "dash")
            {
                // 最近一段时间，5~10分钟
                if (start.Year < 2000)
                {
                    var time = DateTime.Now;
                    var minute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5);
                    start = minute.AddMinutes(-5);
                }

                p.OrderBy = $"{__.Errors} desc, {__.Total} desc";
                p.PageSize = 50;

                PageSetting.EnableNavbar = false;
            }

            p.RetrieveState = true;

            var list = AppMinuteStat.Search(appId, start, end, p["Q"], p);

            if (list.Count > 0 && appId >= 0)
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
            else if (list.Count > 0 && appId < 0)
            {
                var list2 = list.OrderBy(e => e.Errors).ThenBy(e => e.Total).ToList();

                // 绘制柱状图
                var chart = new ECharts
                {
                    Height = 400,
                };
                chart.SetTooltip("axis", "shadow");
                chart.Legend = new { data = new[] { "总数", "错误数" } };
                chart["grid"] = new { left = "3%", right = "4%", bottom = "3%", containLabel = true };

                chart.XAxis = new[] { new { type = "value" } };
                chart.YAxis = new[] {
                    new {
                        type = "category",
                        axisTick = new { show = false },
                        data = list2.Select(e => e.AppName).ToArray()
                    }
                };

                //chart.Add(list2, _.Total, "bar");
                //chart.Add(list2, _.Errors, "bar");
                chart.Add(new Series
                {
                    Name = "错误数",
                    Type = "bar",
                    ["stack"] = "总量",
                    ["label"] = new { show = true, position = "left" },
                    Data = list2.Select(e => -e.Errors).ToArray(),
                });
                chart.Add(new Series
                {
                    Name = "总数",
                    Type = "bar",
                    ["stack"] = "总量",
                    ["label"] = new { show = true },
                    Data = list2.Select(e => e.Total).ToArray(),
                });

                ViewBag.Charts = new[] { chart };
            }

            var ar = AppTracer.FindByID(appId);
            if (ar != null) ViewBag.Title = $"{ar}分钟统计";

            return list;
        }
    }
}