using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>流水线步骤。一次运行的每一个阶段（拉代码/编译/打包/上传/部署等）都对应一条步骤记录，承载阶段级状态、输出与耗时。只读子表视图</summary>
[Menu(35, false)]
[DeploymentArea]
public class AppPipelineStepController : ReadOnlyEntityController<AppPipelineStep>
{
    static AppPipelineStepController()
    {
        // 步骤记录无业务主键概念，移除主键/创建时间列减少噪音
        ListFields.RemoveField("Id", "CreateTime");
    }

    /// <summary>高级搜索。按所属运行编号过滤，按步骤顺序升序展示</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppPipelineStep> Search(Pager p)
    {
        var runId = p["runId"].ToLong(-1);
        return AppPipelineStep.FindAll(
            runId < 0 ? null : AppPipelineStep._.RunId == runId,
            p);
    }
}
