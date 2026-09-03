using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
using Stardust.Web.Services;
using static Stardust.Data.Monitors.AppMinuteStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class AppMinuteStatController : WebReadOnlyEntityController<AppMinuteStat>
{
    private readonly HotAppService _hotApp;

    public AppMinuteStatController(HotAppService hotApp) => _hotApp = hotApp;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

        // 标记热门应用，缩短统计计算周期
        if (appId > 0) _hotApp.SetHotAppAsync(appId);

        var list = AppMinuteStat.Search(appId, minError, start, end, p["Q"], p);

        return list;
    }
}
