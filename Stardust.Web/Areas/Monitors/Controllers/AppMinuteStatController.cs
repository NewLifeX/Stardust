using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using static Stardust.Data.Monitors.AppMinuteStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class AppMinuteStatController : ReadOnlyEntityController<AppMinuteStat>
{
    protected override IEnumerable<AppMinuteStat> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var minError = p["minError"].ToInt(-1);

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
            p.PageSize = 20;

            PageSetting.EnableNavbar = false;
            PageSetting.EnableAdd = false;
            PageSetting.EnableKey = false;
            PageSetting.EnableSelect = false;
        }

        p.RetrieveState = true;

        var list = AppMinuteStat.Search(appId, minError, start, end, p["Q"], p);

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
                chart.SetX(list2, _.StatTime);
                chart.SetY(["调用次数", "错误数"], "value");
                chart.AddLine(list2, _.Total, null, true);

                var line = chart.Add(list2, _.Errors);
                line.YAxisIndex = 1;
                line["itemStyle"] = new { color = "rgba(255, 0, 0, 0.5)", };

                chart.SetTooltip();
                ViewBag.Charts = new[] { chart };
            }
            if (appId >= 0)
            {
                var chart = new ECharts
                {
                    Height = 400,
                };
                chart.SetX(list2, _.StatTime);
                //chart.SetY("耗时");
                chart.YAxis = [
                    new YAxis{ Name = "耗时（ms）", Type = "value" },
                    new YAxis{ Name = "最大耗时（ms）", Type = "value" }
                ];
                chart.AddLine(list2, _.Cost, null, true);
                chart.Add(list2, _.MinCost);

                var line = chart.Add(list2, _.MaxCost);
                line.YAxisIndex = 1;

                chart.SetTooltip();
                ViewBag.Charts2 = new[] { chart };
            }
        }
        else if (list.Count > 0 && appId < 0 && p["t"] == "dash")
        {
            var list2 = new List<AppMinuteStat>();
            foreach (var item in list)
            {
                var st = list2.FirstOrDefault(e => e.AppId == item.AppId);
                if (st == null) list2.Add(st = new AppMinuteStat { AppId = item.AppId });

                st.Total += item.Total;
                st.Errors += item.Errors;
            }
            list2 = list2.OrderBy(e => e.Errors).ThenBy(e => e.Total).ToList();

            // 绘制柱状图
            var chart = new ECharts
            {
                Height = 800,
            };
            chart.SetTooltip("axis", "shadow");
            chart.Legend = new Legend { Data = ["总数", "错误数"] };
            chart["grid"] = new { left = "3%", right = "4%", bottom = "3%", containLabel = true };

            chart.XAxis = [new XAxis { Type = "value" }];
            chart.YAxis = [
                new YAxis{
                    Type = "category",
                    AxisTick = new { show = false },
                    Data = list2.Select(e => e.AppName).ToArray()
                }
            ];

            //chart.Add(list2, _.Total, "bar");
            //chart.Add(list2, _.Errors, "bar");
            chart.Add(new Series
            {
                Name = "错误数",
                Type = "bar",
                ["stack"] = "总量",
                ["label"] = new { show = true, position = "left" },
                ["itemStyle"] = new { color = "rgba(255, 0, 0, 0.5)", },
                Data = list2.Select(e => (Object)(-e.Errors)).ToArray(),
            });
            chart.Add(new Series
            {
                Name = "总数",
                Type = "bar",
                ["stack"] = "总量",
                ["label"] = new { show = true },
                Data = list2.Select(e => (Object)e.Total).ToArray(),
            });

            ViewBag.Charts = new[] { chart };
        }

        var ar = AppTracer.FindByID(appId);
        if (ar != null) ViewBag.Title = $"{ar}分钟统计";

        return list;
    }
}