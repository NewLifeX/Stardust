# 流水线（CI/CD）功能 Spec

## Why

当前星尘发布系统已具备"编译节点（DeployAgent）→ 拉代码/构建/打包/上传 → 部署节点（StarAgent）拉取运行"的完整链路，但所有环节都是**手工触发**：

- 用户需要在 Web 上点"编译上传"按钮
- 用户需要在 Web 上点"发布"按钮

缺少类似 Jenkins / GitLab CI / GitHub Actions 的"**配置好一条流水线，提交某分支后自动跑完**"能力。每次发布都要人工介入，不利于小型团队的持续交付。

## What Changes

新增"流水线"概念，串联"代码提交 → 编译 → 打包 → 上传 → 部署"全流程。

- 新增 3 张数据表：`AppPipeline`（流水线配置）、`AppPipelineRun`（运行实例）、`AppPipelineStep`（运行步骤）
- 新增 Web 端：流水线列表/表单/运行记录三个页面
- 新增服务端：`PipelineWebhookController`（接收 git 通用 webhook）、`PipelineTriggerService`（编排编译与部署）
- 复用现有能力：`AppDeploy` 的代码库/分支/项目参数、`AppBuildNode` 关联的编译节点、`DeployAgent` 的 `deploy/compile` 命令、`StarFactory.SendNodeCommand`、`DeployController.UploadBuildFile`、AppDeployNode 的 install 流程
- **不改动** DeployAgent、StarAgent、AppDeploy、AppBuildNode、AppDeployNode 任何已有逻辑
- **不破坏** 现有"手动编译上传"和"手动发布"两条 UI 路径

## Impact

- Affected specs：
  - 编译能力（已有，无变动）
  - 发布能力（已有，无变动）
  - **新增** 流水线编排能力
- Affected code：
  - `Stardust.Data/Deployment/Model.xml` — 新增 3 张表
  - `Stardust.Server/Controllers/PipelineWebhookController.cs` — 新增
  - `Stardust.Server/Services/PipelineService.cs` — 新增
  - `Stardust.Server/Startup.cs` — 注册服务与路由
  - `Stardust.Web/Areas/Deployment/Controllers/AppPipelineController.cs` — 新增
  - `Stardust.Web/Areas/Deployment/Controllers/AppPipelineRunController.cs` — 新增
  - `Stardust.Web/Areas/Deployment/Views/AppPipeline/*.cshtml` — 新增

## 核心设计

### 1. 流水线配置（AppPipeline）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Int32 | 编号 |
| Name | String | 流水线名（唯一） |
| DeployId | Int32 | 关联 AppDeploy：决定 Repository/Branch/ProjectPath/ProjectKind/BuildArgs/PackageFilters |
| Branch | String | 触发的分支（精确匹配，如 `main` / `release/*`） |
| Token | String | webhook 路径中的鉴权 token（自动生成，不可改） |
| Secret | String | webhook 签名密钥（HMAC-SHA256，可选） |
| BuildNodeId | Int32 | 编译节点（对应 AppBuildNode.NodeId） |
| DeployNodeIds | String | 部署节点 ID 列表（AppDeployNode.Id，分号分隔） |
| AutoDeploy | Boolean | 编译上传成功后是否自动触发 install |
| Enable | Boolean | 是否启用 |
| Remark | String | 备注 |

> **最小化原则**：编译参数全部从 `AppDeploy` 取，流水线不重复定义。新建一条流水线时，**BuildNodeId 必填但可后续在 AppBuildNode 列表里调整**。

### 2. 运行实例（AppPipelineRun）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Int32 | 编号 |
| PipelineId | Int32 | 关联 AppPipeline |
| Status | PipelineStatus | 状态枚举：Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled |
| TriggerSource | String | `webhook` / `manual` |
| CommitId | String | 触发提交的 SHA |
| CommitMessage | String | 提交信息 |
| CommitAuthor | String | 提交人 |
| CommitTime | DateTime | 提交时间 |
| Branch | String | 分支名 |
| BuildNodeId | Int32 | 实际跑编译的节点 |
| AppVersionId | Int32 | 编译产物对应的 AppDeployVersion |
| BuildStartedTime / BuildFinishedTime | DateTime | 编译阶段时间 |
| DeployStartedTime / DeployFinishedTime | DateTime | 部署阶段时间 |
| TraceId | String | 链路追踪 ID |

