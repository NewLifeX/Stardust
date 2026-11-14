# 流水线功能 Web 端统一实现 Spec

## Why

当前 Stardust 流水线（CI/CD）功能存在**两套实现**：

- **Web 端** `Stardust.Web/Services/PipelineService.cs`：负责手动触发，复用 `DeployService.Compile/Control`，行为与手动点击「编译上传」「发布」一致。
- **Server 端** `Stardust.Server/Services/PipelineService.cs`：负责 webhook 触发，**自行重写** `ExecuteCompileAsync` / `ExecuteInstallAsync`，行为与手动分叉。

根本原因：`Stardust.Server` 不引用 `Stardust.Web`（项目依赖单向 Web→Server），Server 端无法调用 `DeployService`，只能自己实现一遍编译/部署逻辑。这导致：

1. **行为不一致**（webhook vs 手动）：
   - 编译：Server 端调 `SendNodeCommand`（已 `[Obsolete]`），Web 端调 `SendNodeCommandAsync`。
   - 部署 install：Server 端不调 `DeployService.Install()`（不设 `Enable=true`），Web 端会调。
   - 部署 args：Server 端带 `verId`，Web 端不带。
   - timeout：Server 端 5/30 秒，Web 端 0（无超时）。
2. **"使用版本"步骤缺失**：流水线编译后只设 `run.AppVersionId`，未更新 `app.Version`。Web 端 `Control(install)` 不传 `verId`，部署节点按 `app.Version` 取包 → **可能部署到旧版本**。
3. **重复实现**：`CompileCommand` 构造、`SendNodeCommand` 调用、`AppDeployHistory` 写入在 Server 端被复制了一遍。

**目标**：撤销 Server 端流水线全部实现，统一在 Web 端实现。让 webhook 与手动两条路径共用同一套 `DeployService.Compile → 更新 app.Version → DeployService.Control(install)` 编排，**严格复用手动点击操作，不做额外逻辑实现**。

> 本功能未上线，无已配置 webhook 需兼容，按最佳方案重新设计。

## What Changes

### 撤销（Stardust.Server，全部删除）

- 删除 `Stardust.Server/Services/PipelineService.cs`
- 删除 `Stardust.Server/Controllers/PipelineWebhookController.cs`
- 删除 `Stardust.Server/Controllers/PipelineController.cs`
- `Stardust.Server/Startup.cs` 移除 `services.AddSingleton<PipelineService>()` 一行

### 新增（Stardust.Web）

- 新增 `Stardust.Web/Areas/Deployment/Controllers/PipelineWebhookController.cs`：接收 git push webhook，委托 `PipelineService.HandleWebhookAsync`
- `Stardust.Web/Services/PipelineService.cs` 增补：
  - `HandleWebhookAsync(token, body, signature)` 公开入口
  - `ParsePayload` / `VerifySignature` / `MatchBranch` / `IsDuplicate` 等 webhook 专属辅助方法（从 Server 端迁移）

### 修改（Stardust.Web）

- `PipelineService.RunAsync` 编译成功并查到 `AppDeployVersion` 后，补「使用版本」步骤：`app.Version = version.Version; app.Update();`
- `AppPipelineController.CopyWebhook` 确认 webhook URL 指向 Web 端地址

## Impact

- Affected specs:
  - [add-pipeline-feature](../add-pipeline-feature/spec.md) — **被取代**：webhook 与编排实现全部迁移到 Web 端
  - [refactor-pipeline-service](../refactor-pipeline-service/spec.md) — **保留**：Web 端 `PipelineService` 架构（Trigger/Reprocess/RunAsync）不变，仅扩展
- Affected code:
  - `Stardust.Server/Services/PipelineService.cs` — **删除**
  - `Stardust.Server/Controllers/PipelineWebhookController.cs` — **删除**
  - `Stardust.Server/Controllers/PipelineController.cs` — **删除**
  - `Stardust.Server/Startup.cs` — 移除一行注册
  - `Stardust.Web/Services/PipelineService.cs` — 增补 webhook 入口 + 「使用版本」逻辑
  - `Stardust.Web/Areas/Deployment/Controllers/PipelineWebhookController.cs` — **新增**
- 不受影响：
  - `Stardust.Web/Services/DeployService.cs`（保持原样，被复用）
  - `DeployAgent` / `StarAgent` / `AppDeploy` / `AppBuildNode` / `AppDeployNode` 任何已有逻辑
  - 手动「编译上传」「使用版本」「发布」三条 UI 路径

## 核心设计

### 1. Webhook 路由

```
POST /Pipeline/Webhook?token={AppPipeline.Token}
Body: 通用 JSON 负载（GitHub/GitLab/Gitee/自建 git 都能解析）
Header: X-Hub-Signature-256（可选，配合 Secret 使用）
```

路由策略：在 Web 端 `PipelineWebhookController` 上用 `[Route("Pipeline/[action]")]` 显式声明，URL 保持 `/Pipeline/Webhook?token=...` 简短形式，便于 git 平台配置。

### 2. 复用清单（不改动这些文件）

