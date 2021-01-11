using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.ConfigCenter;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppRuleController : EntityController<AppRule>
    {
        //protected override IEnumerable<AppRule> Search(Pager p)
        //{
        //    var appId = p["appId"].ToInt(-1);

        //    var start = p["dtStart"].ToDateTime();
        //    var end = p["dtEnd"].ToDateTime();

        //    return AppRule.Search(appId, start, end, p["Q"], p);
        //}
    }
}