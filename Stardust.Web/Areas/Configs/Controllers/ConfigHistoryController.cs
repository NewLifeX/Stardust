using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigHistoryController : EntityController<ConfigHistory>
    {
        protected override IEnumerable<ConfigHistory> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var configId = p["configId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return ConfigHistory.Search(appId, configId, start, end, p["Q"], p);
        }
    }
}