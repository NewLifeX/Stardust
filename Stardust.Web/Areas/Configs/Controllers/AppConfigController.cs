using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data;
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
            ListFields.RemoveField("AppId", "AppName", "PublishTime", "Quotes", "QuoteNames", "EnableApollo", "ApolloMetaServer", "ApolloAppId", "ApolloNameSpace", "Remark");

            {
                var df = ListFields.AddListField("ConfigData", "Enable");
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
                var df = ListFields.AddListField("Online", "PublishTime");
                df.Header = "在线实例";
                df.DisplayName = "在线实例";
                df.Title = "查看该应用的在线实例应用";
                df.Url = "/registry/AppOnline?appId={AppId}";
                df.DataVisible = (e, f) => (e is AppConfig entity && entity.AppId > 0);
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

        private readonly StarFactory _starFactory;
        private readonly ITracer _tracer;

        public AppConfigController(StarFactory starFactory, ITracer tracer)
        {
            _starFactory = starFactory;
            _tracer = tracer;
        }

        protected override IEnumerable<AppConfig> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppConfig.FindById(id);
                if (entity != null) return new List<AppConfig> { entity };
            }
            var appId = p["appId"].ToInt(-1);
            if (appId > 0)
            {
                var entity = AppConfig.FindByAppId(appId);
                if (entity != null) return new List<AppConfig> { entity };
            }

            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppConfig.Search(category, enable, start, end, p["Q"], p);
        }

        protected override Boolean Valid(AppConfig entity, DataObjectMethodType type, Boolean post)
        {
            if (post)
            {
                // 更新时关联应用
                switch (type)
                {
                    case DataObjectMethodType.Update:
                    case DataObjectMethodType.Insert:
                        if (entity.AppId == 0)
                        {
                            var app = App.FindByName(entity.Name);
                            if (app != null) entity.AppId = app.Id;
                        }
                        break;
                }
            }

            return base.Valid(entity, type, post);
        }

        public async Task<ActionResult> Publish(Int32 appId)
        {
            using var span = _tracer?.NewSpan(nameof(Publish), appId + "");
            try
            {
                var app = AppConfig.FindById(appId);
                if (app == null) throw new ArgumentNullException(nameof(appId));

                if (app.Version >= app.NextVersion) throw new ApiException(701, "已经是最新版本！");
                app.Publish();

                await _starFactory.SendAppCommand(app.Name, "config/publish", "");

                return JsonRefresh("发布成功！", 3);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                return Json(0, ex.Message, ex);
            }
        }
    }
}