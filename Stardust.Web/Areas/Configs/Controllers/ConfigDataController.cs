using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
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

            AddFormFields.RemoveCreateField();
            AddFormFields.RemoveField("Version");

            EditFormFields.RemoveCreateField();
            EditFormFields.RemoveUpdateField();
            EditFormFields.RemoveField("Version");
        }

        protected override IEnumerable<ConfigData> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var name = p["key"];
            var scope = p["scope"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            // 如果选择了应用，特殊处理版本
            if (appId > 0)
            {
                if (p.PageSize == 20) p.PageSize = 500;

                var list = ConfigData.Search(appId, name, scope, start, end, p["Q"], p);

                // 选择每个版本最大的一个
                list = ConfigData.SelectNewest(list);

                return list;
            }

            return ConfigData.Search(appId, name, scope, start, end, p["Q"], p);
        }

        public override ActionResult Add(ConfigData entity)
        {
            base.Add(entity);

            return RedirectToAction("Index", new { appId = entity.AppId });
        }

        public override ActionResult Edit(ConfigData entity)
        {
            base.Edit(entity);

            return RedirectToAction("Index", new { appId = entity.AppId });
        }
    }
}