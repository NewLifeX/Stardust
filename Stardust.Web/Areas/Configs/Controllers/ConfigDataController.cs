using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Configs;
using XCode;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class ConfigDataController : EntityController<ConfigData>
    {
        static ConfigDataController()
        {
            ListFields.AddField("Scope", "Value");

            AddFormFields.RemoveCreateField();
            AddFormFields.RemoveUpdateField();
            AddFormFields.RemoveField("Version");

            EditFormFields.RemoveCreateField();
            EditFormFields.RemoveUpdateField();
            EditFormFields.RemoveField("Version");

            {
                var df = AddFormFields.GetField("Value");
                df.Readonly = true;
            }
            {
                var df = EditFormFields.GetField("Value");
                df.Readonly = true;
            }
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
            if (appId > 0 && p.PageSize == 20) p.PageSize = 500;

            var list = ConfigData.Search(appId, name, scope, start, end, p["Q"], p);

            PageSetting.EnableAdd = false;
            if (appId > 0)
            {
                PageSetting.EnableAdd = true;

                // 控制发布按钮
                var app = AppConfig.FindById(appId);
                PageSetting.EnableSelect = list.Any(e => e.Version > app.Version);
            }

            return list;
        }

        public override ActionResult Add(ConfigData entity)
        {
            entity.Version = entity.App.AcquireNewVersion();
            base.Add(entity);

            return RedirectToAction("Index", new { appId = entity.AppId });
        }

        //public override ActionResult Edit(ConfigData entity)
        //{
        //    base.Edit(entity);

        //    return RedirectToAction("Index", new { appId = entity.AppId });
        //}

        protected override Int32 OnUpdate(ConfigData entity)
        {
            var e = entity as IEntity;
            if (e.HasDirty)
            {
                if (e.Dirtys[nameof(entity.Key)]) throw new ArgumentException("禁止修改名称，建议新增配置", nameof(entity.Key));
                if (e.Dirtys[nameof(entity.Scope)]) throw new ArgumentException("禁止修改作用域，建议新增配置", nameof(entity.Scope));
                if (e.Dirtys[nameof(entity.Value)]) throw new ArgumentException("禁止修改正在使用的数值！", nameof(entity.Value));

                var ver = entity.App.AcquireNewVersion();
                entity.Version = ver;
            }
            if (e.Dirtys[nameof(entity.Enable)])
            {
                // 禁用启用修改为期望值，发布后才能执行
                entity.DesiredValue = entity.Enable ? ConfigData.ENABLED : ConfigData.DISABLED;
                entity.Enable = !entity.Enable;
            }

            return base.OnUpdate(entity);
        }

        protected override Int32 OnDelete(ConfigData entity)
        {
            // 删除操作，直接修改为即将被删除
            var ver = entity.App.AcquireNewVersion();
            entity.Version = ver;
            entity.DesiredValue = ConfigData.DELETED;
            var rs = base.OnUpdate(entity);

            return rs;
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