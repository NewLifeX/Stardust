using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AppTracerController : EntityController<AppTracer>
    {
        static AppTracerController() => MenuOrder = 90;

        protected override IEnumerable<AppTracer> Search(Pager p)
        {
            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppTracer.Search(category, enable, start, end, p["Q"], p);
        }
    }
}