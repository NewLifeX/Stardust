using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigHistoryController : ReadOnlyEntityController<ConfigHistory>
    {
        protected override IEnumerable<ConfigHistory> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var action = p["action"];
            var success = p["success"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return ConfigHistory.Search(appId, action, success, start, end, p["Q"], p);
        }
    }
}