# Tasks

## Task 1: 撤销 Stardust.Server 端流水线实现

- [ ] SubTask 1.1: 删除 `Stardust.Server/Services/PipelineService.cs`
- [ ] SubTask 1.2: 删除 `Stardust.Server/Controllers/PipelineWebhookController.cs`
- [ ] SubTask 1.3: 删除 `Stardust.Server/Controllers/PipelineController.cs`
- [ ] SubTask 1.4: 从 `Stardust.Server/Startup.cs` 移除 `services.AddSingleton<PipelineService>()` 一行及其上方空行（如存在）
- [ ] SubTask 1.5: 编译 `Stardust.Server` 项目，确认无残留引用错误
- [ ] SubTask 1.6: 全局搜索 `Stardust.Server.Services.PipelineService` / `Stardust.Server.Controllers.PipelineWebhook` / `Stardust.Server.Controllers.Pipeline` 命名空间引用，确认无残留（注释/历史文档除外）

## Task 2: 扩展 Web 端 PipelineService，支持 webhook 入口

- [ ] SubTask 2.1: 在 `Stardust.Web/Services/PipelineService.cs` 新增 public 方法 `HandleWebhookAsync(String token, String body, String signature)`，返回 `Task<Object>`
  - 内部依次：token 空检查 → `AppPipeline.FindByToken` → `Enable` 检查 → Secret 签名校验 → `ParsePayload` → `MatchBranch` → `IsDuplicate` → 创建 `AppPipelineRun` → 异步 `Task.Run(() => RunAsync(...))`
  - 立即返回 `{result:"accepted", runId, status}` 等对象
- [ ] SubTask 2.2: 从 Server 端迁移 `ParsePayload(String body)` 私有方法及其依赖的内部类 `WebhookPayload` / `WebhookCommit` / `WebhookAuthor` / `WebhookObject`，放在 `PipelineService.cs` 同文件或 `Internal` 命名空间下
- [ ] SubTask 2.3: 从 Server 端迁移 `VerifySignature(String secret, String body, String signature)` 私有方法（HMAC-SHA256，GitHub `sha256=` 前缀剥离）
- [ ] SubTask 2.4: 从 Server 端迁移 `MatchBranch(String pattern, String actual)` 私有方法（精确 + 末尾通配符）
- [ ] SubTask 2.5: 从 Server 端迁移 `IsDuplicate(Int32 pipelineId, String commitId, TimeSpan window)` 私有方法（5 分钟窗口，排除 Failed/Cancelled）
- [ ] SubTask 2.6: webhook 创建 run 时正确填充字段：`TriggerSource="webhook"`、`Status=Pending`、`CommitId` / `CommitMessage` / `CommitAuthor` / `CommitTime` / `Branch` / `BuildNodeId` 来自 payload + pipeline
- [ ] SubTask 2.7: `HandleWebhookAsync` 不阻塞，run 创建后立即 return，编排通过 `Task.Run` 异步进行
- [ ] SubTask 2.8: 确认依赖 `DeployService` 与 `ITracer` 已通过现有构造函数注入，无需改 DI 注册

## Task 3: 修改 RunAsync，补「使用版本」步骤

- [ ] SubTask 3.1: 在 `RunAsync` 中编译完成、查到 `version` 后，新增 `if (version != null) { app.Version = version.Version; app.Update(); }` 一段
- [ ] SubTask 3.2: `run.AppVersionId = version?.Id ?? 0` 保留
- [ ] SubTask 3.3: 验证当 `buildNode.UploadPackage=false` 时不查 version、不更新 `app.Version`，但 `AutoDeploy=true` 时仍调 `Control(install)`
- [ ] SubTask 3.4: 确认 `RunAsync` 整体仅通过 `_deployService.Compile` / `_deployService.Control` 调用底层，不直接构造 `CompileCommand`、不直接调 `SendNodeCommand*`、不直接写 `AppDeployHistory`

## Task 4: 新增 Web 端 PipelineWebhookController

- [ ] SubTask 4.1: 新建 `Stardust.Web/Areas/Deployment/Controllers/PipelineWebhookController.cs`
  - 命名空间 `Stardust.Web.Areas.Deployment.Controllers`
  - 类 `PipelineWebhookController`，构造函数注入 `PipelineService` 与 `ITracer`
  - 不加 `[ApiFilter]`（webhook 无登录鉴权）
- [ ] SubTask 4.2: 类上加 `[Route("Pipeline/[action]")]` 显式路由，保证 URL 为 `/Pipeline/Webhook`
- [ ] SubTask 4.3: 实现 `[HttpPost] Webhook(String token)` action：
  - `Request.EnableBuffering()` + reset Position 读取原始 body
  - 读 `X-Hub-Signature-256` header
  - 调 `_pipelineService.HandleWebhookAsync(token, body, signature)` 返回对象
  - 用 `ITracer` 包一层 span（name=`Pipeline-Webhook`），tag token + body 长度 + 返回结果
- [ ] SubTask 4.4: body 读取使用 `StreamReader` + `leaveOpen: true`，读后 reset `Request.Body.Position = 0` 以便后续签名重算（如有需要）

## Task 5: 校验 CopyWebhook URL

- [ ] SubTask 5.1: 确认 `AppPipelineController.CopyWebhook(id)` 返回的 URL 形如 `{serviceAddress}/Pipeline/Webhook?token={pipeline.Token}`
- [ ] SubTask 5.2: 确认 `serviceAddress` 取自 `StarSetting.Current.ServiceAddress` 中首个 http 前缀地址
- [ ] SubTask 5.3: 流水线不存在时返回 `Json(500, "流水线不存在")`

## Task 6: 编译与回归验证

- [ ] SubTask 6.1: 编译 `Stardust.Web`，通过
- [ ] SubTask 6.2: 编译 `Stardust.Server`，通过
- [ ] SubTask 6.3: 运行 `Stardust.ServerTests/Deployment/PipelineTests.cs`，`MatchBranch` / `FindByToken` / `Token 自动生成` 测试通过（不依赖 Server 端 PipelineService）
- [ ] SubTask 6.4: 手动验证 UI「触发」按钮（调用 `AppPipelineController.Trigger` → `PipelineService.Trigger` → `RunAsync`）功能正常
- [ ] SubTask 6.5: 手动验证 UI「重跑」按钮（调用 `AppPipelineRunController.Reprocess` → `PipelineService.Reprocess` → `RunAsync`）功能正常
- [ ] SubTask 6.6: 手动验证「编译上传」「使用版本」「发布」三个手动操作按钮功能不受影响
- [ ] SubTask 6.7: 使用 curl/Postman 模拟 GitHub push 调用 `/Pipeline/Webhook?token=...`，确认返回 `{result:"accepted"}` 且后台 run 创建成功

# Task Dependencies

- Task 1 与 Task 2/3/4 可并行（Task 1 是删除，Task 2/3/4 是新建/扩展）
- Task 3 依赖 Task 2（`HandleWebhookAsync` 调用 `RunAsync`，RunAsync 改造要在 webhook 接入前完成或同时进行）
- Task 4 依赖 Task 2（controller 调用 `HandleWebhookAsync`）
- Task 5 独立，可与任意任务并行
- Task 6 依赖 Task 1-4 全部完成
