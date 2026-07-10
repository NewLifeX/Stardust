using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Models;

namespace Stardust.Web.Services;

/// <summary>流水线编排服务。管理流水线运行的完整生命周期：创建运行记录 → 触发编译 → 查版本 → 部署。
/// 同时提供 webhook 入口，让 git push 与手动触发共用同一份 RunAsync 编排，严格复用 DeployService.Compile/Control</summary>
public class PipelineService
{
    private readonly DeployService _deployService;
    private readonly ITracer _tracer;

    /// <summary>防抖时间窗口。同一 commit 在窗口内只允许触发一次</summary>
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(5);

    public PipelineService(DeployService deployService, ITracer tracer)
    {
        _deployService = deployService;
        _tracer = tracer;
    }

    #region 手动触发
    /// <summary>手动触发流水线运行。创建运行记录，异步执行编译→上传→部署编排</summary>
    /// <param name="pipelineId">流水线编号</param>
    /// <param name="userHost">触发者 IP</param>
    /// <returns>新创建的运行记录</returns>
    public async Task<AppPipelineRun> Trigger(Int32 pipelineId, String userHost)
    {
        var pipeline = AppPipeline.FindById(pipelineId);
        if (pipeline == null) throw new ArgumentNullException(nameof(pipelineId), "流水线不存在");
        if (!pipeline.Enable) throw new InvalidOperationException("流水线未启用");

        var app = AppDeploy.FindById(pipeline.DeployId);
        if (app == null) throw new InvalidOperationException($"应用部署集[{pipeline.DeployId}]不存在");

        var buildNode = AppBuildNode.FindById(pipeline.BuildNodeId);
        if (buildNode == null) throw new InvalidOperationException($"编译节点[{pipeline.BuildNodeId}]不存在");
        if (!buildNode.Enable) throw new InvalidOperationException($"编译节点[{buildNode}]未启用");

        var run = new AppPipelineRun
        {
            PipelineId = pipeline.Id,
            Status = PipelineStatus.Pending,
            TriggerSource = "manual",
            Branch = pipeline.Branch,
            BuildNodeId = pipeline.BuildNodeId,
        };
        run.Insert();

        _ = Task.Run(() => RunAsync(run, pipeline, app, buildNode, userHost, CancellationToken.None));

        return run;
    }

    /// <summary>重新处理流水线运行（重试失败或被中断的运行）。基于原运行创建新记录并执行</summary>
    /// <param name="runId">原运行记录编号</param>
    /// <param name="userHost">触发者 IP</param>
    /// <returns>新创建的运行记录</returns>
    public async Task<AppPipelineRun> Reprocess(Int64 runId, String userHost)
    {
        var src = AppPipelineRun.FindById(runId);
        if (src == null) throw new ArgumentNullException(nameof(runId), "运行记录不存在");

        var pipeline = AppPipeline.FindById(src.PipelineId);
        if (pipeline == null) throw new InvalidOperationException($"原运行所属流水线[{src.PipelineId}]不存在");
        if (!pipeline.Enable) throw new InvalidOperationException($"流水线[{pipeline.Name}]未启用");

        var app = AppDeploy.FindById(pipeline.DeployId);
        if (app == null) throw new InvalidOperationException($"应用部署集[{pipeline.DeployId}]不存在");

        var buildNode = AppBuildNode.FindById(pipeline.BuildNodeId);
        if (buildNode == null) throw new InvalidOperationException($"编译节点[{pipeline.BuildNodeId}]不存在");
        if (!buildNode.Enable) throw new InvalidOperationException($"编译节点[{buildNode}]未启用");

        var run = new AppPipelineRun
        {
            PipelineId = src.PipelineId,
            Status = PipelineStatus.Pending,
            TriggerSource = "reprocess",
            CommitId = src.CommitId,
            CommitMessage = src.CommitMessage,
            CommitAuthor = src.CommitAuthor,
            CommitTime = src.CommitTime == default ? DateTime.Now : src.CommitTime,
            Branch = src.Branch,
            BuildNodeId = src.BuildNodeId,
        };
        run.Insert();

        _ = Task.Run(() => RunAsync(run, pipeline, app, buildNode, userHost, CancellationToken.None));

        return run;
    }
    #endregion

