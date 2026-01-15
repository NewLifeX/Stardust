using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using XCode;

namespace Stardust.Web.Areas;

public class WebEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId <= 0) appId = GetRequest("configId").ToInt(-1);
        if (appId <= 0) appId = GetRequest("deployId").ToInt(-1);
        if (appId <= 0) appId = GetRequest("monitorId").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }

        var projectId = GetRequest("projectId").ToInt(-1);
        if (projectId > 0)
        {
            PageSetting.NavView = "_Project_Nav";
            PageSetting.EnableNavbar = false;
        }

        var nodeId = GetRequest("nodeId").ToInt(-1);
        if (nodeId > 0)
        {
            PageSetting.NavView = "_Node_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("ProjectName", "AppName", "Category");

            var projectId = GetRequest("projectId").ToInt(-1);
            if (projectId > 0) fields.RemoveField("ProjectName");

            var deployId = GetRequest("deployId").ToInt(-1);
            if (deployId > 0) fields.RemoveField("DeployName");

            var configId = GetRequest("configId").ToInt(-1);
            if (configId > 0) fields.RemoveField("ConfigName");

            var nodeId = GetRequest("nodeId").ToInt(-1);
            if (nodeId > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }
}