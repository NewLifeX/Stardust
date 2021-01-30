using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Remoting;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppConfigController : EntityController<AppConfig>
    {
        static AppConfigController()
        {
            MenuOrder = 58;

            ListFields.RemoveCreateField();

            {
                var df = ListFields.AddDataField("Configs", "Enable");
                df.Header = "配置";
                df.DisplayName = "配置";
                df.Title = "查看该应用所有配置数据";
                df.Url = "ConfigData?appId={Id}";
            }

            {
                var df = ListFields.AddDataField("Publish", "CreateUserID");
                df.Header = "发布";
                df.DisplayName = "发布";
                df.Url = "Appconfig/Publish?appId={Id}";
                df.DataAction = "action";
            }

            // 异步同步应用
            {
                Task.Run(() => AppConfig.Sync());
            }
        }

        public AppConfigController()
        {
            PageSetting.EnableAdd = false;
        }

        //protected override IEnumerable<AppConfig> Search(Pager p)
        //{
        //    var appId = p["appId"].ToInt(-1);

        //    var start = p["dtStart"].ToDateTime();
        //    var end = p["dtEnd"].ToDateTime();

        //    return AppConfig.Search(appId, start, end, p["Q"], p);
        //}

        public ActionResult Publish(Int32 appId)
        {
            try
            {
                var app = AppConfig.FindById(appId);
                if (app == null) throw new ArgumentNullException(nameof(appId));

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