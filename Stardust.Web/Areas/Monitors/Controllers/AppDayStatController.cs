using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Log;
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
        private readonly IAppDayStatService _appStat;
        private readonly ITraceStatService _traceStat;

        static AppDayStatController() => MenuOrder = 80;

        public AppDayStatController(IAppDayStatService appStat, ITraceStatService traceStat)
        {
            _appStat = appStat;
            _traceStat = traceStat;
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
                    chart.SetX(list2, _.StatDate, e => e.StatDate.ToString("MM-dd"));
                    chart.SetY("调用次数");
                    chart.AddLine(list2, _.Total, null, true);
                    chart.Add(list2, _.Errors);
                    chart.Add(list2, _.Apis);
                    chart.Add(list2, _.Https);
                    chart.Add(list2, _.Dbs);
                    chart.Add(list2, _.Mqs);
                    chart.Add(list2, _.Redis);
                    chart.Add(list2, _.Others);
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
}