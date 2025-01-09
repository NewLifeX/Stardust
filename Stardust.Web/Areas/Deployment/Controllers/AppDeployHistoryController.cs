using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

[Menu(70, false)]
[DeploymentArea]
public class AppDeployHistoryController : ReadOnlyEntityController<AppDeployHistory>
{
    static AppDeployHistoryController()
    {
        ListFields.RemoveField("Id");
        ListFields.AddDataField("Remark", null, "TraceId");
        ListFields.TraceUrl();

        {
            var df = ListFields.GetField("DeployName") as ListField;
            df.Url = "/Deployment/AppDeploy?deployId={DeployId}";
        }
        {
            var df = ListFields.GetField("NodeName") as ListField;
            df.Url = "/Nodes/Node?Id={NodeId}";
            df.Target = "_frame";
        }
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        var deployId = GetRequest("deployId").ToInt(-1);
        if (deployId > 0 || appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var deployId = GetRequest("deployId").ToInt(-1);
            if (deployId > 0) fields.RemoveField("DeployName");
        }

        return fields;
    }

    protected override IEnumerable<AppDeployHistory> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployHistory.FindById(id);
            if (entity != null) return new List<AppDeployHistory> { entity };
        }

        var appId = p["deployId"].ToInt(-1);
        if (appId <= 0) appId = p["appId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppDeployHistory.Search(appId, nodeId, null, start, end, p["Q"], p);
    }
}