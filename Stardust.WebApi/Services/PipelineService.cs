using System.Collections.Concurrent;
using System.Linq;
using NewLife;
using NewLife.Log;
using Stardust.Data.Deployment;
using Stardust.Models;
using Stardust.Server;
using Stardust.Data.Platform;
using XCode;

namespace Stardust.Web.Services;

/// <summary>流水线编排服务。管理流水线运行的完整生命周期：排队 → 触发编译 → 查版本 → 部署。
/// 同时提供 webhook 入口，让 git push 与手动触发共用同一份排队与编排，严格复用 DeployService.Compile/Control</summary>
public class PipelineService
{
    private readonly DeployService _deployService;
    private readonly ITracer _tracer;

    /// <summary>防抖时间窗口。同一 commit 在窗口内只允许触发一次</summary>
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(5);

    /// <summary>运行中的 CancellationTokenSource 字典（按 runId 索引）。用于支持本进程内的流水线取消</summary>
    private readonly ConcurrentDictionary<Int64, CancellationTokenSource> _runningCts = new();

    public PipelineService(DeployService deployService, ITracer tracer)
    {
        _deployService = deployService;
        _tracer = tracer;
    }

    #region 手动触发
    /// <summary>手动触发流水线运行。仅创建排队记录，由 PipelineRunner 在该流水线空闲时派发</summary>
    /// <param name="pipelineId">流水线编号</param>
    /// <param name="userHost">触发者 IP</param>
    /// <param name="branch">构建分支。为空时按流水线配置推导</param>
    /// <param name="commit">构建提交。指定后构建该提交，为空时构建分支最新提交</param>
    /// <returns>新创建的排队运行记录</returns>
    public Task<AppPipelineRun> Trigger(Int32 pipelineId, String userHost, String? branch = null, String? commit = null)
    {
        var (pipeline, app, _) = GetContext(pipelineId);

        CheckQueueLength(pipeline.Id);

        var run = new AppPipelineRun
        {
            PipelineId = pipeline.Id,
            Status = PipelineStatus.Pending,
            TriggerSource = "manual",
            Branch = ResolveBuildBranch(pipeline, app, branch),
            CommitId = commit,
            BuildNodeId = pipeline.BuildNodeId,
        };
        run.Insert();

        return Task.FromResult(run);
    }

    /// <summary>重新处理流水线运行（重试失败或被中断的运行）。基于原运行创建新记录并排队</summary>
    /// <param name="runId">原运行记录编号</param>
    /// <param name="userHost">触发者 IP</param>
    /// <returns>新创建的排队运行记录</returns>
    public Task<AppPipelineRun> Reprocess(Int32 runId, String userHost)
    {
        var src = ProjectScopeRules.FilterVisible(AppPipelineRun.FindById(runId));
        if (src == null) throw new ArgumentNullException(nameof(runId), L.T("Run record does not exist."));

        var (pipeline, app, _) = GetContext(src.PipelineId);

        CheckQueueLength(pipeline.Id);

        var run = new AppPipelineRun
        {
            PipelineId = src.PipelineId,
            Status = PipelineStatus.Pending,
            TriggerSource = "reprocess",
            CommitId = src.CommitId,
            CommitMessage = src.CommitMessage,
            CommitAuthor = src.CommitAuthor,
            CommitTime = src.CommitTime == default ? DateTime.Now : src.CommitTime,
            Branch = ResolveBuildBranch(pipeline, app, src.Branch),
            BuildNodeId = src.BuildNodeId > 0 ? src.BuildNodeId : pipeline.BuildNodeId,
        };
        run.Insert();

        return Task.FromResult(run);
    }
    #endregion

