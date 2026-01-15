using System.ComponentModel;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using XCode;

namespace Stardust.Web.Areas.Deployment;

[DisplayName("发布中心")]
[Menu(555, true, LastUpdate = "20260115")]
public class DeploymentArea : AreaBase
{
    public DeploymentArea() : base(nameof(DeploymentArea).TrimEnd("Area")) { }

    static DeploymentArea() => RegisterArea<DeploymentArea>();
}

[DeploymentArea]
public class DeploymentEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId <= 0) appId = GetRequest("deployId").ToInt(-1);
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

            var nodeId = GetRequest("nodeId").ToInt(-1);
            if (nodeId > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }
}