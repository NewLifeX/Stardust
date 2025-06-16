using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>编译节点。应用部署集和编译节点的关系，一个应用可有多个部署集如arm和x64，在目标节点上发布该部署集对应的应用zip包</summary>
[Menu(20, true, Icon = "fa-table")]
[DeploymentArea]
public class AppBuildNodeController : EntityController<AppBuildNode>
{
    static AppBuildNodeController()
    {
        LogOnChange = true;

        AddFormFields.RemoveCreateField();
        ListFields.RemoveCreateField().RemoveRemarkField();

        ListFields.TraceUrl("TraceId");

        {
            var df = ListFields.GetField("DeployName") as ListField;
            df.Url = "/Deployment/AppDeploy?deployId={DeployId}";
        }
        {
            var df = ListFields.AddListField("BuildUpload", null, "Enable");
            df.DisplayName = "编译上传";
            df.Url = "/Deployment/AppBuildNode/Operate?Id={Id}&act=Build-Upload";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("PackageUpload", null, "Enable");
            df.DisplayName = "打包上传";
            df.Url = "/Deployment/AppBuildNode/Operate?Id={Id}&act=Package-Upload";
            df.DataAction = "action";
        }
        {
            var df = AddFormFields.GetField("NodeName") as FormField;
            df.GroupView = "_Form_SelectNode";
        }
        {
            var df = EditFormFields.GetField("NodeName") as FormField;
            df.GroupView = "_Form_SelectNode";
        }
        {
            var df = DetailFields.GetField("NodeName") as FormField;
            df.GroupView = "_Form_SelectNode";
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AppBuildNode> Search(Pager p)
    {
        var deployId = p["deployId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);
        var pullCode = p["pullCode"]?.ToBoolean();
        var buildProject = p["buildProject"]?.ToBoolean();
        var packageOutput = p["packageOutput"]?.ToBoolean();
        var uploadPackage = p["uploadPackage"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppBuildNode.Search(deployId, nodeId, pullCode, buildProject, packageOutput, uploadPackage, enable, start, end, p["Q"], p);
    }

    /// <summary>执行操作</summary>
    /// <param name="act"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Operate(String act, Int32 id)
    {
        var dn = AppDeployNode.FindById(id);
        if (dn == null || dn.Node == null || dn.Deploy == null) return Json(500, $"[{id}]不存在");

        var deployName = dn.DeployName;
        if (deployName.IsNullOrEmpty()) deployName = dn.Deploy?.Name;
        //await _deployService.Control(dn.Deploy, dn, act, UserHost, 0, 0);

        return JsonRefresh($"在节点[{dn.NodeName}]上对应用[{deployName}]执行[{act}]操作", 1);
    }
}