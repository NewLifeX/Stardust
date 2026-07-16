using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class AlarmHistoryController : ReadOnlyEntityController<AlarmHistory>
{
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AlarmHistory> Search(Pager p)
    {
        var groupId = p["groupId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmHistory.Search(groupId, start, end, p["Q"], p);
    }
}
