using System;
using System.Collections.Generic;
using System.Reflection;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppServiceController : EntityController<AppService>
    {
        static AppServiceController()
        {
            MenuOrder = 75;
        }

        protected override IEnumerable<AppService> Search(Pager p)
        {
            PageSetting.EnableAdd = false;
            PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);
            var serviceId = p["serviceId"].ToInt(-1);
            var enable = p["enable"]?.ToBoolean();

            return AppService.Search(appId, serviceId, enable, p["Q"], p);
        }

        /// <summary>菜单不可见</summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        protected override IDictionary<MethodInfo, Int32> ScanActionMenu(IMenu menu)
        {
            if (menu.Visible)
            {
                menu.Visible = false;
                (menu as IEntity).Update();
            }

            return base.ScanActionMenu(menu);
        }
    }
}