| 复用对象 | 来源 | 用途 |
|---|---|---|
| `DeployService.Compile` | `Stardust.Web/Services/DeployService.cs` | 手动「编译上传」按钮的方法，内部构造 `CompileCommand`、调 `SendNodeCommandAsync`、写 `AppDeployHistory` |
| `DeployService.Control(install)` | 同上 | 手动「发布」按钮的方法，内部调 `Install()` 设 `Enable=true`、调 `SendNodeCommandAsync`、写 `AppDeployHistory` |
| `StarFactory.SendNodeCommandAsync` | `Stardust` | 与手动一致的非 Obsolete 异步下发 |
| `AppDeployVersion.FindAllByDeployId` | `Stardust.Data` | 查编译产物 |
| `AppDeployHistory.Create` | `Stardust.Data` | 由 `DeployService` finally 块调用 |

### 3. Webhook 触发流

```
PipelineWebhookController.Webhook(token)
  1. 读取原始 body + X-Hub-Signature-256 header
  2. 调用 PipelineService.HandleWebhookAsync(token, body, signature)
     a. token 空 → 200 {result:"error", reason:"missing token"}
     b. AppPipeline.FindByToken(token) 未命中 → 200 {result:"error", reason:"invalid token"}
     c. pipeline.Enable=false → 200 {result:"ignored", reason:"pipeline disabled"}
     d. 配置了 Secret 时校验 X-Hub-Signature-256：
        - 未携带 → 200 {result:"error", reason:"missing signature"}
        - 校验失败 → 200 {result:"error", reason:"invalid signature"}
     e. ParsePayload(body) 解析 ref / commitId / message / author / time
        - 无分支 → 200 {result:"ignored", reason:"no branch"}
     f. MatchBranch(pipeline.Branch, actual)
        - 不匹配 → 200 {result:"ignored", reason:"branch mismatch"}
     g. IsDuplicate(pipelineId, commitId, 5min)
        - 重复 → 200 {result:"skipped", reason:"duplicate"}
     h. 创建 AppPipelineRun：
         - TriggerSource = "webhook"
         - Status = Pending
         - CommitId/Message/Author/Time/Branch/BuildNodeId 从 payload + pipeline 取
     i. 异步 _ = Task.Run(() => RunAsync(run, pipeline, app, buildNode, userHost, CancellationToken.None))
  3. 立即返回 200 {result:"accepted", runId, status}
```

### 4. 手动触发流（沿用 refactor-pipeline-service 架构）

```
AppPipelineController.Trigger(id)       → PipelineService.Trigger(pipelineId, userHost)
AppPipelineRunController.Reprocess(id) → PipelineService.Reprocess(runId, userHost)

两者最终都调用私有 RunAsync(run, pipeline, app, buildNode, userHost, ct)
```

### 5. 统一编排流程（RunAsync，webhook 与手动共用）

```
PipelineService.RunAsync(run, pipeline, app, buildNode, userHost, ct)
  1. run.TraceId = span?.TraceId; run.Update()
  2. run.Status = Building; run.BuildStartedTime = now; run.Update()
  3. ★ 调用 _deployService.Compile(app, buildNode, "Build-Upload", userHost, ct)
       ← 复用手动「编译上传」操作
  4. 编译成功，查最新版本：
       if buildNode.UploadPackage:
         version = AppDeployVersion.FindAllByDeployId(pipeline.DeployId, 1).FirstOrDefault()
         run.AppVersionId = version?.Id ?? 0
  5. ★【使用版本】if version != null:
         app.Version = version.Version
         app.Update()
       ← 对齐手动「使用版本」操作，确保后续 install 部署该版本
  6. run.BuildFinishedTime = now; run.Status = UploadSucceeded; run.Update()
  7. if pipeline.AutoDeploy:
       run.Status = Deploying; run.DeployStartedTime = now; run.Update()
       遍历 pipeline.DeployNodeIds（逗号分隔）:
         var dn = AppDeployNode.FindById(id)
         if dn != null && dn.Enable && dn.DeployId == pipeline.DeployId:
           ★ 调用 _deployService.Control(app, dn, "install", userHost, 0, 0, null, ct)
             ← 复用手动「发布」操作
       run.DeployFinishedTime = now
  8. run.Status = Success; run.Update()
  9. 异常 catch:
       run.Status = Failed; run.Remark = ex.Message; run.Update()
```

### 6. 严格不做额外逻辑

`PipelineService.RunAsync` **不**重新实现以下内容（这些由 `DeployService` 内部完成，直接复用即可）：

- 不重新构造 `CompileCommand`（由 `DeployService.Compile` 内部完成）
- 不直接调用 `SendNodeCommand` / `SendNodeCommandAsync`（由 `DeployService.Compile/Control` 内部完成）
- 不直接写 `AppDeployHistory`（由 `DeployService.Compile/Control` 的 finally 块完成）
- 不直接调用 `DeployService.Install()`（由 `DeployService.Control(install)` 内部 switch 调用）
- 不写 `AppPipelineStep`（webhook 路径暂不写 step，作为后续独立扩展）

### 7. Webhook URL 来源

