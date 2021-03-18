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
    [MonitorsArea]
    public class AlarmGroupController : EntityController<AlarmGroup>
    {
        static AlarmGroupController() => MenuOrder = 90;

        protected override IEnumerable<AlarmGroup> Search(Pager p)
        {
            var id = p["Id"].ToInt(-1);
            if (id > 0)
            {
                var app = AlarmGroup.FindById(id);
                if (app != null) return new[] { app };
            }

            var name = p["name"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AlarmGroup.Search(name, enable, start, end, p["Q"], p);
        }
    }
}