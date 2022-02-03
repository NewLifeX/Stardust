using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    [Menu(70)]
    [DeploymentArea]
    public class AppDeployHistoryController : ReadOnlyEntityController<AppDeployHistory>
    {
        static AppDeployHistoryController()
        {
            LogOnChange = true;

            ListFields.RemoveCreateField();
        }

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
    }
}