    #region Webhook 入口
    /// <summary>处理 git push webhook 入口。多平台负载先经解析器转换为规范模型，再执行分支匹配、防抖与运行创建。
    /// 后续编排与具体 git 平台无关，新增平台只需在 WebhookParserRegistry 注册一个解析器</summary>
    /// <param name="token">webhook 鉴权 token（AppPipeline.Token）</param>
    /// <param name="request">原始请求（请求体、平台事件头、签名）</param>
    /// <returns>处理结果对象，形如 {result:"queued", runId, status}</returns>
    public Task<Object> HandleWebhookAsync(String token, WebhookRequest request)
    {
        if (token.IsNullOrEmpty()) return Task.FromResult<Object>(new { result = "error", reason = "missing token" });
        if (request == null || request.Body.IsNullOrEmpty()) return Task.FromResult<Object>(new { result = "error", reason = "empty payload" });

        var pipeline = AppPipeline.FindByToken(token);
        if (pipeline == null) return Task.FromResult<Object>(new { result = "error", reason = "invalid token" });
        if (!pipeline.Enable) return Task.FromResult<Object>(new { result = "ignored", reason = "pipeline disabled" });

        // 签名校验（可选，配置了 Secret 才校验）
        if (!pipeline.Secret.IsNullOrEmpty() && !WebhookSignature.Verify(pipeline.Secret, request))
            return Task.FromResult<Object>(new { result = "error", reason = "invalid signature" });

        // 平台适配：不同 git 平台的负载在此收敛为统一模型，后续流程不再关心来源平台
        var evt = WebhookParserRegistry.Parse(request);
        if (evt == null) return Task.FromResult<Object>(new { result = "error", reason = "invalid payload" });
        if (evt.Branch.IsNullOrEmpty()) return Task.FromResult<Object>(new { result = "ignored", reason = "no branch" });
        if (evt.Deleted) return Task.FromResult<Object>(new { result = "ignored", reason = "ref deleted" });

        // 分支/标签匹配：标签推送需在流水线分支配置 tag: 前缀（如 tag:v*）才触发
        if (evt.IsTag)
        {
            var tag = evt.Ref.StartsWithIgnoreCase("refs/tags/") ? evt.Ref[10..] : evt.Branch;
            if (!MatchTag(pipeline.Branch, tag)) return Task.FromResult<Object>(new { result = "ignored", reason = "tag mismatch" });
        }
        else if (!MatchBranch(pipeline.Branch, evt.Branch))
        {
            return Task.FromResult<Object>(new { result = "ignored", reason = "branch mismatch" });
        }

        // 防抖：同 pipeline + 同 commit 在 5 分钟内有进行中或已成功的 run 则跳过
        if (!evt.CommitId.IsNullOrEmpty() && IsDuplicate(pipeline.Id, evt.CommitId, DedupWindow))
            return Task.FromResult<Object>(new { result = "skipped", reason = "duplicate" });

        // 关联实体校验，避免创建无法执行的 run
        var app = ProjectScopeRules.FilterVisible(AppDeploy.FindById(pipeline.DeployId));
        if (app == null) return Task.FromResult<Object>(new { result = "error", reason = "deploy not found" });

        var buildNode = ProjectScopeRules.FilterVisible(AppBuildNode.FindAllByNodeId(pipeline.BuildNodeId)).FirstOrDefault(e => e.DeployId == pipeline.DeployId);
        if (buildNode == null || !buildNode.Enable) return Task.FromResult<Object>(new { result = "error", reason = "build node unavailable" });

        // 队列深度保护，避免推送风暴无限堆积
        var max = StarServerSetting.Current.PipelineQueueLength;
        if (max > 0)
        {
            var count = AppPipelineRun.FindCount(AppPipelineRun._.PipelineId == pipeline.Id & AppPipelineRun._.Status == (Int32)PipelineStatus.Pending);
            if (count >= max) return Task.FromResult<Object>(new { result = "rejected", reason = "queue full" });
        }

        // 创建 run（Pending），由 PipelineRunner 串行派发
        var run = new AppPipelineRun
        {
            PipelineId = pipeline.Id,
            Status = PipelineStatus.Pending,
            TriggerSource = "webhook",
            CommitId = evt.CommitId,
            CommitMessage = evt.CommitMessage,
            CommitAuthor = evt.CommitAuthor,
            CommitTime = evt.CommitTime == default ? DateTime.Now : evt.CommitTime,
            Branch = ResolveBuildBranch(pipeline, app, evt.IsTag ? null : evt.Branch),
            BuildNodeId = pipeline.BuildNodeId,
        };
        run.Insert();

        return Task.FromResult<Object>(new { result = "queued", runId = run.Id, status = run.Status.ToString() });
    }
    #endregion

