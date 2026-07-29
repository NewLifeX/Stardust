using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>应用资源关联。应用部署集引用的共享资源，发布时一并下发</summary>
[DeploymentArea]
[Menu(94, false)]
public class AppDeployResourceController : DeploymentEntityController<AppDeployResource>
{
    static AppDeployResourceController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveField("CreateIP", "CreateUserId", "CreateTime");

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveField("CreateIP", "CreateUserId", "CreateTime");

        EditFormFields.RemoveCreateField();
        EditFormFields.RemoveField("CreateIP", "CreateUserId", "CreateTime");

        LogOnChange = true;

        {
            var df = ListFields.GetField("DeployName") as ListField;
            df.Url = "/Deployment/AppDeploy?Id={DeployId}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("ResourceName") as ListField;
            df.Url = "/Deployment/AppResource?Id={ResourceId}";
            df.Target = "_blank";
        }
    }

    /// <summary>搜索</summary>
    /// <param name="p"></param>
    /// <returns></returns>
    protected override IEnumerable<AppDeployResource> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployResource.FindById(id);
            if (entity != null) return [entity];
        }

        var deployId = p["deployId"].ToInt(-1);
        var resourceId = p["resourceId"].ToInt(-1);

        PageSetting.EnableAdd = deployId > 0 || resourceId > 0;
        PageSetting.EnableNavbar = false;

        if (deployId > 0)
            return AppDeployResource.FindAllByDeployId(deployId);
        if (resourceId > 0)
            return AppDeployResource.FindAllByResourceId(resourceId);

        return AppDeployResource.FindAll();
    }
}
