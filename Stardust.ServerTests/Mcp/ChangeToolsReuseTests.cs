using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// stardust_now_changes 必须复用 MVC/API 已有查询。
/// 部署历史、配置历史走 Biz Search；流水线运行与列表控制器一样用 FindAll + CreateTime。
/// 纯源码契约，不连数据库。
/// </summary>
public class ChangeToolsReuseTests
{
    private static String ToolSource() => SourceRoot.ReadSource("Stardust.Mcp/Tools/Now/ChangeTools.cs");

    [Fact(DisplayName = "变更时间线复用 MVC/API 已有查询")]
    public void ChangesTool_UsesExistingSearchAndProjectFinders()
    {
        var source = ToolSource();

        Assert.Contains("AppDeploy.FindAllByProjectId(ctx.ProjectId)", source);
        Assert.Contains("AppConfig.FindAllByProjectId(ctx.ProjectId)", source);
        Assert.Contains("AppPipeline.FindAllByDeployId(deploy.Id)", source);

        // Biz：Search(deployId, nodeId, action, start, end, key, page)。生成重载多一个 Boolean? success，这里不能调到那一版。
        Assert.Contains("AppDeployHistory.Search(deploy.Id, -1, null, start, DateTime.MinValue, null, Top(\"Id\"))", source);
        // Biz：Search(configId, action, success, start, end, key, page)。
        Assert.Contains("ConfigHistory.Search(config.Id, null, null, start, DateTime.MinValue, null, Top(\"CreateTime\"))", source);
        // MVC 与 WebApi 的 AppPipelineRunController 都是 FindAll + CreateTime，不调用按 UpdateTime 过滤的 Search。
        Assert.Contains("AppPipelineRun.FindAll(", source);
        Assert.Contains("AppPipelineRun._.PipelineId == pipelineId", source);
        Assert.Contains("AppPipelineRun._.CreateTime.Between(start, DateTime.MinValue)", source);
        Assert.DoesNotContain("AppPipelineRun.Search", source);
        Assert.DoesNotContain("UpdateTime", source);

        Assert.DoesNotContain("AppDeployHistory.FindAll", source);
        Assert.DoesNotContain("ConfigHistory.FindAll", source);
        Assert.DoesNotContain("AppPipeline.FindAll()", source);
        Assert.DoesNotContain("AppDeploy.FindAll(", source);
        Assert.DoesNotContain("AppConfig.FindAll(", source);
    }
}
