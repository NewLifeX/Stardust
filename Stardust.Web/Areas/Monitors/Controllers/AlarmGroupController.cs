using System;
using System.Collections.Generic;
using System.Web;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AlarmGroupController : EntityController<AlarmGroup>
    {
        static AlarmGroupController()
        {
            MenuOrder = 90;

            {
                var df = ListFields.AddDataField("History", null, "Enable");
                df.DisplayName = "告警历史";
                df.Header = "告警历史";
                df.Url = "AlarmHistory?appId={Id}";
            }
            {
                var df = ListFields.AddDataField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = $"/Admin/Log?category={HttpUtility.UrlEncode("告警组")}&linkId={{Id}}";
            }
        }

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