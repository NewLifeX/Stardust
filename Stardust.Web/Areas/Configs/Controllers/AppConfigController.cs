using System;
using System.Linq;
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

            {
                var df = ListFields.AddDataField("History", "PublishTime");
                df.Header = "历史";
                df.DisplayName = "历史";
                df.Title = "查看该应用的配置历史";
                df.Url = "ConfigHistory?appId={Id}";
            }

            {
                var df = ListFields.AddDataField("Preview", "PublishTime");
                df.Header = "预览";
                df.DisplayName = "预览";
                df.Title = "查看该应用的配置数据";
                df.Url = "/config/getall?appId={Name}";
            }

            {
                var df = AddFormFields.AddDataField("Quotes");
                df.DataSource = (entity, field) => AppConfig.FindAllWithCache().Where(e => e.CanBeQuoted).ToDictionary(e => e.Id, e => e.Name);
            }

            {
                var df = EditFormFields.AddDataField("Quotes");
                df.DataSource = (entity, field) => AppConfig.FindAllWithCache().Where(e => e.CanBeQuoted).ToDictionary(e => e.Id, e => e.Name);
            }

            //// 异步同步应用
            //{
            //    Task.Run(() => AppConfig.Sync());
            //}
        }

        //public AppConfigController()
        //{
        //    PageSetting.EnableAdd = false;
        //}

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