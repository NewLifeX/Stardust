# 流水线 Web 端统一实现 验收清单

## Server 端撤销

- [ ] `Stardust.Server/Services/PipelineService.cs` 文件已删除
- [ ] `Stardust.Server/Controllers/PipelineWebhookController.cs` 文件已删除
- [ ] `Stardust.Server/Controllers/PipelineController.cs` 文件已删除
- [ ] `Stardust.Server/Startup.cs` 中 `services.AddSingleton<PipelineService>()` 一行已移除
- [ ] `Stardust.Server` 项目编译通过，无残留对 `PipelineService` / `PipelineWebhookController` / `PipelineController` 的引用
- [ ] 全局搜索 `Stardust.Server.Services.PipelineService` / `Stardust.Server.Controllers.PipelineWebhook` / `Stardust.Server.Controllers.Pipeline` 无命中（注释/文档除外）

## Web 端新增：PipelineWebhookController

- [ ] `Stardust.Web/Areas/Deployment/Controllers/PipelineWebhookController.cs` 文件存在
- [ ] controller 使用 `[Route("Pipeline/[action]")]` 显式路由，URL 为 `/Pipeline/Webhook`
- [ ] `Webhook(String token)` action 通过构造函数注入 `PipelineService` 与 `ITracer`
- [ ] 读取原始 body 时使用 `Request.EnableBuffering()` 并 reset Position，不消耗 stream
- [ ] 读取 `X-Hub-Signature-256` header 传给 `HandleWebhookAsync`
- [ ] 调用 `_pipelineService.HandleWebhookAsync(token, body, signature)` 并直接返回结果对象
- [ ] 该 controller 无 `[ApiFilter]`（webhook 无鉴权，仅 token + 可选 secret）

## Web 端扩展：PipelineService.HandleWebhookAsync

- [ ] `Stardust.Web/Services/PipelineService.cs` 包含 public `HandleWebhookAsync(String token, String body, String signature)` 方法
- [ ] token 空 → 返回 `{result:"error", reason:"missing token"}`
- [ ] `AppPipeline.FindByToken(token)` 未命中 → 返回 `{result:"error", reason:"invalid token"}`
- [ ] `pipeline.Enable=false` → 返回 `{result:"ignored", reason:"pipeline disabled"}`
- [ ] 配置了 Secret 时校验签名：未携带 → `missing signature`，校验失败 → `invalid signature`
- [ ] `ParsePayload(body)` 正确解析 GitHub `ref` / `head_commit.id` 与 GitLab `object_attributes.source_branch` / `checkout_sha`
- [ ] 分支名从 `refs/heads/main` 形式正确剥离前缀
- [ ] `MatchBranch` 支持精确匹配与末尾通配符 `release/*`（大小写不敏感）
- [ ] `IsDuplicate` 检查 5 分钟内同 pipeline + 同 commit 是否有未达 Failed/Cancelled 终态的 run
- [ ] 创建 `AppPipelineRun` 时 `TriggerSource="webhook"`、`Status=Pending`、`CommitId/Message/Author/Time/Branch/BuildNodeId` 字段正确填充
- [ ] 异步 `Task.Run(() => RunAsync(...))` 不阻塞响应
- [ ] 立即返回 `{result:"accepted", runId, status}`

## Web 端扩展：RunAsync 编排

- [ ] `RunAsync` 在 webhook 与手动两条路径上共用，签名一致
- [ ] 编译阶段调用 `_deployService.Compile(app, buildNode, "Build-Upload", userHost, ct)`
- [ ] **不**重新构造 `CompileCommand`、**不**直接调用 `SendNodeCommand` / `SendNodeCommandAsync`
- [ ] 编译成功后，若 `buildNode.UploadPackage=true`，调用 `AppDeployVersion.FindAllByDeployId(pipeline.DeployId, 1).FirstOrDefault()` 取最新版本
- [ ] 查到 version 后调用 `app.Version = version.Version; app.Update();`（「使用版本」步骤）
- [ ] `run.AppVersionId` 被正确赋值（无版本时为 0）
- [ ] `AutoDeploy=true` 时遍历 `pipeline.DeployNodeIds`（逗号分隔），对每个 `AppDeployNode` 调 `_deployService.Control(app, dn, "install", userHost, 0, 0, null, ct)`
- [ ] 部署节点过滤：`dn != null && dn.Enable && dn.DeployId == pipeline.DeployId`
- [ ] **不**直接调用 `DeployService.Install()`、**不**自行拼装 args、**不**自行写 `AppDeployHistory`
- [ ] 全流程成功后 `run.Status = Success`
- [ ] 任意异常 → `run.Status = Failed`，`run.Remark = ex.Message`，`run.Update()`
- [ ] 编译失败时不进入部署阶段

## 行为一致性

- [ ] webhook 触发与手动触发走同一份 `RunAsync`，编译命令构造、`SendNodeCommandAsync` 调用、`AppDeployHistory` 写入方式完全一致
- [ ] 部署阶段调用的 `Control(install)` 与手动「发布」按钮调用同一方法
- [ ] 不再出现 `[Obsolete] SendNodeCommand` 调用

## Webhook URL 与配置

- [ ] `AppPipelineController.CopyWebhook(id)` 返回的 URL 形如 `{serviceAddress}/Pipeline/Webhook?token={pipeline.Token}`
- [ ] `serviceAddress` 来自 `StarSetting.Current.ServiceAddress`，取首个 http 前缀地址
- [ ] 流水线不存在时返回 500 错误

## 编译与测试

- [ ] `Stardust.Web` 项目编译通过
- [ ] `Stardust.Server` 项目编译通过
- [ ] `Stardust.ServerTests/Deployment/PipelineTests.cs` 中 `MatchBranch` / `FindByToken` / `Token 自动生成` 测试仍通过（不依赖 Server 端 PipelineService）
- [ ] 手动触发流水线（UI「触发」按钮）功能正常
- [ ] 手动重跑流水线（UI「重跑」按钮）功能正常
- [ ] 手动「编译上传」按钮功能不受影响
- [ ] 手动「使用版本」按钮功能不受影响
- [ ] 手动「发布」按钮功能不受影响

## 文档

- [ ] 不新增/修改任何 `*.md` 文档文件（除本 spec 系列）
- [ ] 代码注释使用中文，与现有风格一致
