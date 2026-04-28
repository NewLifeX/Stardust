using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceData;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class TraceDataController : ReadOnlyEntityController<TraceData>
{
    static TraceDataController() => ListFields.RemoveField("ID");

    protected override IEnumerable<TraceData> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var itemId = p["itemId"].ToInt(-1);
        var clientId = p["clientId"];
        var name = p["name"];
        var minError = p["minError"].ToInt(-1);
        var searchTag = p["searchTag"]?.Split(",").FirstOrDefault()?.ToBoolean() ?? false;

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        var kind = p["kind"];
        var date = p["date"].ToDateTime();
        if (start.Year < 2000 && end.Year < 2000) start = end = date;
        var time = p["time"].ToDateTime();
        if (start.Year < 2000 && end.Year < 2000) start = end = time;

        ////todo 待更新NewLife.Core修复雪花算法后，后面两行可以注释
        //if (start.Year > 2000) start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
        //if (end.Year > 2000) end = new DateTime(end.Year, end.Month, end.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);

        if (start.Year < 2000 && end.Year < 2000)
        {
            var dt = DateTime.Today;
            start = dt;
            end = dt;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
            p["dtEnd"] = end.ToString("yyyy-MM-dd");
        }

        if (appId > 0 && p.PageSize == 20) p.PageSize = 100;
        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = TraceData.Search(appId, itemId, clientId, name, kind, minError, searchTag, start, end, p["Q"], p);

        if (list.Count > 1 && appId > 0 && itemId > 0)
        {
            var list2 = list.OrderBy(e => e.StartTime).ToList();

            // 绘制日期曲线图
            var app = AppTracer.FindByID(appId);
            if (appId >= 0)
            {
                var chart = new ECharts
                {
                    //Title = new ChartTitle { Text = "调用次数" },
                    Height = 400,
                };
                chart.SetX(list2, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToFullString());
                //chart.SetY("次数");
                chart.YAxis = [
                    new YAxis{ Name = "调用次数", Type = "value" },
                    new YAxis{ Name = "错误数", Type = "value" }
                ];
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
                    //Title = new ChartTitle { Text = "耗时" },
                    Height = 400,
                };
                chart.SetX(list2, _.StartTime, e => e.StartTime.ToDateTime().ToLocalTime().ToFullString());
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

        var ar = AppTracer.FindByID(appId);
        if (ar != null) ViewBag.Title = $"{ar}追踪";

        return list;
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult Trace(Int64 id)
    {
        var td = TraceData.FindById(id);
        if (td != null && td.LinkId > 0) id = td.LinkId;

        //var list = SampleData.FindAllByDataId(id);
        var start = DateTime.Today.AddDays(-30);
        var end = DateTime.Today;
        var list = SampleData.Search(id, "", null, start, end, new PageParameter { PageSize = 1000 });
        if (list.Count == 0) throw new InvalidDataException("找不到采样数据");

        //return RedirectToAction("Index", "SampleData", new { traceId = list[0].TraceId });
        return Redirect($"/trace?id={list[0].TraceId}");
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult Exclude(Int64 id)
    {
        var td = FindById(id);
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