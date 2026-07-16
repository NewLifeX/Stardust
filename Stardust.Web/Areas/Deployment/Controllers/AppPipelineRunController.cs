using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using Stardust.Models;
using Stardust.Web.Services;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线运行。每次 webhook 触发或手动触发，都会产生一条运行记录，记录状态、关联步骤、TraceId 等信息</summary>
[Menu(40, false)]
[DeploymentArea]
public class AppPipelineRunController : EntityController<AppPipelineRun>
{
    private readonly PipelineService _pipelineService;

    static AppPipelineRunController()
    {
        // 列表字段：去掉敏感字段
        ListFields.RemoveField("Id", "TraceId", "TriggerSource");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.AddListField("Reprocess", null, "Status");
        ListFields.TraceUrl("TraceId");

        // 步骤列：跳转到该运行的步骤记录列表
        {
            var df = ListFields.AddListField("Steps", "UpdateUserId");
            df.DisplayName = "步骤";
            df.Url = "/Deployment/AppPipelineStep?runId={Id}";
        }

        // 设置操作按钮
        {
            var df = ListFields.GetField("Reprocess") as ListField;
            df.DisplayName = "重跑";
            df.Description = "基于本次运行重新触发流水线";
            df.Url = "/Deployment/AppPipelineRun/Reprocess?id={Id}";
            df.DataAction = "action";
        }
    }

    public AppPipelineRunController(PipelineService pipelineService)
    {
        _pipelineService = pipelineService;
    }

    /// <summary>高级搜索</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppPipelineRun> Search(Pager p)
    {
        var pipelineId = p["pipelineId"].ToInt(-1);
        var status = p["status"].ToInt(-1);
        return AppPipelineRun.FindAll(
            (pipelineId < 0 ? null : AppPipelineRun._.PipelineId == pipelineId) &
            (status < 0 ? null : AppPipelineRun._.Status == status) &
            AppPipelineRun._.CreateTime.Between(p["dtStart"].ToDateTime(), p["dtEnd"].ToDateTime()),
            p);
    }

    /// <summary>详情页：实体已就绪后注入客户端轮询脚本</summary>
    /// <param name="id">运行编号</param>
    /// <returns></returns>
    public ActionResult Detail(Int64 id)
    {
        var entity = AppPipelineRun.FindById(id);
        if (entity == null) return Json(500, "运行记录不存在");

        // 注入前端轮询脚本：状态为 Pending/Building/Deploying 时每 5 秒自动刷新一次
        ViewBag.ClientScript = @"
<script>
(function(){
  var status = " + (Int32)entity.Status + @";
  // PipelineStatus: 1=Pending, 2=Building, 3=UploadSucceeded/Deploying
  if (status >= 1 && status <= 3) {
    setTimeout(function(){ location.reload(); }, 5000);
  }
})();
</script>";

        return View(entity);
    }

    /// <summary>重新处理（重试失败或被中断的运行）。委托 PipelineService.Reprocess</summary>
    /// <param name="id">运行编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Reprocess(Int64 id)
    {
        try
        {
            var run = await _pipelineService.Reprocess(id, UserHost);
            return JsonRefresh($"流水线已重新触发，运行编号[{run.Id}]", 1);
        }
        catch (Exception ex)
        {
            return Json(500, ex.Message);
        }
    }
}
