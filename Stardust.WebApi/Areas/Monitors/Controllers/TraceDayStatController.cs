using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
using Stardust.Web.Services;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceDayStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class TraceDayStatController : WebReadOnlyEntityController<TraceDayStat>
{
    private readonly HotAppService _hotApp;

    public TraceDayStatController(HotAppService hotApp) => _hotApp = hotApp;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

        // 标记热门应用，缩短统计计算周期
        if (appId > 0) _hotApp.SetHotAppAsync(appId);

        var list = TraceDayStat.Search(appId, itemId, name, type, start, end, p["Q"], p);

        if (list.Count > 0 && appId >= 0 && itemId > 0)
        {
            var list2 = list.OrderBy(e => e.StatDate).ToList();

            // 纯 API 无视图，原 MVC 图表绘制（ECharts/ViewBag.Charts/ViewBag.Charts2）已删除
        }

        // 纯 API 无视图，原 ViewBag.Title 赋值已删除

        return list;
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult Trace(Int32 id)
    {
        var st = FindByID(id);
        if (st == null) return JsonRefresh("找不到统计数据");

        var traceId = st.TraceId;

        // 如果有新的TraceId，则直接使用，否则使用原来的
        try
        {
            var ds = TraceData.Search(st.AppId, st.ItemId, "day", st.StatDate, 20);
            if (ds.Count == 0) return JsonRefresh("找不到追踪数据");

            var list = SampleData.FindAllByDataIds(ds.Select(e => e.LinkId > 0 ? e.LinkId : e.Id).ToArray(), st.StatDate);
            if (list.Count == 0) return JsonRefresh("找不到采样数据");

            traceId = list[0].TraceId;
            st.TraceId = traceId;

            st.Update();
        }
        catch
        {
            if (traceId.IsNullOrEmpty()) throw;
        }

        // 纯 API 无视图跳转，返回统一响应
        return JsonRefresh(new { traceId });
    }
}
