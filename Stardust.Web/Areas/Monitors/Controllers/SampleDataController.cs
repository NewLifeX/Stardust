using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class SampleDataController : EntityController<SampleData>
    {
        static SampleDataController() => MenuOrder = 80;

        protected override IEnumerable<SampleData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var traceId = p["traceId"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return SampleData.Search(appId, traceId, start, end, p["Q"], p);
        }
    }
}