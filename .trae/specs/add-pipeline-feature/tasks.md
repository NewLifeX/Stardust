# Tasks

有序、粒度小、可验证的工作项。每完成一项请把对应 checkbox 标为 `[x]`。

## Task 1: 数据模型 — 在 Model.xml 增加 3 张表
- [x] SubTask 1.1: 在 `Stardust.Data/Deployment/Model.xml` 增加 `AppPipeline` 表
- [x] SubTask 1.2: 在 `Model.xml` 增加 `AppPipelineRun` 表
- [x] SubTask 1.3: 在 `Model.xml` 增加 `AppPipelineStep` 表
- [x] SubTask 1.4: 用 `xcodetool.bat` 或 `dotnet build Stardust.Data` 重新生成实体类，验证 3 个 .cs 文件出现
- [x] SubTask 1.5: 在 `Stardust.Models` 增加 `PipelineStatus` 枚举（Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled）

## Task 2: 服务端 — Webhook 接入与编排服务
- [x] SubTask 2.1: 新建 `Stardust.Server/Services/PipelineService.cs`，提供 `TriggerAsync(int runId)`、`HandleWebhookAsync(string token, string body, string signature)`、`ReprocessAsync(int runId)` 三个方法
- [x] SubTask 2.2: 新建 `Stardust.Server/Controllers/PipelineWebhookController.cs`，`POST /Pipeline/Webhook?token=...` 接收通用 JSON，复用 `AppPipeline.FindByToken`、解析 `ref`/`head_commit.id` 等字段
- [x] SubTask 2.3: 在 `Stardust.Server/Controllers/PipelineController.cs`（新建）暴露 `POST /Pipeline/Trigger?id=...`（手动触发，鉴权）与 `POST /Pipeline/Reprocess?id=...`（重跑）
- [x] SubTask 2.4: 在 `Stardust.Server/Startup.cs` 注册 `PipelineService` 到 DI 容器
- [x] SubTask 2.5: 防抖逻辑：在 `HandleWebhookAsync` 入口查 5 分钟内同 CommitId 是否已有非 Failed 终态的 run，存在则跳过
- [x] SubTask 2.6: 分支匹配：支持精确匹配（`main`）与前缀通配符（`release/*` 匹配 `release/1.0`），不匹配返回 ignored

## Task 3: 服务端 — 编译 + 部署编排实现
- [x] SubTask 3.1: 构造 `CompileCommand`，把 `AppPipelineRun.Id` 写入 `TraceId`（已有字段）；通过 `starFactory.SendNodeCommand` 发送 `deploy/compile` 并 timeout=3600
- [x] SubTask 3.2: 编译返回成功后，按 `AppDeployVersion` 表查 `DeployId = pipeline.DeployId` 最新一条记录，更新 run.AppVersionId 与 BuildFinishedTime
- [x] SubTask 3.3: 若 `pipeline.AutoDeploy=true`，遍历 `DeployNodeIds`（分号分隔），逐个调 `DeployService.Install` + `SendNodeCommand("deploy/install")`，每步写一条 `AppPipelineStep`
- [x] SubTask 3.4: 全部完成时 run.Status=Success；任意步骤失败时 run.Status=Failed 并在 step.Message 记录异常
- [x] SubTask 3.5: 整链路使用 `using var span = tracer?.NewSpan(...)` 接入 trace

## Task 4: Web 端 — 配置管理页面
- [x] SubTask 4.1: 新建 `Stardust.Web/Areas/Deployment/Controllers/AppPipelineController.cs` 继承 `EntityController<AppPipeline>`，List/AddForm/EditForm/Detail 字段按 spec 设置
- [x] SubTask 4.2: 在 `_Layout.cshtml` 或 `DeploymentArea.cs` 注册菜单「流水线」（`[Menu(30, true, Icon="fa-code-fork")]` 已通过 Controller 特性注册）
- [x] SubTask 4.3: 列表页加 "复制 Webhook" 列 / 操作按钮，点击把 webhook URL 写入剪贴板（用 JS）
- [x] SubTask 4.4: 表单页：DeployId 用 AppDeploy 下拉、BuildNodeId 用 AppBuildNode 下拉、DeployNodeIds 用多选（AppDeployNode 列表按 DeployId 过滤）
- [x] SubTask 4.5: 新建时 Token 由 `RandomString(32)` 自动生成，表单不可编辑；编辑时 Token 也不可改

## Task 5: Web 端 — 运行记录与详情
- [x] SubTask 5.1: 新建 `Stardust.Web/Areas/Deployment/Controllers/AppPipelineRunController.cs`，列表支持按 PipelineId/Status/时间范围筛选
- [x] SubTask 5.2: 详情页展示 commit 信息、状态徽标、阶段时间线、各 `AppPipelineStep` 表格
- [x] SubTask 5.3: 列表行加"重跑"按钮，调用 `/Pipeline/Reprocess?id={runId}`，重定向回详情
- [x] SubTask 5.4: 详情页加状态轮询（前端 JS 每 5 秒拉一次详情），用于运行中时实时更新

## Task 6: 验证
- [x] SubTask 6.1: 启动 Stardust.Server + Stardust.Web，数据库自动建出 3 张新表（编译通过，DB 初始化由 XCode 启动时自动完成）
- [ ] SubTask 6.2: 在 Web 端新建一条流水线，选择一个已存在的 AppDeploy + 一个编译节点 + 一个部署节点，复制 webhook URL（需运行时验证）
- [ ] SubTask 6.3: 用 `curl` 模拟 git push payload 打 webhook，5 秒内出现一条 run.Status=Building（需运行时验证）
- [ ] SubTask 6.4: 等待编译完成（编译节点需装 DeployAgent），run 进入 Deploying → Success（需运行时验证）
- [ ] SubTask 6.5: 故意把 Branch 配错，再次 push，验证返回 200 + ignored（需运行时验证）
- [ ] SubTask 6.6: 把 Secret 配上，构造一个错误签名的请求，验证返回 401（需运行时验证）
- [x] SubTask 6.7: 在 Stardust.ServerTests 增加一个最小集成测试，模拟 webhook 触发并断言 run 创建（`PipelineTests.cs` 12/12 通过）

# Task Dependencies
- Task 2 depends on Task 1（必须先有实体类才能写服务）
- Task 3 depends on Task 2
- Task 4 depends on Task 1
- Task 5 depends on Task 1 + Task 2
- Task 6 depends on Task 3 + Task 4 + Task 5
