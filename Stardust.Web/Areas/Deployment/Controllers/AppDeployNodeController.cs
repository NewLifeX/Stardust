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
    public class AppDeployNodeController : EntityController<AppDeployNode>
    {
        static AppDeployNodeController()
        {
            MenuOrder = 88;

            ListFields.RemoveCreateField();
        }

        protected override IEnumerable<AppDeployNode> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployNode.FindById(id);
                if (entity != null) return new List<AppDeployNode> { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var nodeId = p["nodeId"].ToInt(-1);

            PageSetting.EnableAdd = appId > 0;

            return AppDeployNode.Search(appId,  nodeId, p["Q"], p);
        }

        protected override Boolean Valid(AppDeployNode entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

            var err = "";
            try
            {
                entity.App?.Fix();

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