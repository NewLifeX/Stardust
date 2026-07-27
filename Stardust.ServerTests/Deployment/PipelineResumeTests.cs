using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Remoting.Models;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>流水线事件驱动续跑（NodeService.ProcessPipelineReplyAsync）单元测试。
/// 用反射调用私有方法：build-only 路径只调用静态实体方法，不依赖 DI 注入的实例字段，
/// 故用 GetUninitializedObject 创建实例；AutoDeploy 路径会经 DispatchDeployAsync 调 SendCommand，
/// 因此注入假的 NodeSessionManager，使 timeout=0 的 PublishAsync 立即返回 null、SendCommand 返回带命令 Id 的回包，
/// 从而部署步骤能真实记录 CommandId>0（不依赖真实节点会话）。</summary>
public class PipelineResumeTests
{
    #region 假的会话管理器基础设施
    /// <summary>最小服务提供器：仅满足 NodeSessionManager 构造时对 IHostApplicationLifetime 的依赖，
    /// 其余一律返回 null（可选服务）。</summary>
    private sealed class FakeServiceProvider : IServiceProvider
    {
        public Object GetService(Type serviceType) =>
            serviceType == typeof(IHostApplicationLifetime) ? new FakeHostApplicationLifetime() : null;
    }

    /// <summary>最小化的宿主生命周期，避免 NodeSessionManager 构造时空引用（ApplicationStopping 注册回调到不可取消令牌上，不会触发）。</summary>
    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;
        public void StopApplication() { }
    }
    #endregion

    /// <summary>创建 NodeService 实例并注入假的会话管理器。构造依赖 DI；
    /// 用 GetUninitializedObject 创建实例后，再把 _sessionManager 字段设为假的 NodeSessionManager。</summary>
    private static NodeService CreateService()
    {
        var svc = (NodeService)RuntimeHelpers.GetUninitializedObject(typeof(NodeService));
        var sm = new NodeSessionManager(new FakeServiceProvider());
        typeof(NodeService).GetField("_sessionManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, sm);
        return svc;
    }

    /// <summary>生成一个小正整数 Id（落在 Int32 范围内），避免 AppPipelineStep.RunId 列（int）存放不下
    /// AppPipelineRun.Id（Snowflake bigint）而被截断，导致实现内 FindById(step.RunId) 命中不到 run。
    /// 真实数据行 Id 均为 Snowflake（≈7e18），远大于 Int32 上限，故任意小正整数都不会与之冲突。</summary>
    private static Int32 NextSmallId() => Math.Abs(Guid.NewGuid().GetHashCode()) % 2000000000 + 1;

    /// <summary>反射调用私有方法 ProcessPipelineReplyAsync 并等待完成。</summary>
    private static async Task InvokeAsync(NodeService svc, NodeCommand cmd, CommandStatus status, String ip = "127.0.0.1")
    {
        var m = typeof(NodeService).GetMethod("ProcessPipelineReplyAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("找不到 ProcessPipelineReplyAsync");
        var model = new CommandReplyModel { Id = cmd.Id, Status = status };
        var task = (Task)m.Invoke(svc, new Object[] { cmd, model, ip })!;
        await task;
    }

    /// <summary>建一套干净的流水线数据：app + pipeline + run(Building) + buildStep(Running) + nodeCommand。</summary>
    private static (AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd)
        SetupBuild(String name, Boolean autoDeploy = false, String version = null)
    {
        var app = new AppDeploy { Name = name };
        app.Insert();

        var pipeline = new AppPipeline
        {
            Name = name,
            DeployId = app.Id,
            Branch = "main",
            BuildNodeId = 0,
            AutoDeploy = autoDeploy,
            Enable = true,
        };
        pipeline.Insert();

        var run = new AppPipelineRun
        {
            Id = NextSmallId(),
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

        if (version != null)
        {
            new AppDeployVersion { DeployId = app.Id, Version = version }.Insert();
        }

        return (app, pipeline, run, step, cmd);
    }

    /// <summary>建一套 AutoDeploy 流水线数据（含可选的有效部署节点）。
    /// withNode=true 时创建启用节点 Node 与 AppDeployNode，并把 DeployNodeIds 指向该 AppDeployNode.Id（实现按逗号拆分，单值即可命中）。</summary>
    private static (AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd, AppDeployNode dn, Node node)
        SetupAutoDeploy(String name, String version, Boolean withNode)
    {
        var app = new AppDeploy { Name = name };
        app.Insert();

        Node node = null;
        AppDeployNode dn = null;
        var pipeline = new AppPipeline
        {
            Name = name,
            DeployId = app.Id,
            Branch = "main",
            BuildNodeId = 0,
            AutoDeploy = true,
            Enable = true,
        };
        if (withNode)
        {
            node = new Node
            {
                Name = "n" + Guid.NewGuid().ToString("N")[..6],
                Code = "c" + Guid.NewGuid().ToString("N")[..6],
                Enable = true,
            };
            node.Insert();
            dn = new AppDeployNode
            {
                DeployId = app.Id,
                NodeId = node.ID,
                Enable = true,
                DeployName = "d" + Guid.NewGuid().ToString("N")[..4],
            };
            dn.Insert();
            pipeline.DeployNodeIds = dn.Id.ToString();
        }
        else
        {
            pipeline.DeployNodeIds = ""; // 无可用部署节点
        }
        pipeline.Insert();

        var run = new AppPipelineRun
        {
            Id = NextSmallId(),
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

        new AppDeployVersion { DeployId = app.Id, Version = version }.Insert();

        return (app, pipeline, run, step, cmd, dn, node);
    }

    /// <summary>清理 build-only 测试产生的数据，避免污染共享测试库。</summary>
    private static void Cleanup((AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd) data)
    {
        // 删除编译成功时写入的部署历史（Remark=编译完成）
        foreach (var h in AppDeployHistory.FindAllByDeployId(data.app.Id))
        {
            if (h.Remark == "编译完成") h.Delete();
        }
        data.step?.Delete();
        data.run?.Delete();
        data.pipeline?.Delete();
        data.app?.Delete();
        data.cmd?.Delete();
    }

    /// <summary>清理 AutoDeploy 测试产生的数据（步骤/命令/部署节点/节点/历史/版本）。DeployId 唯一归属本测试，删除其历史安全。</summary>
    private static void CleanupAutoDeploy((AppDeploy app, AppPipeline pipeline, AppPipelineRun run, AppPipelineStep step, NodeCommand cmd, AppDeployNode dn, Node node) data)
    {
        foreach (var h in AppDeployHistory.FindAllByDeployId(data.app.Id)) h.Delete();

        foreach (var s in AppPipelineStep.FindAll(AppPipelineStep._.RunId == data.run.Id))
        {
            if (s.CommandId > 0)
            {
                var dc = NodeCommand.FindById(s.CommandId);
                dc?.Delete();
            }
            s.Delete();
        }

        data.dn?.Delete();
        data.node?.Delete();
        data.run?.Delete();
        data.pipeline?.Delete();
        foreach (var v in AppDeployVersion.FindAllByDeployId(data.app.Id)) v.Delete();
        data.app?.Delete();
        data.cmd?.Delete();
    }

    [Fact]
    public async Task Build_Success_NoAutoDeploy_使用版本并置Success()
    {
        var data = SetupBuild("res-bok-" + Guid.NewGuid().ToString("N")[..6], autoDeploy: false, version: "v20260724-000000");
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);

            var step = AppPipelineStep.FindById(data.step.Id);
            var run = AppPipelineRun.FindById(data.run.Id);
            var app = AppDeploy.FindById(data.app.Id);

            Assert.Equal("Success", step.Status);
            Assert.Equal(PipelineStatus.Success, run.Status);
            Assert.Equal("v20260724-000000", app.Version); // 使用版本已生效，等价于 Web「使用版本」按钮
        }
        finally
        {
            Cleanup(data);
        }
    }

    [Fact]
    public async Task Build_Success_NoVersion_仍置Success()
    {
        var data = SetupBuild("res-bnv-" + Guid.NewGuid().ToString("N")[..6], autoDeploy: false, version: null);
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);

            var step = AppPipelineStep.FindById(data.step.Id);
            var run = AppPipelineRun.FindById(data.run.Id);

            Assert.Equal("Success", step.Status);
            Assert.Equal(PipelineStatus.Success, run.Status);
        }
        finally
        {
            Cleanup(data);
        }
    }

    [Fact]
    public async Task Build_Failed_置Failed()
    {
        var data = SetupBuild("res-bfa-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.错误);

            var step = AppPipelineStep.FindById(data.step.Id);
            var run = AppPipelineRun.FindById(data.run.Id);

            Assert.Equal("Failed", step.Status);
            Assert.Equal(PipelineStatus.Failed, run.Status);
        }
        finally
        {
            Cleanup(data);
        }
    }

    [Fact]
    public async Task CancelledRun_不续跑()
    {
        var data = SetupBuild("res-can-" + Guid.NewGuid().ToString("N")[..6]);
        data.run.Status = PipelineStatus.Cancelled;
        data.run.Update();
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);

            var step = AppPipelineStep.FindById(data.step.Id);
            var run = AppPipelineRun.FindById(data.run.Id);

            Assert.Equal("Running", step.Status); // 已取消的 run 不再续跑
            Assert.Equal(PipelineStatus.Cancelled, run.Status);
        }
        finally
        {
            Cleanup(data);
        }
    }

    [Fact]
    public async Task Build_Success_AutoDeploy_重入幂等_不重复下发部署()
    {
        // 修复重入假阳性：构造含有效部署节点的 AutoDeploy run，模拟回包网络重试（连续两次 Build 成功回包）。
        var data = SetupAutoDeploy("res-re-" + Guid.NewGuid().ToString("N")[..6], "vRR", withNode: true);
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成); // 第二次应幂等返回，不重复下发

            var app = AppDeploy.FindById(data.app.Id);
            var run = AppPipelineRun.FindById(data.run.Id);
            var deploySteps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id & AppPipelineStep._.StepType == "Deploy");
            var buildSteps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id & AppPipelineStep._.StepType == "Build");

            Assert.Single(deploySteps); // 续跑只发生一次，不重复创建部署步骤
            Assert.True(deploySteps[0].CommandId > 0); // 且真实下发了部署命令（记录 CommandId）
            Assert.Equal("vRR", app.Version); // 使用版本只被设置一次
            Assert.Single(buildSteps); // Build 步骤仍只有一个
            Assert.Equal("Success", buildSteps[0].Status);
        }
        finally
        {
            CleanupAutoDeploy(data);
        }
    }

    [Fact]
    public async Task Build_Success_AutoDeploy_触发部署并建Running步骤()
    {
        // 覆盖 AutoDeploy 路径：编译成功回包应触发 DispatchDeployAsync，为节点建 Running 部署步骤并记录 CommandId>0。
        var data = SetupAutoDeploy("res-au-" + Guid.NewGuid().ToString("N")[..6], "vAU", withNode: true);
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);

            var run = AppPipelineRun.FindById(data.run.Id);
            var deployStep = AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id & AppPipelineStep._.StepType == "Deploy").FirstOrDefault();

            Assert.NotNull(deployStep);
            Assert.Equal("Running", deployStep.Status); // 部署步骤已下发，等待节点回包
            Assert.True(deployStep.CommandId > 0); // 已记录各自命令 Id
            Assert.Equal(PipelineStatus.Deploying, run.Status); // 进入部署中，待回包完成
        }
        finally
        {
            CleanupAutoDeploy(data);
        }
    }

    [Fact]
    public async Task Build_Success_AutoDeploy_无部署节点_不卡Deploying()
    {
        // 覆盖严重问题1：AutoDeploy 但无可用部署节点时，run 应直接置 Success（不卡 Deploying，DeployFinishedTime 有值）。
        var data = SetupAutoDeploy("res-no-" + Guid.NewGuid().ToString("N")[..6], "vNO", withNode: false);
        try
        {
            await InvokeAsync(CreateService(), data.cmd, CommandStatus.已完成);

            var run = AppPipelineRun.FindById(data.run.Id);
            var deploySteps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id & AppPipelineStep._.StepType == "Deploy");

            Assert.Equal(PipelineStatus.Success, run.Status); // 无可用节点也直接完成，不卡 Deploying
            Assert.NotEqual(default(DateTime), run.DeployFinishedTime);
            Assert.Empty(deploySteps); // 未下发任何部署步骤
        }
        finally
        {
            CleanupAutoDeploy(data);
        }
    }

    [Fact]
    public async Task Deploy_全部Success或Skipped_置Success()
    {
        var data = SetupBuild("res-dep-" + Guid.NewGuid().ToString("N")[..6]);
        data.run.Status = PipelineStatus.Deploying;
        data.run.Update();

        var cmd2 = new NodeCommand { NodeID = 1, Status = CommandStatus.处理中 };
        cmd2.Insert();

        var stepOk = new AppPipelineStep { RunId = data.run.Id, StepType = "Deploy", StepIndex = 0, NodeId = 1, Status = "Success", CreateTime = DateTime.Now };
        stepOk.Insert();
        var stepSkip = new AppPipelineStep { RunId = data.run.Id, StepType = "Deploy", StepIndex = 1, NodeId = 1, Status = "Skipped", CreateTime = DateTime.Now };
        stepSkip.Insert();
        var stepRun = new AppPipelineStep { RunId = data.run.Id, StepType = "Deploy", StepIndex = 2, NodeId = 1, CommandId = cmd2.Id, Status = "Running", StartedTime = DateTime.Now, CreateTime = DateTime.Now };
        stepRun.Insert();

        try
        {
            await InvokeAsync(CreateService(), cmd2, CommandStatus.已完成);

            var stepRun2 = AppPipelineStep.FindById(stepRun.Id);
            var run = AppPipelineRun.FindById(data.run.Id);

            Assert.Equal("Success", stepRun2.Status);
            Assert.Equal(PipelineStatus.Success, run.Status); // Skipped 不阻断完成（修复点验证）
        }
        finally
        {
            stepRun?.Delete();
            stepSkip?.Delete();
            stepOk?.Delete();
            cmd2?.Delete();
            Cleanup(data);
        }
    }
}
