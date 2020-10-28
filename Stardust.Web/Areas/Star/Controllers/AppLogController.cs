using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    [StarArea]
    public class AppLogController : ReadOnlyEntityController<AppLog>
    {
        static AppLogController()
        {
            MenuOrder = 50;
        }

        protected override IEnumerable<AppLog> Search(Pager p)
        {
            PageSetting.EnableAdd = false;
            PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);
            var clientId = p["clientId"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (start.Year < 2000) start = DateTime.Today;

            return AppLog.Search(appId, clientId, start, end, p["Q"], p);
        }
    }
}