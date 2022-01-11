using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Configs;
using XCode;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [Menu(58)]
    [ConfigsArea]
    public class AppConfigController : EntityController<AppConfig>
    {
        static AppConfigController()
        {
            LogOnChange = true;

            ListFields.RemoveCreateField();
            ListFields.RemoveField("EnableApollo", "ApolloMetaServer", "ApolloAppId", "ApolloNameSpace");

            {
                var df = ListFields.AddListField("Configs", "Enable");
                df.Header = "管理配置";
                df.DisplayName = "管理配置";
                df.Title = "查看该应用所有配置数据";
                df.Url = "ConfigData?appId={Id}";
            }

            {
                var df = ListFields.AddListField("Publish", "PublishTime");
                df.Header = "发布";
                df.DisplayName = "发布";
                df.Url = "Appconfig/Publish?appId={Id}";
                df.DataAction = "action";
                df.DataVisible = (e, f) => (e is AppConfig ac && ac.Version < ac.NextVersion);
            }

            {
                var df = ListFields.AddListField("History", "PublishTime");
                df.Header = "历史";
                df.DisplayName = "历史";
                df.Title = "查看该应用的配置历史";
                df.Url = "ConfigHistory?appId={Id}";
            }

            {
                var df = ListFields.AddListField("Preview", "PublishTime");
                df.Header = "预览";
                df.DisplayName = "预览";
                df.Title = "查看该应用的配置数据";
                df.Url = "/config/getall?appId={Name}&secret={appSecret}";
            }

            {
                var df = ListFields.AddListField("Log", "UpdateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用配置&linkId={Id}";
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

        protected override IEnumerable<AppConfig> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppConfig.FindById(id);
                if (entity != null) return new List<AppConfig> { entity };
            }

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppConfig.Search(start, end, p["Q"], p);
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