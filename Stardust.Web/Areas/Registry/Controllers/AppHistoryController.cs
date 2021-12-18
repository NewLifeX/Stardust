using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(0, false)]
    public class AppHistoryController : ReadOnlyEntityController<AppHistory>
    {
        static AppHistoryController()
        {
            ListFields.RemoveField("Id");
        }

        protected override IEnumerable<AppHistory> Search(Pager p)
        {
            //PageSetting.EnableAdd = false;
            //PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (appId > 0 && start.Year < 2000) p["dtStart"] = (start = DateTime.Today).ToFullString();

            return AppHistory.Search(appId, start, end, p["Q"], p);
        }
    }
}