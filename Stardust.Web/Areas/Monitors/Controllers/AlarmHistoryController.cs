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

        /// <summary>菜单不可见</summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        protected override IDictionary<System.Reflection.MethodInfo, Int32> ScanActionMenu(XCode.Membership.IMenu menu)
        {
            if (menu.Visible)
            {
                menu.Visible = false;
                (menu as XCode.IEntity).Update();
            }

            return base.ScanActionMenu(menu);
        }
    }
}