    #region 取消
    /// <summary>取消运行中的流水线。校验状态后置为 Cancelled，并触发 CTS 中断后续步骤</summary>
    /// <param name="runId">运行记录编号</param>
    /// <param name="userHost">取消者 IP</param>
    /// <returns>是否取消成功（run 不存在或已终态返回 false）</returns>
    public Boolean Cancel(Int32 runId, String userHost)
    {
        var run = ProjectScopeRules.FilterVisible(AppPipelineRun.FindById(runId));
        if (run == null) throw new ArgumentNullException(nameof(runId), L.T("Run record does not exist."));

        // 仅允许取消未到终态的 run
        if (run.Status is PipelineStatus.Success or PipelineStatus.Failed or PipelineStatus.Cancelled)
            return false;

        // 触发 CTS 中断 RunAsync 中的后续步骤
        if (_runningCts.TryRemove(runId, out var cts))
        {
            try { cts.Cancel(); } catch { /* 忽略取消异常 */ }
            cts.Dispose();
        }

        // 标记为已取消
        run.Status = PipelineStatus.Cancelled;
        run.Remark = L.T("Cancelled by user (IP: {0})", userHost);
        if (run.BuildStartedTime != default && run.BuildFinishedTime == default) run.BuildFinishedTime = DateTime.Now;
        if (run.DeployStartedTime != default && run.DeployFinishedTime == default) run.DeployFinishedTime = DateTime.Now;
        run.Update();

        // 把还在 Running 的 step 标记为 Skipped
        var steps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == runId & AppPipelineStep._.Status == "Running");
        foreach (var s in steps)
        {
            s.Status = "Skipped";
            s.Message = L.T("Run cancelled.");
            s.FinishedTime = DateTime.Now;
            s.Update();
        }

