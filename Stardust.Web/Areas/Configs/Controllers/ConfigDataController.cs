using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigDataController : EntityController<ConfigData>
    {
        static ConfigDataController()
        {
            ListFields.AddField("Scope", "Value");
        }

        protected override IEnumerable<ConfigData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["key"];
            var scope = p["scope"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return ConfigData.Search(appId, name, scope, start, end, p["Q"], p);
        }
    }
}