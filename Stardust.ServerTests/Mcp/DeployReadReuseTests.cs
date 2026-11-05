using Xunit;

namespace ServerTest.Mcp;

/// <summary>部署读工具复用 MVC/API 已有查询，不在 MCP 里另写 FindAll 条件。</summary>
public class DeployReadReuseTests
{
    private static String ToolBody(String typeName)
    {
        var source = global::ServerTest.SourceRoot.ReadSource("Stardust.Mcp/Tools/Deploy/DeployTools.cs");
        var start = source.IndexOf($"public class {typeName}", StringComparison.Ordinal);
        Assert.True(start >= 0, typeName);
        var next = source.IndexOf("public class ", start + 1, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }

    [Fact]
    public void DeployStatus_UsesFindAllByDeployId()
    {
        var body = ToolBody("DeployStatusTool");
        Assert.Contains("AppDeployNode.FindAllByDeployId(deploy.Id)", body);
        Assert.DoesNotContain("AppDeployNode.FindAll(", body);
        Assert.DoesNotContain("FindAllByAppId", body);
    }

    [Fact]
    public void DeployVersions_UsesControllerSearch()
    {
        var body = ToolBody("DeployVersionsTool");
        Assert.Contains("AppDeployVersion.Search(deploy.Id, null, null, DateTime.MinValue, DateTime.MinValue, null, null)", body);
        Assert.DoesNotContain("AppDeployVersion.FindAll(", body);
    }

    [Fact]
    public void DeployHistory_UsesControllerSearchWithoutMemoryPaging()
    {
        var body = ToolBody("DeployHistoryTool");
        Assert.Contains("AppDeployHistory.Search(deploy.Id, -1, null, Since(args), DateTime.MinValue, null, page)", body);
        Assert.Contains("RetrieveTotalCount = true", body);
        Assert.DoesNotContain("AppDeployHistory.FindAll(", body);
        Assert.DoesNotContain(".Skip(", body);
        Assert.DoesNotContain(".Take(", body);
    }

    [Fact]
    public void PipelineRunDetail_UsesFindAllByRunId()
    {
        var body = ToolBody("PipelineRunDetailTool");
        Assert.Contains("AppPipelineStep.FindAllByRunId(run.Id)", body);
        Assert.DoesNotContain("AppPipelineStep.FindAll(", body);
    }

    [Fact]
    public void DeployRelease_SelectsNodesByDeployId_AndKeepsVersionSwitchWithoutPublish()
    {
        var body = ToolBody("DeployReleaseTool");
        Assert.Contains("AppDeployNode.FindAllByDeployId(deploy.Id)", body);
        Assert.DoesNotContain("AppDeployNode.FindAll(", body);
        Assert.Contains("AppDeployVersion.FindByDeployIdAndVersion(deploy.Id, version)", body);
        Assert.DoesNotContain("AppDeployVersion.FindAll(", body);
        Assert.Contains("await ops.UseVersionAsync(ver.Id, ctx.CallerIp, false);", body);
        Assert.DoesNotContain("publishEnabledNodes", body);
        Assert.Contains("ControlDeployAsync(deploy.Id, node.Id, \"install\", ctx.CallerIp)", body);
    }

    [Fact]
    public void HistoryControllers_ShareTheSameSearch()
    {
        const String call = "return AppDeployHistory.Search(appId, nodeId, null, start, end, p[\"Q\"], p);";
        var mvc = global::ServerTest.SourceRoot.ReadSource("Stardust.Web/Areas/Deployment/Controllers/AppDeployHistoryController.cs");
        var api = global::ServerTest.SourceRoot.ReadSource("Stardust.WebApi/Areas/Deployment/Controllers/AppDeployHistoryController.cs");
        Assert.Contains(call, mvc);
        Assert.Contains(call, api);
    }

    [Fact]
    public void PipelineRunDetail_ApiAlreadyUsesFindAllByRunId_MvcDetailDoesNotQuerySteps()
    {
        var api = global::ServerTest.SourceRoot.ReadSource("Stardust.WebApi/Areas/Deployment/Controllers/AppPipelineRunController.cs");
        var mvc = global::ServerTest.SourceRoot.ReadSource("Stardust.Web/Areas/Deployment/Controllers/AppPipelineRunController.cs");
        Assert.Contains("AppPipelineStep.FindAllByRunId(id)", api);
        Assert.Contains("return View(entity);", mvc);
        Assert.DoesNotContain("AppPipelineStep.", mvc);
    }
}
