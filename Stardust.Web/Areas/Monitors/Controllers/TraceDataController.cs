using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class TraceDataController : EntityController<TraceData>
    {
        static TraceDataController() => MenuOrder = 80;

        protected override IEnumerable<TraceData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["name"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return TraceData.Search(appId, name, start, end, p["Q"], p);
        }
    }
}