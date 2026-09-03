using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
using static Stardust.Data.Monitors.TraceData;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>链路查询。基于跟踪数据(TraceData)自动渲染列表，提供分页查询。</summary>
[DisplayName("链路查询")]
[Menu(10, true)]
[MonitorsArea]
public class TraceController : WebEntityController<TraceData>
{
    static TraceController()
    {
        LogOnChange = true;
        ListFields.RemoveField("ID");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<TraceData> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var itemId = p["itemId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        if (start.Year < 2000 && end.Year < 2000)
        {
            var dt = DateTime.Today;
            start = dt;
            end = dt;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
            p["dtEnd"] = end.ToString("yyyy-MM-dd");
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Name + " Desc";

        return TraceData.Search(appId, itemId, null, null, null, -1, false, start, end, p["Q"], p);
    }
}
