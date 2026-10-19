using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using Stardust.Web.Areas;
using Stardust.Web.Services;
using XCode.Membership;
using static Stardust.Data.Monitors.AppDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(80)]
[MonitorsArea]
public class AppDayStatController : WebEntityController<AppDayStat>
{
    private readonly IAppDayStatService _appStat;
    private readonly ITraceStatService _traceStat;
    private readonly HotAppService _hotApp;

    public AppDayStatController(IAppDayStatService appStat, ITraceStatService traceStat, HotAppService hotApp)
    {
        _appStat = appStat;
        _traceStat = traceStat;
        _hotApp = hotApp;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        PageSetting.EnableAdd = false;
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

        // 标记热门应用，缩短统计计算周期
        if (appId > 0) _hotApp.SetHotAppAsync(appId);

        var list = AppDayStat.Search(appId, start, end, p["Q"], p);

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
                XTrace.WriteLine("Recomputing statistics {0}/{1} {2}", stat.AppName, stat.AppId, stat.StatDate);

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