### 3. 运行步骤（AppPipelineStep）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | Int32 | 编号 |
| RunId | Int32 | 关联 AppPipelineRun |
| StepType | String | `Build` / `Upload` / `Deploy` |
| StepIndex | Int32 | 步骤序号 |
| NodeId | Int32 | 节点（编译节点 / 部署节点） |
| Status | String | `Pending` / `Running` / `Success` / `Failed` / `Skipped` |
| Message | String | 错误信息或日志摘要 |
| StartedTime / FinishedTime | DateTime | 步骤时间 |

> **PipelineStatus** 在 `Stardust.Models` 枚举中新增。

### 4. 触发流

#### 4.1 Webhook 触发

```
POST /Pipeline/Webhook?token={流水线.Token}
Body: 通用 JSON 负载（GitHub/GitLab/Gitee/自建 git 都能解析）

┌────────────────────────────────────────────────────────┐
│ WebhookController.OnWebhook(token, body)                │
│   1. 根据 token 找 AppPipeline，未找到 → 401            │
│   2. 若配置了 Secret，校验 X-Hub-Signature-256         │
│   3. 解析 payload：                                     │
│        - ref / branch：取 "refs/heads/main" 后缀        │
│        - commit SHA / message / author / time            │
│   4. 校验分支 == pipeline.Branch（支持 release/* 简单   │
│      通配符），不匹配 → 200 + ignore                    │
│   5. 防抖：同 CommitId 在 5 分钟内已有非 Failed 终态     │
│      → 200 + skip                                       │
│   6. 创建 AppPipelineRun（Status=Building）            │
│   7. 异步调用 PipelineService.TriggerAsync(runId)       │
│   8. 立即返回 200 + { runId }                           │
└────────────────────────────────────────────────────────┘
```

#### 4.2 手动触发

```
POST /Pipeline/Trigger?id={流水线.Id}    （需用户登录）
  - 等同于 webhook 流程，但 TriggerSource=manual
  - 不做去重（用户主动请求时可重跑）
```

#### 4.3 编排执行

```
PipelineService.TriggerAsync(runId)
  1. 把 run 状态置 Building，关联 BuildNodeId
  2. 构造 CompileCommand（与现有 DeployService.Compile 一致）
  3. 把 RunId 写进命令的 TraceId（用 pipeline.Run.TraceId）
  4. 调用 starFactory.SendNodeCommand(buildNode, "deploy/compile", args, timeout=3600)
     - 等待 DeployAgent 跑完 git pull / build / pack / upload
     - 这一步会调用 DeployController.UploadBuildFile
     - UploadBuildFile 创建 AppDeployVersion
  5. 成功 → 查 AppDeployVersion 中 DeployId=pipeline.DeployId 的最新记录
             即为本次产物，设 run.AppVersionId
             状态 → UploadSucceeded
  6. 若 pipeline.AutoDeploy=true：
        把 run 状态 → Deploying
        遍历 pipeline.DeployNodeIds（分号分隔）
        对每个 AppDeployNode：
           - 调 DeployService.Install(node)
           - 调 starFactory.SendNodeCommand(deployNode, "deploy/install", args)
           - 写一条 AppPipelineStep
        全部完成 → Deploying → Success
  7. 任意步骤异常 → Status=Failed，记录 AppDeployHistory

复用清单（不改这些文件）：
  - starFactory.SendNodeCommand（NewLife.Remoting）
  - Stardust.Web.Services.DeployService.Compile
  - Stardust.Web.Services.DeployService.Install
  - DeployController.UploadBuildFile
  - AppDeployHistory
```

