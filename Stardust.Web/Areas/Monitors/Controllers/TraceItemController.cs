using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [Menu(85)]
    [MonitorsArea]
    public class TraceItemController : EntityController<TraceItem>
    {
        static TraceItemController()
        {
            LogOnChange = true;

            {
                var df = ListFields.AddListField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=跟踪项&linkId={Id}";
            }
        }

        protected override IEnumerable<TraceItem> Search(Pager p)
        {
            var id = p["Id"].ToInt(-1);
            if (id > 0)
            {
                var entity = TraceItem.FindById(id);
                if (entity != null) return new[] { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var name = p["name"];
            var kind = p["kind"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return TraceItem.Search(appId, name, kind, enable, start, end, p["Q"], p);
        }
    }
}