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
    [Menu(70)]
    [DeploymentArea]
    public class AppDeployHistoryController : ReadOnlyEntityController<AppDeployHistory>
    {
        static AppDeployHistoryController() => ListFields.RemoveCreateField();

        protected override IEnumerable<AppDeployHistory> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployHistory.FindById(id);
                if (entity != null) return new List<AppDeployHistory> { entity };
            }

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppDeployHistory.Search(start, end, p["Q"], p);
        }

        protected override Boolean Valid(AppDeployHistory entity, DataObjectMethodType type, Boolean post)
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