`AppPipelineController.CopyWebhook(id)` 返回的 URL 使用 `StarSetting.Current.ServiceAddress`（同进程部署时即 Web 站地址），格式：

```
{serviceAddress}/Pipeline/Webhook?token={pipeline.Token}
```

## ADDED Requirements

### Requirement: Webhook 接入在 Web 端实现

系统 SHALL 在 `Stardust.Web` 提供 `POST /Pipeline/Webhook?token={token}` 接口，接收通用 git push JSON 负载并触发流水线运行。webhook 处理复用 Web 端 `PipelineService.HandleWebhookAsync`，编排复用 `DeployService.Compile/Control`。

#### Scenario: GitHub push 触发

- **WHEN** git 平台调用 webhook URL，body 包含 `ref=refs/heads/main`、`head_commit.id=abc123`
- **THEN** 系统找到 Branch=main 的流水线，创建一条 run（TriggerSource=webhook），异步调用 `RunAsync`，30 秒内编译节点开始拉取

#### Scenario: 分支不匹配

- **WHEN** webhook 触发的分支与流水线配置的 Branch 不匹配（精确匹配或末尾通配符 `release/*`）
- **THEN** 系统返回 200 `{result:"ignored", reason:"branch mismatch"}`，不创建 run

#### Scenario: 同 commit 短时间内重复触发

- **WHEN** 同一 CommitId 在 5 分钟内已有 Pending/Building/UploadSucceeded/Deploying/Success 状态的 run
- **THEN** 系统返回 200 `{result:"skipped", reason:"duplicate"}`，不创建新 run

#### Scenario: 签名校验失败

- **WHEN** 流水线配置了 Secret，但请求未携带 `X-Hub-Signature-256` 或签名不匹配
- **THEN** 系统返回 200 `{result:"error", reason:"missing/invalid signature"}`，不创建 run

#### Scenario: 流水线未启用

- **WHEN** webhook 命中的流水线 `Enable=false`
- **THEN** 系统返回 200 `{result:"ignored", reason:"pipeline disabled"}`，不创建 run

### Requirement: 编排严格复用手动操作

`PipelineService.RunAsync` SHALL 通过调用 `DeployService.Compile` 和 `DeployService.Control(install)` 完成编译与部署，**不得**重新构造 `CompileCommand` 或直接调用 `SendNodeCommand`。webhook 与手动两条触发路径 SHALL 走同一份 `RunAsync` 实现。

#### Scenario: webhook 触发与手动触发行为一致

- **WHEN** 同一流水线分别由 webhook 和手动触发
- **THEN** 两次运行的编译命令构造方式、`SendNodeCommandAsync` 调用方式、`AppDeployHistory` 记录方式完全一致

#### Scenario: 编译失败

- **WHEN** `DeployService.Compile` 抛出异常
- **THEN** run.Status=Failed，run.Remark 记录异常信息，不进入部署阶段

### Requirement: 编译后更新应用版本

`PipelineService.RunAsync` 在编译成功并查到 `AppDeployVersion` 后 SHALL 更新 `AppDeploy.Version`，使后续 `Control(install)` 部署到该版本，对齐手动「使用版本」操作。

#### Scenario: 编译后自动使用版本

- **WHEN** 编译节点上传成功（`buildNode.UploadPackage=true`），查到新的 `AppDeployVersion`
- **THEN** `AppDeploy.Version` 被更新为该版本号，后续 `Control(install)` 部署该版本

#### Scenario: 仅编译不上传

- **WHEN** `buildNode.UploadPackage=false`
- **THEN** 不查 version，不更新 `app.Version`，`run.AppVersionId=0`；若 `AutoDeploy=true` 仍调用 `Control(install)`（部署 `app.Version` 当前值），最终 `Status=Success`

## REMOVED Requirements

### Removed: Server 端流水线编排逻辑

撤销 [add-pipeline-feature](../add-pipeline-feature/spec.md) 中关于 `Stardust.Server/Services/PipelineService.cs` 与 `Stardust.Server/Controllers/PipelineWebhookController.cs` / `PipelineController.cs` 的需求。Server 端不再承担流水线任何职责。

### Removed: Server 端 ExecuteCompileAsync / ExecuteInstallAsync

撤销 Server 端 `PipelineService.ExecuteCompileAsync`（自行构造 `CompileCommand` 并调用 `[Obsolete] SendNodeCommand`）和 `ExecuteInstallAsync`（自行拼装 args 并调用 `SendNodeCommand`，带 `verId`）的设计。这些职责由 Web 端 `DeployService` 统一承担。

## 不在本次范围

- 灰度发布（按比例 / 按节点标签）
- 流水线阶段（多 step 编排，AppPipelineStep 暂不写入）
- 定时触发（cron）
- 钉钉/飞书/企微审批
- GitHub Checks API / PR 状态回写
- 多租户/项目隔离（用现有 ProjectId 字段即可，UI 不做改动）
- 兼容已配置的旧 webhook URL（功能未上线，无此约束）

如需后续支持，`AppPipeline` 已留 `Remark` / `Enable` 等扩展位。
