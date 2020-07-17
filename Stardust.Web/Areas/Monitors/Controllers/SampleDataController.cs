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
            var dataId = p["dataId"].ToInt(-1);
            var appId = p["appId"].ToInt(-1);
            var name = p["name"] + "";
            var traceId = p["traceId"];
            var spanId = p["spanId"];
            var parentId = p["parentId"];
            var success = p["success"]?.ToBoolean();

            //var start = p["dtStart"].ToDateTime();
            //var end = p["dtEnd"].ToDateTime();
            var start = p["start"].ToLong(-1);
            var end = p["end"].ToLong(-1);

            return SampleData.Search(dataId, appId, name, traceId, spanId, parentId, success, start, end, p["Q"], p);
        }
    }
}