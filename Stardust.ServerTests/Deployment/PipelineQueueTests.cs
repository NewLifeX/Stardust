extern alias web;

using System;
using System.Reflection;
using System.Threading.Tasks;
using NewLife.Remoting.Models;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>流水线排队、超时自愈与命令过期联动测试。覆盖 PipelineService 的静态辅助与数据层联动，
/// 不依赖真实编译节点。</summary>
public class PipelineQueueTests
{
    private static web::Stardust.Web.Services.PipelineService CreateService() => new(null!, null!);

    private static Object InvokeStatic(String name, params Object[] args)
    {
        var m = typeof(web::Stardust.Web.Services.PipelineService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"找不到 {name}");
        return m.Invoke(null, args);
    }

    [Theory]
    [InlineData("tag:v*", "v1.0.0", true)]
    [InlineData("tag:v*", "v2", true)]
    [InlineData("tag:v1.0.0", "v1.0.0", true)]
    [InlineData("tag:v1.0.0", "v1.0.1", false)]
    [InlineData("main", "v1.0.0", false)] // 未声明 tag: 前缀不响应标签
    [InlineData("", "v1.0.0", false)]
    public void MatchTag_OnlyWhenTagPrefixConfigured(String pattern, String tag, Boolean expected)
    {
        var rs = (Boolean)InvokeStatic("MatchTag", pattern, tag)!;
        Assert.Equal(expected, rs);
    }

    [Theory]
    [InlineData("release/*", "main", null, "main")] // 通配分支回退部署集分支
    [InlineData("tag:v*", "main", null, "main")] // 标签规则也回退部署集分支
    [InlineData("dev", "main", null, "dev")] // 具体分支优先
    [InlineData("dev", "main", "release/1.0", "release/1.0")] // 手动指定分支优先
    [InlineData("dev", "main", "refs/heads/release/1.0", "release/1.0")] // 兼容完整引用
    [InlineData("dev", "main", "refs/tags/v1", "main")] // 标签引用按部署集分支构建
    public void ResolveBuildBranch_PrefersConcreteTrigger(String pipelineBranch, String appBranch, String? requested, String expected)
    {
        var pipeline = new AppPipeline { Branch = pipelineBranch };
        var app = new AppDeploy { Branch = appBranch };

        var rs = (String?)InvokeStatic("ResolveBuildBranch", pipeline, app, requested);
        Assert.Equal(expected, rs);
    }

    [Fact(DisplayName = "手动触发只创建排队记录，并记录指定分支与提交")]
    public async Task Trigger_CreatesPendingRunWithBranchAndCommit()
    {
        var name = "q-trig-" + Guid.NewGuid().ToString("N")[..6];
        var app = new AppDeploy { Name = name };
        app.Insert();
        var pipeline = new AppPipeline { Name = name, DeployId = app.Id, Branch = "release/*", BuildNodeId = 1001, Enable = true };
        pipeline.Insert();
        var bn = new AppBuildNode { DeployId = app.Id, NodeId = 1001, Enable = true };
        bn.Insert();

        AppPipelineRun? run = null;
        try
        {
            run = await CreateService().Trigger(pipeline.Id, "127.0.0.1", "release/1.0", "abc123");

            Assert.Equal(PipelineStatus.Pending, run.Status);
            Assert.Equal("release/1.0", run.Branch);
            Assert.Equal("abc123", run.CommitId);
            Assert.Equal("manual", run.TriggerSource);
        }
        finally
        {
            if (run != null) AppPipelineRun.FindById(run.Id)?.Delete();
            bn.Delete();
            pipeline.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "构建超时的运行会被标记失败，Running 步骤同步失败")]
    public void RecoverStale_BuildTimeout_MarksRunAndStepsFailed()
    {
        var data = SetupBuild("q-stale-" + Guid.NewGuid().ToString("N")[..6]);
        data.run.BuildStartedTime = DateTime.Now.AddHours(-2);
        data.run.CreateTime = DateTime.Now.AddHours(-2);
        data.run.Update();

        try
        {
            var removed = CreateService().RecoverStale(DateTime.Now);

            var run = AppPipelineRun.FindById(data.run.Id);
            var step = AppPipelineStep.FindById(data.step.Id);
            Assert.True(removed >= 1);
            Assert.Equal(PipelineStatus.Failed, run.Status);
            Assert.Equal("Failed", step.Status);
            Assert.False(step.FinishedTime == default);
        }
        finally
        {
            Cleanup(data);
        }
    }

    [Fact(DisplayName = "命令过期时联动失败流水线步骤与运行，避免永久停在 Building")]
    public void CancelExpired_FailsLinkedPipelineStepAndRun()
    {
        var data = SetupBuild("q-exp-" + Guid.NewGuid().ToString("N")[..6]);
        data.cmd.Expire = DateTime.Now.AddMinutes(-1);
        data.cmd.Update();

        try
        {
            NodeCommand.CancelExpired(DateTime.Now);

            var run = AppPipelineRun.FindById(data.run.Id);
            var step = AppPipelineStep.FindById(data.step.Id);
            Assert.Equal("Failed", step.Status);
            Assert.Equal(PipelineStatus.Failed, run.Status);
        }
        finally
        {
            Cleanup(data);
        }
    }

    private static (AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd) SetupBuild(String name)
    {
        var app = new AppDeploy { Name = name };
        app.Insert();

        var pipeline = new AppPipeline
        {
            Name = name,
            DeployId = app.Id,
            Branch = "main",
            BuildNodeId = 0,
            Enable = true,
        };
        pipeline.Insert();

        var run = new AppPipelineRun
        {
            Id = Math.Abs(Guid.NewGuid().GetHashCode()) % 2000000000 + 1,
            PipelineId = pipeline.Id,
            Status = PipelineStatus.Building,
            CreateTime = DateTime.Now,
        };
        run.Insert();

        var cmd = new NodeCommand { NodeID = 1, Status = CommandStatus.处理中 };
        cmd.Insert();

        var step = new AppPipelineStep
        {
            RunId = run.Id,
            StepType = "Build",
            StepIndex = 0,
            NodeId = 1,
            CommandId = cmd.Id,
            Status = "Running",
            StartedTime = DateTime.Now,
            CreateTime = DateTime.Now,
        };
        step.Insert();

        return (app, pipeline, run, step, cmd);
    }

    private static void Cleanup((AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd) data)
    {
        foreach (var h in AppDeployHistory.FindAllByDeployId(data.app.Id)) h.Delete();
        data.step?.Delete();
        data.run?.Delete();
        data.pipeline?.Delete();
        data.app?.Delete();
        data.cmd?.Delete();
    }
}
