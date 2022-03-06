using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(99)]
    public class AppController : EntityController<App>
    {
        static AppController()
        {
            LogOnChange = true;

            ListFields.RemoveField("Secret", "WebHook");
            ListFields.RemoveCreateField();

            {
                var df = ListFields.AddListField("Online", "LastLogin");
                df.DisplayName = "在线";
                df.Header = "在线";
                df.Url = "AppOnline?appId={Id}";
            }
            {
                var df = ListFields.AddListField("AppLog", "LastLogin");
                df.DisplayName = "应用日志";
                df.Header = "应用日志";
                df.Url = "AppLog?appId={Id}";
            }
            {
                var df = ListFields.AddListField("History", null, "AutoActive");
                df.DisplayName = "历史";
                df.Header = "历史";
                df.Url = "AppHistory?appId={Id}";
            }
            {
                var df = ListFields.AddListField("Meter", null, "AutoActive");
                df.DisplayName = "性能";
                df.Header = "性能";
                df.Url = "AppMeter?appId={Id}";
            }
            {
                var df = ListFields.AddListField("Deploy", null, "AutoActive");
                df.DisplayName = "部署";
                df.Header = "部署";
                df.Url = "/Deployment/AppDeploy?appId={Id}";
            }
            {
                var df = ListFields.AddListField("Providers", null, "AutoActive");
                df.DisplayName = "提供服务";
                df.Header = "提供服务";
                df.Url = "AppService?appId={Id}";
                df.DataVisible = (e, f) => (e as App).Providers.Count > 0;
            }
            {
                var df = ListFields.AddListField("Consumers", null, "AutoActive");
                df.DisplayName = "消费服务";
                df.Header = "消费服务";
                df.Url = "AppConsume?appId={Id}";
                df.DataVisible = (e, f) => (e as App).Consumers.Count > 0;
            }
            {
                var df = ListFields.AddListField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用系统&linkId={Id}";
            }
        }

        protected override IEnumerable<App> Search(Pager p)
        {
            var id = p["Id"].ToInt(-1);
            if (id > 0)
            {
                var node = App.FindById(id);
                if (node != null) return new[] { node };
            }

            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return App.Search(category, enable, start, end, p["Q"], p);
        }

        /// <summary>搜索</summary>
        /// <param name="category"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ActionResult AppSearch(String category, String key = null)
        {
            var page = new PageParameter { PageSize = 20 };

            //// 默认排序
            //if (page.Sort.IsNullOrEmpty()) page.Sort = _.Name;

            var list = App.Search(category, true, DateTime.MinValue, DateTime.MinValue, key, page);

            return Json(0, null, list.Select(e => new
            {
                e.Id,
                e.Name,
                e.DisplayName,
                e.Category,
            }).ToArray());
        }

        /// <summary>启用禁用下线告警</summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult SetAlarm(Boolean enable = true)
        {
            foreach (var item in SelectKeys)
            {
                var dt = App.FindById(item.ToInt());
                if (dt != null)
                {
                    dt.AlarmOnOffline = enable;
                    dt.Save();
                }
            }

            return JsonRefresh("操作成功！");
        }
    }
}