    #region Webhook 入口
    /// <summary>处理 git push webhook 入口。解析 payload、分支匹配、防抖后创建 run 并异步执行</summary>
    /// <param name="token">webhook 鉴权 token（AppPipeline.Token）</param>
    /// <param name="body">原始请求体</param>
    /// <param name="signature">GitHub 风格 X-Hub-Signature-256，可选</param>
    /// <returns>处理结果对象，形如 {result:"accepted", runId, status}</returns>
    public async Task<Object> HandleWebhookAsync(String token, String body, String signature)
    {
        if (token.IsNullOrEmpty()) return new { result = "error", reason = "missing token" };

        var pipeline = AppPipeline.FindByToken(token);
        if (pipeline == null) return new { result = "error", reason = "invalid token" };
        if (!pipeline.Enable) return new { result = "ignored", reason = "pipeline disabled" };

        // 签名校验（可选，配置了 Secret 才校验）
        if (!pipeline.Secret.IsNullOrEmpty())
        {
            if (signature.IsNullOrEmpty()) return new { result = "error", reason = "missing signature" };
            if (!VerifySignature(pipeline.Secret, body, signature)) return new { result = "error", reason = "invalid signature" };
        }

        // 解析 payload
        var (branch, commitId, commitMessage, commitAuthor, commitTime) = ParsePayload(body);
        if (branch.IsNullOrEmpty()) return new { result = "ignored", reason = "no branch" };

        // 分支匹配
        if (!MatchBranch(pipeline.Branch, branch)) return new { result = "ignored", reason = "branch mismatch" };

        // 防抖：同 pipeline + 同 commit 在 5 分钟内有进行中或已成功的 run 则跳过
        if (!commitId.IsNullOrEmpty() && IsDuplicate(pipeline.Id, commitId, DedupWindow))
            return new { result = "skipped", reason = "duplicate" };

        // 关联实体校验，避免创建无法执行的 run
        var buildNode = AppBuildNode.FindById(pipeline.BuildNodeId);
        if (buildNode == null || !buildNode.Enable) return new { result = "error", reason = "build node unavailable" };

        var app = AppDeploy.FindById(pipeline.DeployId);
        if (app == null) return new { result = "error", reason = "deploy not found" };

        // 创建 run（Pending）
        var run = new AppPipelineRun
        {
            PipelineId = pipeline.Id,
            Status = PipelineStatus.Pending,
            TriggerSource = "webhook",
            CommitId = commitId,
            CommitMessage = commitMessage,
            CommitAuthor = commitAuthor,
            CommitTime = commitTime == default ? DateTime.Now : commitTime,
            Branch = branch,
            BuildNodeId = pipeline.BuildNodeId,
        };
        run.Insert();

        // 异步执行，不阻塞 webhook 响应
        _ = Task.Run(() => RunAsync(run, pipeline, app, buildNode, "webhook", CancellationToken.None));

        return new { result = "accepted", runId = run.Id, status = run.Status.ToString() };
    }
    #endregion

    #region 主流程
    /// <summary>异步执行流水线编排：编译 → 查版本 → 使用版本 → 部署。
    /// webhook 与手动两条路径共用此方法，严格复用 DeployService.Compile/Control。
    /// 每个阶段写入 AppPipelineStep 记录，方便从 run 详情页查看各阶段状态。</summary>
    private async Task RunAsync(AppPipelineRun run, AppPipeline pipeline, AppDeploy app, AppBuildNode buildNode, String userHost, CancellationToken cancellationToken)
    {
        using var span = _tracer?.NewSpan($"Pipeline-{run.Id}", run);
        run.TraceId = span?.TraceId;
        run.Update();

        try
        {
            // ---------- 编译阶段 ----------
            run.Status = PipelineStatus.Building;
            run.BuildStartedTime = DateTime.Now;
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
                // 复用手动「编译上传」操作
                await _deployService.Compile(app, buildNode, "Build-Upload", userHost, cancellationToken);
                buildStep.Status = "Success";
                buildStep.FinishedTime = DateTime.Now;
                buildStep.Update();
            }
            catch (Exception ex)
            {
                buildStep.Status = "Failed";
                buildStep.FinishedTime = DateTime.Now;
                buildStep.Message = ex.Message;
                buildStep.Update();
                run.BuildFinishedTime = DateTime.Now;
                throw;
            }

            // 编译完成，查询版本
            AppDeployVersion version = null;
            if (buildNode.UploadPackage)
            {
                var vers = AppDeployVersion.FindAllByDeployId(pipeline.DeployId, 1);
                version = vers.FirstOrDefault();
            }
            run.AppVersionId = version?.Id ?? 0;

            // ★【使用版本】对齐手动「使用版本」操作，确保后续 Control(install) 部署该版本
            if (version != null)
            {
                app.Version = version.Version;
                app.Update();
            }

            run.BuildFinishedTime = DateTime.Now;
            run.Status = PipelineStatus.UploadSucceeded;
            run.Update();

            // ---------- 自动部署 ----------
            if (!pipeline.AutoDeploy)
            {
                run.Status = PipelineStatus.Success;
                run.Update();
                return;
            }

            run.Status = PipelineStatus.Deploying;
            run.DeployStartedTime = DateTime.Now;
            run.Update();

            String firstError = null;
            var nodeIds = (pipeline.DeployNodeIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (var idx = 0; idx < nodeIds.Length; idx++)
            {
                var dn = AppDeployNode.FindById(nodeIds[idx].ToInt());

                var deployStep = new AppPipelineStep
                {
                    RunId = run.Id,
                    StepType = "Deploy",
                    StepIndex = idx,
                    NodeId = dn?.NodeId ?? 0,
                    Status = "Running",
                    StartedTime = DateTime.Now,
                    CreateTime = DateTime.Now,
                };
                deployStep.Insert();

                if (dn == null)
                {
                    deployStep.Status = "Skipped";
                    deployStep.Message = "节点不存在";
                    deployStep.FinishedTime = DateTime.Now;
                }
                else if (!dn.Enable)
                {
                    deployStep.Status = "Skipped";
                    deployStep.Message = "节点未启用";
                    deployStep.FinishedTime = DateTime.Now;
                }
                else if (dn.DeployId != pipeline.DeployId)
                {
                    deployStep.Status = "Skipped";
                    deployStep.Message = "DeployId 不匹配";
                    deployStep.FinishedTime = DateTime.Now;
                }
                else
                {
                    try
                    {
                        // 复用手动「发布」操作
                        await _deployService.Control(app, dn, "install", userHost, 0, 0, null, cancellationToken);
                        deployStep.Status = "Success";
                    }
                    catch (Exception ex)
                    {
                        deployStep.Status = "Failed";
                        deployStep.Message = ex.Message;
                        firstError ??= ex.Message;
                    }
                    finally
                    {
                        deployStep.FinishedTime = DateTime.Now;
                    }
                }
                deployStep.Update();
            }

            run.DeployFinishedTime = DateTime.Now;

            if (firstError != null)
            {
                run.Status = PipelineStatus.Failed;
                run.Remark = firstError;
                run.Update();
                return;
            }

            run.Status = PipelineStatus.Success;
            run.Update();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            run.Status = PipelineStatus.Failed;
            run.Remark = ex.Message;
            run.Update();
        }
    }
    #endregion

