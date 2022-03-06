using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(95)]
    public class AppOnlineController : EntityController<AppOnline>
    {
        static AppOnlineController()
        {
            ListFields.RemoveField("Token");

            {
                var df = ListFields.AddListField("Meter", null, "PingCount");
                df.DisplayName = "性能";
                df.Header = "性能";
                df.Url = "AppMeter?appId={AppId}";
            }
        }

        protected override IEnumerable<AppOnline> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var category = p["category"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppOnline.Search(appId, category, start, end, p["Q"], p);
        }
    }
}