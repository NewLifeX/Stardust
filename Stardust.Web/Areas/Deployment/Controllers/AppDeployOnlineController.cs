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
    public class AppDeployOnlineController : ReadOnlyEntityController<AppDeployOnline>
    {
        static AppDeployOnlineController()
        {
            MenuOrder = 88;

            ListFields.RemoveCreateField();
        }

        protected override IEnumerable<AppDeployOnline> Search(Pager p)
        {
            var id = p["id"].ToInt(-1);
            if (id > 0)
            {
                var entity = AppDeployOnline.FindById(id);
                if (entity != null) return new List<AppDeployOnline> { entity };
            }

            var deployId = p["deployId"].ToInt(-1);
            var appId = p["appId"].ToInt(-1);
            var nodeId = p["nodeId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            PageSetting.EnableAdd = false;

            return AppDeployOnline.Search(appId, deployId, nodeId, start, end, p["Q"], p);
        }
    }
}