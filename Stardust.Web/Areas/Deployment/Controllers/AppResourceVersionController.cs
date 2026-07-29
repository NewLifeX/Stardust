using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>资源版本。资源的多版本管理，支持不同运行时平台</summary>
[DeploymentArea]
[Menu(93, false)]
public class AppResourceVersionController : DeploymentEntityController<AppResourceVersion>
{
    static AppResourceVersionController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();
        ListFields.RemoveRemarkField();
        ListFields.RemoveField("TraceId");

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveUpdateField();
        AddFormFields.RemoveRemarkField();
        AddFormFields.RemoveField("TraceId");

        EditFormFields.RemoveCreateField();
        EditFormFields.RemoveUpdateField();
        EditFormFields.RemoveRemarkField();
        EditFormFields.RemoveField("TraceId");

        LogOnChange = true;

        {
            var df = ListFields.GetField("ResourceName") as ListField;
            df.Url = "/Deployment/AppResource?Id={ResourceId}";
            df.Target = "_blank";
        }
    }

    /// <summary>搜索</summary>
    /// <param name="p"></param>
    /// <returns></returns>
    protected override IEnumerable<AppResourceVersion> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppResourceVersion.FindById(id);
            if (entity != null) return [entity];
        }

        var resourceId = p["resourceId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        PageSetting.EnableAdd = resourceId > 0;
        PageSetting.EnableNavbar = false;

        return AppResourceVersion.Search(resourceId, enable, p["Q"], p);
    }
}
