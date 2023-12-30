using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class TraceDayStatController : ReadOnlyEntityController<TraceDayStat>
{
    protected override IEnumerable<TraceDayStat> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var itemId = p["itemId"].ToInt(-1);
        var name = p["name"];
        var type = p["type"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var date = p["date"].ToDateTime();
        if (start.Year < 2000 && end.Year < 2000) start = end = date;

        // 选了应用，没有选时间，按照统计日期升序
        if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
        {
            p.Sort = __.StatDate;
            p.Desc = true;
            p.PageSize = 90;
        }
        // 选了应用和时间，按照接口调用次数降序
        else if (appId >= 0 && itemId < 0 && start.Year > 2000 && p.Sort.IsNullOrEmpty())
        {
            p.Sort = __.Total;
            p.Desc = true;
            p.PageSize = 100;
        }

        p.RetrieveState = true;

        var list = TraceDayStat.Search(appId, itemId, name, type, start, end, p["Q"], p);

        if (list.Count > 0 && appId >= 0 && itemId > 0)
        {
            var list2 = list.OrderBy(e => e.StatDate).ToList();

            // 绘制日期曲线图
            if (appId >= 0)
            {
                var chart = new ECharts
                {
                    Height = 400,
                };
                chart.SetX(list2, _.StatDate);
                //chart.SetY("调用次数");
                chart.YAxis = new[] {
                    new { name = "调用次数", type = "value" },
                    new { name = "错误数", type = "value" }
                };
                chart.AddDataZoom();
                chart.AddLine(list2, _.Total, null, true);

                var line = chart.Add(list2, _.Errors);
                line["yAxisIndex"] = 1;
                //line["lineStyle"] = new { color = "rgba(255, 0, 0, 1)", };
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
                chart.SetX(list2, _.StatDate);
                //chart.SetY("耗时");
                chart.YAxis = new[] {
                    new { name = "耗时（ms）", type = "value" },
                    new { name = "最大耗时（ms）", type = "value" }
                };
                chart.AddDataZoom();
                chart.AddLine(list2, _.Cost, null, true);
                chart.Add(list2, _.MinCost);

                var line = chart.Add(list2, _.MaxCost);
                line["yAxisIndex"] = 1;

                chart.SetTooltip();
                ViewBag.Charts2 = new[] { chart };
            }
        }

        var ti = TraceItem.FindById(itemId);
        if (ti != null)
            ViewBag.Title = $"{ti}每日统计";
        else
        {
            var ar = AppTracer.FindByID(appId);
            if (ar != null) ViewBag.Title = $"{ar}每日统计";
        }

        return list;
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult Trace(Int32 id)
    {
        var st = FindByID(id);
        if (st == null) return Content("找不到统计数据");

        var traceId = st.TraceId;

        // 如果有新的TraceId，则直接使用，否则使用原来的
        try
        {
            var ds = TraceData.Search(st.AppId, st.ItemId, "day", st.StatDate, 20);
            if (ds.Count == 0) return Content("找不到追踪数据");

            var list = SampleData.FindAllByDataIds(ds.Select(e => e.LinkId > 0 ? e.LinkId : e.Id).ToArray(), st.StatDate);
            if (list.Count == 0) return Content("找不到采样数据");

            traceId = list[0].TraceId;
            st.TraceId = traceId;

            st.Update();
        }
        catch
        {
            if (traceId.IsNullOrEmpty()) throw;
        }

        //return RedirectToAction("Index", "SampleData", new { traceId });
        return Redirect($"/trace?id={traceId}");
    }
}