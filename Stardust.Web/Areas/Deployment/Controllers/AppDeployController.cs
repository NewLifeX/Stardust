using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [DeploymentArea]
    public class AppDeployController : EntityController<AppDeploy>
    {
        static AppDeployController()
        {
            MenuOrder = 90;

            ListFields.RemoveCreateField();
            ListFields.RemoveField("ApolloMetaServer");

            {
                var df = ListFields.AddDataField("Configs", "Enable");
                df.Header = "配置";
                df.DisplayName = "配置";
                df.Title = "查看该应用所有配置数据";
                df.Url = "ConfigData?appId={Id}";
            }

            {
                var df = ListFields.AddDataField("Publish", "PublishTime");
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
                df.Url = "/config/getall?appId={Name}&secret={appSecret}";
            }

            {
                var df = ListFields.AddDataField("Log", "UpdateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用配置&linkId={Id}";
            }

            {
                var df = AddFormFields.AddDataField("Nodes");
                df.DataSource = (entity, field) => Node.FindAllWithCache().Where(e => e.Enable).ToDictionary(e => e.ID, e => e.Name);
            }

            {
                var df = EditFormFields.AddDataField("Nodes");
                df.DataSource = (entity, field) => Node.FindAllWithCache().Where(e => e.Enable).ToDictionary(e => e.ID, e => e.Name);
            }
        }

        protected override IEnumerable<AppDeploy> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeploy.FindById(id);
                if (entity != null) return new List<AppDeploy> { entity };
            }

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppDeploy.Search(start, end, p["Q"], p);
        }

        protected override Boolean Valid(AppDeploy entity, DataObjectMethodType type, Boolean post)
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
    }
}