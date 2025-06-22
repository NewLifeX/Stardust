using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using XCode.Membership;
using static Stardust.Data.Monitors.AppDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(80)]
[MonitorsArea]
public class AppDayStatController : ReadOnlyEntityController<AppDayStat>
{
    private readonly IAppDayStatService _appStat;
    private readonly ITraceStatService _traceStat;

    public AppDayStatController(IAppDayStatService appStat, ITraceStatService traceStat)
    {
        _appStat = appStat;
        _traceStat = traceStat;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        var monitorId = GetRequest("monitorId").ToInt(-1);
        if (appId > 0 || monitorId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }

        PageSetting.EnableAdd = false;
    }

    protected override IEnumerable<AppDayStat> Search(Pager p)
    {
        var appId = p["monitorId"].ToInt(-1);
        if (appId <= 0) appId = p["appId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        // 默认排序
        if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
        {
            p.Sort = __.StatDate;
            p.Desc = true;
            p.PageSize = 30;
        }

        p.RetrieveState = true;
        PageSetting.EnableSelect = true;

        var list = AppDayStat.Search(appId, start, end, p["Q"], p);

        if (list.Count > 0 && appId > 0)
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
                chart.SetY(["调用次数", "错误数"], "value");
                chart.AddDataZoom();
                chart.Grid.Left = -5;
                chart.Grid.Right = -5;
                chart.AddLine(list2, _.Total, null, true);

                chart.Add(list2, _.Apis);
                chart.Add(list2, _.Https);
                chart.Add(list2, _.Dbs);
                chart.Add(list2, _.Mqs);
                chart.Add(list2, _.Redis);
                chart.Add(list2, _.Others);

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
                chart.SetX(list2, _.StatDate);
                //chart.SetY("耗时");
                chart.YAxis = [
                    new YAxis{ Name = "耗时（ms）", Type = "value" },
                    new YAxis{ Name = "最大耗时（ms）", Type = "value" }
                ];
                chart.AddDataZoom();
                chart.AddLine(list2, _.Cost, null, true);
                chart.Add(list2, _.MinCost);

                var line = chart.Add(list2, _.MaxCost);
                line.YAxisIndex = 1;

                chart.SetTooltip();
                ViewBag.Charts2 = new[] { chart };
            }
        }

        var ar = AppTracer.FindByID(appId);
        if (ar != null) ViewBag.Title = $"{ar}每日统计";

        return list;
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult RetryStat()
    {
        foreach (var item in SelectKeys)
        {
            var stat = FindByID(item.ToInt());
            if (stat != null)
            {
                XTrace.WriteLine("重新统计 {0}/{1} {2}", stat.AppName, stat.AppId, stat.StatDate);

                _appStat.Add(stat.StatDate);
                //TraceStat.Add(stat.AppId, stat.StatDate);
                //TraceStat.Add(stat.AppId, stat.StatDate.AddDays(1));
                //TraceStat.Add(stat.AppId, stat.StatDate.AddDays(1).AddSeconds(-1));
                for (var time = stat.StatDate; time < stat.StatDate.AddDays(1); time = time.AddMinutes(5))
                {
                    _traceStat.Add(stat.AppId, time);
                }
            }
        }

        return JsonRefresh("成功！");
    }
}