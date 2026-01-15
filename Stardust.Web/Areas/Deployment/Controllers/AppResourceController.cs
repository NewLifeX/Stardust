using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Deployment.AppResource;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>部署资源。附加资源管理，如数据库驱动、SSL证书、配置模板等，支持全局/项目/应用三级归属</summary>
[Menu(40, true, Icon = "fa-table")]
[DeploymentArea]
public class AppResourceController : DeploymentEntityController<AppResource>
{
    static AppResourceController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?Id={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Deployment/AppResource?Id={Id}";
        }
        {
            var df = ListFields.AddListField("Version", "Enable");
            df.DisplayName = "版本";
            df.Title = "管理资源版本";
            df.Url = "/Deployment/AppResourceVersion?resourceId={Id}";
        }
        {
            var df = ListFields.AddListField("Membership", "Enable");
            df.DisplayName = "应用资源";
            df.Title = "管理所有绑定到当前资源的部署集";
            df.Url = "/Deployment/AppDeployResource?resourceId={Id}";
        }
    }

    //private readonly ITracer _tracer;

    //public AppResourceController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AppResource> Search(Pager p)
    {
        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var unZip = p["unZip"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppResource.Search(projectId, category, unZip, enable, start, end, p["Q"], p);
    }
}