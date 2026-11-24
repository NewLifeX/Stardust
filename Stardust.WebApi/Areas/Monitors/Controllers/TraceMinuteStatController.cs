using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Data.Platform;
using Stardust.Web.Areas;
using Stardust.Web.Services;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceMinuteStat;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>追踪分钟统计。按分钟汇总的接口调用指标</summary>
[Menu(0, false)]
[MonitorsArea]
public class TraceMinuteStatController : WebReadOnlyEntityController<TraceMinuteStat>
{
    private readonly HotAppService _hotApp;

    public TraceMinuteStatController(HotAppService hotApp) => _hotApp = hotApp;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<TraceMinuteStat> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var itemId = p["itemId"].ToInt(-1);
        var name = p["name"];
        var minError = p["minError"].ToInt(-1);
        var type = p["type"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var date = p["date"].ToDateTime();
        MonitorSearchHelper.ResolveDateRange(appId, itemId, date, ref start, ref end);

        // 选了应用，没有选时间，按照统计日期升序
        if (appId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
        {
            p.Sort = __.StatTime;
            p.Desc = true;
            p.PageSize = 24 * 60 / 5;
        }
        // 选了应用和时间，按照接口调用次数降序
        else if (appId >= 0 && itemId < 0 && start.Year > 2000 && p.Sort.IsNullOrEmpty())
        {
            p.Sort = __.Total;
            p.Desc = true;
            p.PageSize = 24 * 60 / 5;
        }

        p.RetrieveState = MonitorSearchHelper.NeedState(appId);

        // 标记热门应用，缩短统计计算周期
        if (appId > 0) _hotApp.SetHotAppAsync(appId);

        IList<TraceMinuteStat> list;
        if (!type.IsNullOrEmpty())
        {
            // CARD-MON-0506（task-71）：type 种类过滤（A1 方案 Minute 对称补，j-config 定稿闸裁补——三粒度行为一致）。
            // Minute 实体无 Type 列，由 TraceItem.GetKind(Name) 派生种类判定筛出 ItemId 集，
            // 交实体集合重载 In 过滤。EqualIgnoreCase 对齐 Day 先例。
            // 空集短路：无匹配埋点返回空列表，非退化全量。
            var items = ProjectScopeRules.FilterVisible(appId >= 0 ? TraceItem.FindAll(TraceItem._.AppId == appId) : TraceItem.FindAll());
            var itemIds = items.Where(e => TraceItem.GetKind(e.Name).EqualIgnoreCase(type)).Select(e => e.Id).ToArray();
            if (itemIds.Length == 0) return Array.Empty<TraceMinuteStat>();

            list = TraceMinuteStat.Search(appId, itemIds, name, minError, start, end, p["Q"], p);
        }
        else
        {
            list = TraceMinuteStat.Search(appId, itemId, name, minError, start, end, p["Q"], p);
        }

        if (list.Count > 0 && appId >= 0 && itemId > 0)
        {
            var list2 = list.OrderBy(e => e.StatTime).ToList();

            // 纯 API 无视图，原 MVC 图表绘制（ECharts/ViewBag.Charts/ViewBag.Charts2）已删除
        }

        // 纯 API 无视图，原 ViewBag.Title 赋值已删除

        return list;
    }

    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult Trace(Int32 id)
    {
        var st = ProjectScopeRules.FilterVisible(FindByID(id));
        if (st == null) return JsonRefresh("找不到统计数据");

        var traceId = st.TraceId;

        // 如果有新的TraceId，则直接使用，否则使用原来的
        try
        {
            var ds = TraceData.Search(st.AppId, st.ItemId, "minute", st.StatTime, 20);
            if (ds.Count == 0) return JsonRefresh("找不到追踪数据");

            var list = ProjectScopeRules.FilterVisible(SampleData.FindAllByDataIds(ds.Select(e => e.LinkId > 0 ? e.LinkId : e.Id).ToArray(), st.StatTime));
            if (list.Count == 0) return JsonRefresh(L.T("Sample data not found."));

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
