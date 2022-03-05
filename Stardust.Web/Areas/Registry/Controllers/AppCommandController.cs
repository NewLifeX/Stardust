using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [Menu(58)]
    [RegistryArea]
    public class NodeCommandController : EntityController<AppCommand>
    {
        protected override IEnumerable<AppCommand> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var command = p["command"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppCommand.Search(appId, command, start, end, p["Q"], p);
        }
    }
}