using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [Menu(88)]
    [DeploymentArea]
    public class AppDeployOnlineController : ReadOnlyEntityController<AppDeployOnline>
    {
        static AppDeployOnlineController() => ListFields.RemoveCreateField();

        protected override IEnumerable<AppDeployOnline> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployOnline.FindById(id);
                if (entity != null) return new List<AppDeployOnline> { entity };
            }

            var appId = p["appId"].ToInt(-1);
            var nodeId = p["nodeId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            PageSetting.EnableAdd = false;

            return AppDeployOnline.Search(appId, nodeId, start, end, p["Q"], p);
        }
    }
}