## ADDED Requirements

### Requirement: 流水线配置管理

The system SHALL provide Web pages to create/edit/list `AppPipeline` records with all fields listed above. When a pipeline is created, the system SHALL auto-generate a random 32-char `Token` that cannot be edited.

#### Scenario: 新建流水线
- **WHEN** 用户进入「流水线」菜单，填写名称、选择 AppDeploy、填写 Branch、选择编译节点、勾选部署节点、点击保存
- **THEN** 系统创建 AppPipeline，自动生成 Token，跳转回列表，列表里展示 webhook URL `…/Pipeline/Webhook?token={token}`

#### Scenario: 查看 webhook URL
- **WHEN** 用户在流水线列表点击"复制 Webhook"
- **THEN** 系统弹出 webhook URL（含 token），用户复制到 git 平台 webhook 配置

### Requirement: 通用 Webhook 接入

The system SHALL expose `POST /Pipeline/Webhook?token={token}` that accepts a generic JSON push payload and triggers a pipeline run.

#### Scenario: GitHub push 触发
- **WHEN** git 平台调用 webhook URL，body 包含 `ref=refs/heads/main`、`head_commit.id=abc123`
- **THEN** 系统找到 Branch=main 的流水线，创建一条 run，状态=Building，30 秒内编译节点开始拉取

#### Scenario: 分支不匹配
- **WHEN** webhook 触发的分支与所有流水线都不匹配
- **THEN** 系统返回 200 `{result:"ignored", reason:"branch mismatch"}`，不创建 run

#### Scenario: 同一 commit 短时间内重复触发
- **WHEN** 同一 CommitId 在 5 分钟内已有 Pending/Building/UploadSucceeded/Deploying/Success 状态的 run
- **THEN** 系统返回 200 `{result:"skipped", reason:"duplicate"}`，不创建新 run

### Requirement: 流水线运行编排

The system SHALL orchestrate the build→upload→deploy chain using existing `StarFactory.SendNodeCommand` and `DeployService.Compile/Install` flows.

#### Scenario: 全流程成功
- **WHEN** 流水线被触发
- **THEN** 依次执行：编译节点 git pull + build + pack + upload → 新建 AppDeployVersion → 部署节点 install → 全部 AppDeployNode 完成 → run.Status=Success

#### Scenario: 编译失败
- **WHEN** 编译节点执行 `deploy/compile` 抛出异常
- **THEN** run.Status=Failed，对应 AppPipelineStep.Status=Failed，AppDeployHistory 记录异常信息

#### Scenario: 仅编译不上传（不推荐但保留）
- **WHEN** 流水线被触发但 BuildNode 配置 `UploadPackage=false`
- **THEN** run 进入 UploadSucceeded 状态但 AppVersionId=0，跳过部署步骤，最终 Status=Success（视为"产出可下载版本"完成）

### Requirement: 运行记录可查

The system SHALL provide a "流水线运行"列表页，按时间倒序展示 run 记录，支持按 PipelineId/Status/时间筛选，点击进入详情看 AppPipelineStep 时间线与错误信息。

#### Scenario: 查看运行详情
- **WHEN** 用户在运行列表点击某条 run
- **THEN** 系统展示 commit 信息、状态时间线、各步骤起止时间与消息

#### Scenario: 重跑
- **WHEN** 用户在某条 Failed/Success 的 run 上点"重跑"
- **THEN** 系统创建一条新 run，TriggerSource=manual，参数与原 run 相同（Branch、CommitId），立即开始

## 不在本次范围

- 灰度发布（按比例 / 按节点标签）
- 流水线阶段（多 step 编排）
- 定时触发（cron）
- 钉钉/飞书/企微审批
- GitHub Checks API / PR 状态回写
- 多租户/项目隔离（用现有 ProjectId 字段即可，UI 不做改动）

如需后续支持，AppPipeline 已留 `Remark` / `Enable` 等扩展位。
