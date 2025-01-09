using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class AlarmHistoryController : ReadOnlyEntityController<AlarmHistory>
{
    protected override IEnumerable<AlarmHistory> Search(Pager p)
    {
        var groupId = p["groupId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmHistory.Search(groupId, start, end, p["Q"], p);
    }
}