        return true;
    }
    #endregion

    #region 排队与自愈
    /// <summary>处理排队运行。由 PipelineRunner 周期调用：先清理超时运行，再按流水线串行派发 Pending。
    /// 同一流水线只允许一个活动运行；不同流水线可并行。以数据库条件更新抢占运行，适配多实例部署。</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次派发的运行数</returns>
    public Task<Int32> ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        // 关闭队列时不做任何后台处理，便于测试与运维临时停用
        if (!StarServerSetting.Current.PipelineQueueEnabled) return Task.FromResult(0);

        RecoverStale(DateTime.Now);

        var pending = AppPipelineRun.FindAll(AppPipelineRun._.Status == (Int32)PipelineStatus.Pending, AppPipelineRun._.Id.Asc(), null, 0, 50);
        var count = 0;
        foreach (var run in pending)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (run.PipelineId <= 0) continue;

            // 同流水线串行：已有活动运行则等待下一轮
            if (HasActiveRun(run.PipelineId)) continue;

            // 条件更新抢占：仅当仍为 Pending 时置为 Building，影响行数为 0 表示其它实例已处理
            var now = DateTime.Now;
            var rs = AppPipelineRun.Update(
                [nameof(AppPipelineRun.Status), nameof(AppPipelineRun.BuildStartedTime)],
                [(Int32)PipelineStatus.Building, now],
                [nameof(AppPipelineRun.Id), nameof(AppPipelineRun.Status)],
                [run.Id, (Int32)PipelineStatus.Pending]);
            if (rs == 0) continue;

            run.Status = PipelineStatus.Building;
            run.BuildStartedTime = now;
            try
            {
                var (pipeline, app, buildNode) = GetContext(run.PipelineId);
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runningCts[run.Id] = cts;
                _ = Task.Run(() => RunAsync(run, pipeline, app, buildNode, run.CreateIP, cts.Token), cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                run.Status = PipelineStatus.Failed;
                run.Remark = AppPipelineStep.CutMessage(ex.Message, 480);
                run.BuildFinishedTime = DateTime.Now;
                run.Update();
            }
        }

        return Task.FromResult(count);
    }

    /// <summary>清理卡住的运行。节点离线、构建超时或服务重启后，命令回包不会再到达，必须由服务端兜底置为失败。</summary>
    /// <param name="now">当前时间</param>
    /// <returns>清理的运行数</returns>
    public Int32 RecoverStale(DateTime now)
    {
        var set = StarServerSetting.Current;
        var count = 0;

        if (set.PipelineQueueTimeout > 0)
        {
            foreach (var run in AppPipelineRun.FindAll(AppPipelineRun._.Status == (Int32)PipelineStatus.Pending, AppPipelineRun._.Id.Asc(), null, 0, 100))
            {
                if (run.CreateTime.Year > 2000 && run.CreateTime < now.AddSeconds(-set.PipelineQueueTimeout))
                {
                    FailStaleRun(run, L.T("Pipeline queue timed out."));
                    count++;
                }
            }
        }

        if (set.PipelineBuildTimeout > 0)
        {
            foreach (var run in AppPipelineRun.FindAll(AppPipelineRun._.Status == (Int32)PipelineStatus.Building, AppPipelineRun._.Id.Asc(), null, 0, 100))
            {
                var start = run.BuildStartedTime.Year > 2000 ? run.BuildStartedTime : run.CreateTime;
                if (start.Year > 2000 && start < now.AddSeconds(-set.PipelineBuildTimeout))
                {
                    FailStaleRun(run, L.T("Pipeline build timed out."));
                    count++;
                }
            }
        }

        if (set.PipelineDeployTimeout > 0)
        {
            foreach (var run in AppPipelineRun.FindAll(AppPipelineRun._.Status == (Int32)PipelineStatus.Deploying, AppPipelineRun._.Id.Asc(), null, 0, 100))
            {
                var start = run.DeployStartedTime.Year > 2000 ? run.DeployStartedTime : run.UpdateTime;
                if (start.Year > 2000 && start < now.AddSeconds(-set.PipelineDeployTimeout))
                {
                    FailStaleRun(run, L.T("Pipeline deploy timed out."));
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>把卡住的运行及其仍在执行的步骤置为失败</summary>
    private static void FailStaleRun(AppPipelineRun run, String message)
    {
        run.Status = PipelineStatus.Failed;
        run.Remark = AppPipelineStep.CutMessage(message, 480);
        if (run.BuildStartedTime != default && run.BuildFinishedTime == default) run.BuildFinishedTime = DateTime.Now;
        if (run.DeployStartedTime != default && run.DeployFinishedTime == default) run.DeployFinishedTime = DateTime.Now;
        run.Update();

        foreach (var step in AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id & AppPipelineStep._.Status == "Running"))
        {
            step.Status = "Failed";
            step.Message = AppPipelineStep.CutMessage(message);
            step.FinishedTime = DateTime.Now;
            step.Update();
        }
    }

    /// <summary>是否存在同流水线的活动运行。Pending 不计入，避免队首阻塞后续派发</summary>
    private static Boolean HasActiveRun(Int32 pipelineId)
    {
        var exp = AppPipelineRun._.PipelineId == pipelineId &
            (AppPipelineRun._.Status == (Int32)PipelineStatus.Building | AppPipelineRun._.Status == (Int32)PipelineStatus.Deploying);
        return AppPipelineRun.FindAll(exp, null, null, 0, 1).Count > 0;
    }

    /// <summary>加载并校验流水线、部署集与编译节点</summary>
    private static (AppPipeline pipeline, AppDeploy app, AppBuildNode buildNode) GetContext(Int32 pipelineId)
    {
        var pipeline = ProjectScopeRules.FilterVisible(AppPipeline.FindById(pipelineId));
        if (pipeline == null) throw new ArgumentNullException(nameof(pipelineId), L.T("Pipeline does not exist."));
        if (!pipeline.Enable) throw new InvalidOperationException(L.T("Pipeline is not enabled."));

        var app = ProjectScopeRules.FilterVisible(AppDeploy.FindById(pipeline.DeployId));
        if (app == null) throw new InvalidOperationException(L.T("Application deployment set [{0}] does not exist.", pipeline.DeployId));

        var buildNode = ProjectScopeRules.FilterVisible(AppBuildNode.FindAllByNodeId(pipeline.BuildNodeId)).FirstOrDefault(e => e.DeployId == pipeline.DeployId);
        if (buildNode == null) throw new InvalidOperationException(L.T("Build node [{0}] does not exist or is not associated with deployment set [{1}].", pipeline.BuildNodeId, pipeline.DeployId));
        if (!buildNode.Enable) throw new InvalidOperationException(L.T("Build node [{0}] is not enabled.", buildNode));

        return (pipeline, app, buildNode);
    }

    /// <summary>推导构建分支。流水线分支为通配符或标签规则时回退部署集分支</summary>
    private static String? ResolveBuildBranch(AppPipeline pipeline, AppDeploy app, String? requested)
    {
        if (!requested.IsNullOrEmpty())
        {
            // 兼容旧运行记录的完整引用：refs/heads/x 取分支名，标签不能当分支构建
            if (requested.StartsWithIgnoreCase("refs/heads/")) return requested[11..];
            if (requested.StartsWithIgnoreCase("refs/tags/")) return app.Branch;

            return requested;
        }

        var branch = pipeline.Branch;
        if (!branch.IsNullOrEmpty() && !branch.Contains('*') && !branch.StartsWithIgnoreCase("tag:")) return branch;

        return app.Branch;
    }

    /// <summary>队列深度检查。超过上限时拒绝新的触发，避免推送风暴无限堆积</summary>
    private static void CheckQueueLength(Int32 pipelineId)
    {
        var max = StarServerSetting.Current.PipelineQueueLength;
        if (max <= 0) return;

        var count = AppPipelineRun.FindCount(AppPipelineRun._.PipelineId == pipelineId & AppPipelineRun._.Status == (Int32)PipelineStatus.Pending);
        if (count >= max) throw new InvalidOperationException(L.T("The pipeline queue is full ({0}); wait for the running tasks to finish.", max));
    }
    #endregion

    #region 主流程
    /// <summary>异步执行流水线编排：仅负责「下发编译命令并记录 CommandId」，绝不等待编译结果、绝不在此标成功。
    /// 编译真正完成后由 StarServer 的 CommandReply 事件驱动续跑（取版本 / 使用版本 / 自动部署），webhook 与手动两条路径共用此方法，严格复用 DeployService.Compile。
    /// 每个阶段写入 AppPipelineStep 记录，方便从 run 详情页查看各阶段状态。</summary>
    private async Task RunAsync(AppPipelineRun run, AppPipeline pipeline, AppDeploy app, AppBuildNode buildNode, String userHost, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"Pipeline-{run.Id}", run);
        run.TraceId = span?.TraceId;
        run.Update();

        try
        {
            // ★ 编译前检查取消
            if (cancellationToken.IsCancellationRequested)
            {
                run.Status = PipelineStatus.Cancelled;
                run.Update();
                return;
            }

            // ---------- 编译阶段：仅下发编译命令并记 CommandId，不等待、不标成功 ----------
            run.Status = PipelineStatus.Building;
            if (run.BuildStartedTime == default) run.BuildStartedTime = DateTime.Now;
            run.Update();

            var buildStep = new AppPipelineStep
            {
                RunId = run.Id,
                StepType = "Build",
                StepIndex = 0,
                NodeId = buildNode.NodeId,
                Status = "Running",
                StartedTime = run.BuildStartedTime,
                CreateTime = DateTime.Now,
            };
            buildStep.Insert();

            try
            {
                // 复用手动「编译上传」；writeHistory:false（真实历史由节点 PostEvents 与 CommandReply 事件写入）
                // fire-and-forget 下发：返回 NodeCommand.Id；编译真正完成由 StarServer 的 CommandReply 事件驱动续跑
                // 传入触发分支与提交，确保编译节点构建的是触发本次运行的代码，而不是分支最新
                var cmdId = await _deployService.Compile(app, buildNode, "Build-Upload", userHost, cancellationToken, writeHistory: false, branch: run.Branch, commitId: run.CommitId);
                buildStep.CommandId = cmdId;
                buildStep.Update();

                // 命令未真正发出（如 StarFactory 未就绪），reply?.Id 为 0：再不会有回包事件，必须立即失败，否则永远卡 Building
                if (cmdId == 0)
                {
                    buildStep.Status = "Failed";
                    buildStep.FinishedTime = DateTime.Now;
                    buildStep.Message = L.T("Build command was not sent (node may not be ready).");
                    buildStep.Update();
                    run.BuildFinishedTime = DateTime.Now;
                    run.Status = PipelineStatus.Failed;
                    run.Remark = L.T("Build command was not sent (node may not be ready).");
                    run.Update();
                    return;
                }
            }
            catch (Exception ex)
            {
                // 命令下发即失败（如节点不存在）：立即标记失败。Message/Remark 列宽有限，长异常消息需截断保留尾部
                buildStep.Status = "Failed";
                buildStep.FinishedTime = DateTime.Now;
                buildStep.Message = AppPipelineStep.CutMessage(ex.Message);
                buildStep.Update();
                run.BuildFinishedTime = DateTime.Now;
                run.Status = PipelineStatus.Failed;
                run.Remark = AppPipelineStep.CutMessage(ex.Message, 480);
                run.Update();
                return;
            }

            // 编译已下发，等待节点真正执行完后的 CommandReply 事件来更新步骤/run（不轮询、不等待）
            return;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            run.Status = PipelineStatus.Failed;
            run.Remark = AppPipelineStep.CutMessage(ex.Message, 480);
            run.Update();
        }
        finally
        {
            // 清理 CTS 字典（无论成功/失败/取消，run 已终态）
            if (_runningCts.TryRemove(run.Id, out var cts))
            {
                try { cts.Dispose(); } catch { /* 忽略 */ }
            }
        }
    }
    #endregion

    #region Webhook 辅助
    /// <summary>分支匹配。支持精确匹配与末尾通配符（release/*）</summary>
    private static Boolean MatchBranch(String pattern, String actual)
    {
        if (pattern.IsNullOrEmpty()) return true;
        if (actual.IsNullOrEmpty()) return false;

        // 末尾 * 视作通配符
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return actual.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return pattern.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>标签匹配。流水线分支配置 tag: 前缀后才响应标签推送，如 tag:v* 或 tag:v1.0.0</summary>
    private static Boolean MatchTag(String pattern, String tag)
    {
        if (pattern.IsNullOrEmpty() || !pattern.StartsWithIgnoreCase("tag:")) return false;
        if (tag.IsNullOrEmpty()) return false;

        return MatchBranch(pattern[4..], tag);
    }

    /// <summary>同 commit 在窗口内已有进行中或已成功的 run 视为重复</summary>
    private Boolean IsDuplicate(Int32 pipelineId, String commitId, TimeSpan window)
    {
        if (commitId.IsNullOrEmpty()) return false;

        var since = DateTime.Now - window;
        var exists = AppPipelineRun.FindAll(AppPipelineRun._.PipelineId == pipelineId & AppPipelineRun._.CommitId == commitId & AppPipelineRun._.CreateTime >= since);
        return exists.Any(e => e.Status is PipelineStatus.Pending or PipelineStatus.Building or PipelineStatus.UploadSucceeded or PipelineStatus.Deploying or PipelineStatus.Success);
    }
    #endregion
}
