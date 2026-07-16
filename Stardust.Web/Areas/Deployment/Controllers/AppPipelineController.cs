using Microsoft.AspNetCore.Mvc;
using Stardust;
using Stardust.Data.Deployment;
using Stardust.Models;
using Stardust.Web.Services;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线。流水线配置，关联一个应用部署集与一个编译节点，监听指定分支的 webhook 触发，自动完成代码拉取、编译、上传、部署的全流程</summary>
[Menu(30, true, Icon = "fa-code-fork")]
[DeploymentArea]
public class AppPipelineController : EntityController<AppPipeline>
{
    private readonly PipelineService _pipelineService;

    static AppPipelineController()
    {
        // 列表字段：去掉敏感字段
        ListFields.RemoveField("Id", "Token", "Secret", "DeployNodeIds", "TraceId");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.TraceUrl("TraceId");

        // 表单字段：Token/Secret/TraceId 不展示，新建时后台自动生成
        AddFormFields.RemoveField("Token", "Secret", "TraceId");
        EditFormFields.RemoveField("Token", "Secret", "TraceId");
        DetailFields.RemoveField("TraceId");

        // 流水线历史列：跳转到该流水线的运行记录列表
        {
            var df = ListFields.AddListField("History", "UpdateUserId");
            df.DisplayName = "流水线历史";
            df.Url = "/Deployment/AppPipelineRun?pipelineId={Id}";
        }

        // 编译节点单选（搜索建议组件，保持 GroupView）
        {
            var df = AddFormFields.GetField("BuildNodeId") as FormField;
            if (df != null) df.GroupView = "_Form_SelectBuildNode";
        }
        {
            var df = EditFormFields.GetField("BuildNodeId") as FormField;
            if (df != null) df.GroupView = "_Form_SelectBuildNode";
        }
        // 部署节点多选（级联：按 DeployId 过滤，GroupView + 前端 JS 实现级联）
        {
            var df = AddFormFields.GetField("DeployNodeIds") as FormField;
            if (df != null) df.GroupView = "_Form_SelectDeployNodes";
        }
        {
            var df = EditFormFields.GetField("DeployNodeIds") as FormField;
            if (df != null) df.GroupView = "_Form_SelectDeployNodes";
        }
    }

    public AppPipelineController(PipelineService pipelineService)
    {
        _pipelineService = pipelineService;
    }

    /// <summary>高级搜索</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppPipeline> Search(Pager p)
    {
        var deployId = p["deployId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();
        return AppPipeline.FindAll(
            (deployId < 0 ? null : AppPipeline._.DeployId == deployId) &
            (enable == null ? null : AppPipeline._.Enable == enable.Value) &
            AppPipeline._.UpdateTime.Between(p["dtStart"].ToDateTime(), p["dtEnd"].ToDateTime()),
            p);
    }

    /// <summary>复制 webhook URL 到剪贴板</summary>
    /// <param name="id">流水线编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult CopyWebhook(Int32 id)
    {
        var entity = AppPipeline.FindById(id);
        if (entity == null) return Json(500, "流水线不存在");

        var serviceAddress = StarSetting.Current.ServiceAddress?.Split(",").FirstOrDefault(e => e.StartsWithIgnoreCase("http"));
        var url = $"{serviceAddress}/Pipeline/Webhook?token={entity.Token}";

        return Json(0, "ok", url);
    }

    /// <summary>手动触发流水线运行。委托 PipelineService.Trigger，参考 BatchOperate 模式通过 DeployService 下发指令</summary>
    /// <param name="id">流水线编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Trigger(Int32 id)
    {
        try
        {
            var run = await _pipelineService.Trigger(id, UserHost);
            return JsonRefresh($"流水线已触发，运行编号[{run.Id}]", 1);
        }
        catch (Exception ex)
        {
            return Json(500, ex.Message);
        }
    }

    /// <summary>获取指定部署集下的可用部署节点（JSON），供前端级联下拉使用</summary>
    /// <param name="deployId">部署集编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult GetDeployNodes(Int32 deployId)
    {
        var nodes = AppDeployNode.FindAllByDeployId(deployId);
        var data = nodes.Where(e => e.Enable).Select(e => new { e.Id, name = $"{e.NodeName} ({e.IP})" });
        return Json(0, null, data);
    }

    /// <summary>获取指定部署集对应的项目编号（JSON），供前端级联使用</summary>
    /// <param name="deployId">部署集编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Detail)]
    public ActionResult GetDeployProject(Int32 deployId)
    {
        var deploy = AppDeploy.FindById(deployId);
        return Json(0, null, deploy?.ProjectId ?? 0);
    }
}
