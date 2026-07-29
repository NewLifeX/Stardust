using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>部署资源。资源定义管理，如数据库驱动、SSL证书、配置模板等</summary>
[DeploymentArea]
[Menu(92, true)]
public class AppResourceController : DeploymentEntityController<AppResource>
{
    static AppResourceController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();
        ListFields.RemoveRemarkField();

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveUpdateField();
        AddFormFields.RemoveRemarkField();

        EditFormFields.RemoveCreateField();
        EditFormFields.RemoveUpdateField();
        EditFormFields.RemoveRemarkField();

        LogOnChange = true;

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?Id={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.AddListField("VersionManage", null, "UnZip") as ListField;
            df.DisplayName = "版本管理";
            df.Title = "管理资源版本";
            df.Url = "/Deployment/AppResourceVersion?resourceId={Id}";
        }
        {
            var df = ListFields.AddListField("DeployRefs", null, "UnZip") as ListField;
            df.DisplayName = "引用部署集";
            df.Title = "查看引用该资源的部署集";
            df.Url = "/Deployment/AppDeployResource?resourceId={Id}";
        }
    }

    /// <summary>搜索</summary>
    /// <param name="p"></param>
    /// <returns></returns>
    protected override IEnumerable<AppResource> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppResource.FindById(id);
            if (entity != null) return [entity];
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        PageSetting.EnableNavbar = false;

        return AppResource.Search(projectId, category, enable, p["Q"], p);
    }
}
