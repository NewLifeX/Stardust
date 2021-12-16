using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigOnlineController : ReadOnlyEntityController<ConfigOnline>
    {
        static ConfigOnlineController()
        {
            ListFields.RemoveField("Token");
        }

        protected override IEnumerable<ConfigOnline> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var clientId = p["clientId"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return ConfigOnline.Search(appId, clientId, start, end, p["Q"], p);
        }
    }
}