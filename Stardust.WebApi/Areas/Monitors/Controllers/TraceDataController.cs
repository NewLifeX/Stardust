using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
using Stardust.Web.Services;
using XCode;
using XCode.Membership;
using static Stardust.Data.Monitors.TraceData;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class TraceDataController : WebReadOnlyEntityController<TraceData>
{
    private readonly HotAppService _hotApp;

    public TraceDataController(HotAppService hotApp) => _hotApp = hotApp;

    static TraceDataController() => ListFields.RemoveField("ID");

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

        // 标记热门应用，缩短统计计算周期
        if (appId > 0) _hotApp.SetHotAppAsync(appId);

        var list = TraceData.Search(appId, itemId, clientId, name, kind, minError, searchTag, start, end, p["Q"], p);

        if (list.Count > 1 && appId > 0 && itemId > 0)
        {
            var list2 = list.OrderBy(e => e.StartTime).ToList();

            // 纯 API 无视图，原 MVC 图表绘制（ECharts/ViewBag.Charts）已删除
        }

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

        // 纯 API 无视图跳转，返回统一响应
        return JsonRefresh(new { traceId = list[0].TraceId });
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

        return JsonRefresh("成功！");
    }
}
