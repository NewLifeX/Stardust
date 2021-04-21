using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
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
                var df = ListFields.AddDataField("AddNode", null, "Enable");
                df.Header = "节点";
                df.DisplayName = "添加节点";
                df.Title = "添加服务器节点";
                df.Url = "AppDeployNode/Add?appId={AppId}&deployId={Id}";
            }

            {
                var df = ListFields.AddDataField("Nodes");
                df.Header = "节点";
                //df.DisplayName = "添加节点";
                //df.Title = "添加服务器节点";
                df.Url = "AppDeployNode?deployId={Id}";
            }

            {
                var df = ListFields.AddDataField("Log", "UpdateUserId");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用部署&linkId={Id}";
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
            if (!post)
            {
                if (type == DataObjectMethodType.Insert)
                {
                    entity.Enable = true;
                    entity.AutoStart = true;
                }

                return base.Valid(entity, type, post);
            }

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