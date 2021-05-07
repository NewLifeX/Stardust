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
    public class AppDeployVersionController : EntityController<AppDeployVersion>
    {
        static AppDeployVersionController()
        {
            MenuOrder = 80;

            ListFields.RemoveCreateField();

            AddFormFields.RemoveCreateField();
        }

        protected override IEnumerable<AppDeployVersion> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployVersion.FindById(id);
                if (entity != null) return new List<AppDeployVersion> { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            PageSetting.EnableAdd = appId > 0;

            return AppDeployVersion.Search(appId, null, start, end, p["Q"], p);
        }

        protected override Boolean Valid(AppDeployVersion entity, DataObjectMethodType type, Boolean post)
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