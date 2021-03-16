using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Remoting;
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

            PageSetting.EnableSelect = false;

            // 如果选择了应用，特殊处理版本
            if (appId > 0)
            {
                if (p.PageSize == 20) p.PageSize = 500;

                var list = ConfigData.Search(appId, name, scope, start, end, p["Q"], p);

                // 选择每个版本最大的一个
                list = ConfigData.SelectNewest(list);

                // 控制发布按钮
                var app = AppConfig.FindById(appId);
                PageSetting.EnableSelect = list.Any(e => e.Version > app.Version);

                return list;
            }

            return ConfigData.Search(appId, name, scope, start, end, p["Q"], p);
        }

        public override ActionResult Add(ConfigData entity)
        {
            entity.Version = entity.App.AcquireNewVersion();
            base.Add(entity);

            return RedirectToAction("Index", new { appId = entity.AppId });
        }

        public override ActionResult Edit(ConfigData entity)
        {
            var ver = entity.App.AcquireNewVersion();

            // 如果当前版本是待发布版本，则编辑，否则添加
            if (entity.Version >= ver)
            {
                entity.Version = ver;
                base.Edit(entity);
            }
            else
            {
                // 强行改为添加
                entity.Id = 0;
                entity.Version = ver;
                base.Add(entity);
            }

            return RedirectToAction("Index", new { appId = entity.AppId });
        }

        public ActionResult Publish(Int32 appId)
        {
            try
            {
                var app = AppConfig.FindById(appId);
                if (app == null) throw new ArgumentNullException(nameof(appId));

                if (app.Version >= app.NextVersion) throw new ApiException(701, "已经是最新版本！");
                app.Publish();

                return JsonRefresh("发布成功！", 3);
            }
            catch (Exception ex)
            {
                return Json(0, ex.Message, ex);
            }
        }
    }
}