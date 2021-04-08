using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppHistoryController : EntityController<AppHistory>
    {
        static AppHistoryController() => MenuOrder = 93;

        protected override IEnumerable<AppHistory> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (start.Year < 2000) start = DateTime.Today;

            return AppHistory.Search(appId, start, end, p["Q"], p);
        }
    }
}