    #region Webhook 辅助
    /// <summary>分支匹配。支持精确匹配与末尾通配符（release/*）</summary>
    private Boolean MatchBranch(String pattern, String actual)
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

    /// <summary>HMAC-SHA256 校验（GitHub 风格 X-Hub-Signature-256）</summary>
    private Boolean VerifySignature(String secret, String body, String signature)
    {
        if (secret.IsNullOrEmpty() || body == null || signature.IsNullOrEmpty()) return false;

        // 形如 "sha256=xxxxx"
        var sig = signature.StartsWithIgnoreCase("sha256=") ? signature[7..] : signature;

        try
        {
            using var hmac = new HMACSHA256(secret.GetBytes());
            var hash = hmac.ComputeHash(body.GetBytes());
            var expected = hash.ToBase64();
            return expected.Equals(sig, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>解析通用 webhook payload（尽力兼容 GitHub/GitLab）</summary>
    private (String branch, String commitId, String commitMessage, String commitAuthor, DateTime commitTime) ParsePayload(String body)
    {
        if (body.IsNullOrEmpty()) return default;

        try
        {
            var json = body.ToJsonEntity<WebhookPayload>();
            if (json == null) return default;

            // GitHub: ref=refs/heads/main, head_commit.id
            var branch = json.Ref;
            if (!branch.IsNullOrEmpty() && branch.StartsWithIgnoreCase("refs/heads/")) branch = branch["refs/heads/".Length..];

            // GitLab: object_attributes.source_branch / checkout_sha
            if (branch.IsNullOrEmpty() && json.ObjectAttributes != null) branch = json.ObjectAttributes.SourceBranch;
            var commitId = json.After ?? json.CheckoutSha ?? json.HeadCommit?.Id;
            if (commitId.IsNullOrEmpty() && json.ObjectAttributes != null) commitId = json.ObjectAttributes.CheckoutSha;

            var commitMessage = json.HeadCommit?.Message ?? json.ObjectAttributes?.Message;
            var commitAuthor = json.HeadCommit?.Author?.Name ?? json.UserName ?? json.ObjectAttributes?.AuthorName;
            // 时间戳：head_commit.timestamp → commits[0].timestamp → object_attributes.timestamp
            var commitTime = json.HeadCommit?.Timestamp
                ?? json.Commits?.FirstOrDefault()?.Timestamp
                ?? json.ObjectAttributes?.Timestamp
                ?? default;

            return (branch, commitId, commitMessage, commitAuthor, commitTime);
        }
        catch
        {
            return default;
        }
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

/// <summary>通用 webhook payload（GitHub/GitLab/Gitea 兼容）</summary>
internal class WebhookPayload
{
    public String Ref { get; set; }
    public String After { get; set; }
    public String CheckoutSha { get; set; }
    public String UserName { get; set; }
    public WebhookCommit HeadCommit { get; set; }
    public WebhookObject ObjectAttributes { get; set; }
    public WebhookCommit[] Commits { get; set; }
}

internal class WebhookCommit
{
    public String Id { get; set; }
    public String Message { get; set; }
    public DateTime Timestamp { get; set; }
    public WebhookAuthor Author { get; set; }
}

internal class WebhookAuthor
{
    public String Name { get; set; }
}

internal class WebhookObject
{
    public String SourceBranch { get; set; }
    public String CheckoutSha { get; set; }
    public String Message { get; set; }
    public DateTime Timestamp { get; set; }
    public String AuthorName { get; set; }
}
