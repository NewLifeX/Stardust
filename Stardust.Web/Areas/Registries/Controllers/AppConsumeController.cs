using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppConsumeController : EntityController<AppConsume>
    {
        static AppConsumeController()
        {
            MenuOrder = 73;
        }

        protected override IEnumerable<AppConsume> Search(Pager p)
        {
            PageSetting.EnableAdd = false;
            PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);
            var serviceId = p["serviceId"].ToInt(-1);
            var enable = p["enable"]?.ToBoolean();

            return AppConsume.Search(appId, serviceId, enable, p["Q"], p);
        }
    }
}