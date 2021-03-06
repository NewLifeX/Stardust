﻿using System;
using System.Collections.Generic;
using System.Reflection;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers
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