using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppLogController : ReadOnlyEntityController<AppLog>
    {
        static AppLogController()
        {
            MenuOrder = 50;
        }

        protected override AppLog Find(Object key) => AppLog.FindById(key.ToLong());

        protected override IEnumerable<AppLog> Search(Pager p)
        {
            PageSetting.EnableAdd = false;
            PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);
            var clientId = p["clientId"];
            var threadId = p["threadId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (start.Year < 2000) start = DateTime.Today;

            return AppLog.Search(appId, clientId, threadId, start, end, p["Q"], p);
        }
    }
}