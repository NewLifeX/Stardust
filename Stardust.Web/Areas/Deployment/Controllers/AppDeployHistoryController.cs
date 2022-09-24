using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

[Menu(70)]
[DeploymentArea]
public class AppDeployHistoryController : ReadOnlyEntityController<AppDeployHistory>
{
    static AppDeployHistoryController()
    {
        ListFields.AddDataField("Remark", null, "Success");
        ListFields.TraceUrl();
    }

    protected override IEnumerable<AppDeployHistory> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployHistory.FindById(id);
            if (entity != null) return new List<AppDeployHistory> { entity };
        }

        var appId = p["appId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppDeployHistory.Search(appId, nodeId, null, start, end, p["Q"], p);
    }
}