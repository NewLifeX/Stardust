using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AlarmHistoryController : ReadOnlyEntityController<AlarmHistory>
    {
        static AlarmHistoryController() => MenuOrder = 90;

        protected override IEnumerable<AlarmHistory> Search(Pager p)
        {
            var groupId = p["groupId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AlarmHistory.Search(groupId, start, end, p["Q"], p);
        }
    }
}