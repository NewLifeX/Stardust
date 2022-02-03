using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [Menu(90)]
    [MonitorsArea]
    public class AppTracerController : EntityController<AppTracer>
    {
        static AppTracerController()
        {
            LogOnChange = true;

            {
                var df = ListFields.AddListField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用跟踪器&linkId={Id}";
            }
        }

        protected override IEnumerable<AppTracer> Search(Pager p)
        {
            var id = p["Id"].ToInt(-1);
            if (id > 0)
            {
                var app = AppTracer.FindByID(id);
                if (app != null) return new[] { app };
            }

            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppTracer.Search(category, enable, start, end, p["Q"], p);
        }
    }
}