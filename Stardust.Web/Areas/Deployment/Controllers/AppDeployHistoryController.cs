using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
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

            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is AppDeployHistory entity && !entity.TraceId.IsNullOrEmpty();
            }
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