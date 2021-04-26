using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Configs;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Configs.Controllers
{
    [ConfigsArea]
    public class AppConfigController : EntityController<AppConfig>
    {
        static AppConfigController()
        {
            MenuOrder = 58;

            ListFields.RemoveCreateField();
            ListFields.RemoveField("EnableApollo", "ApolloMetaServer", "ApolloAppId", "ApolloNameSpace");

            {
                var df = ListFields.AddDataField("Configs", "Enable");
                df.Header = "管理配置";
                df.DisplayName = "管理配置";
                df.Title = "查看该应用所有配置数据";
                df.Url = "ConfigData?appId={Id}";
            }

            {
                var df = ListFields.AddDataField("Publish", "PublishTime");
                df.Header = "发布";
                df.DisplayName = "发布";
                df.Url = "Appconfig/Publish?appId={Id}";
                df.DataAction = "action";
                df.DataVisible = (e, f) => (e is AppConfig ac && ac.Version < ac.NextVersion);
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
                df.Url = "/config/getall?appId={Name}&secret={appSecret}";
            }

            {
                var df = ListFields.AddDataField("Log", "UpdateUserID");
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

        protected override Boolean Valid(AppConfig entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                if (type != DataObjectMethodType.Update) LogProvider.Provider.WriteLog(type + "", entity, err);
            }
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