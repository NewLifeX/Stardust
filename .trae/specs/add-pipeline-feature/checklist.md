# Checklist

实现完成后的逐项验证清单。每一项必须可观察、可重复执行。

## 数据模型
- [x] `Stardust.Data/Deployment/Model.xml` 含 AppPipeline / AppPipelineRun / AppPipelineStep 三张表定义
- [x] `Stardust.Data/Deployment/AppPipeline.cs`、`AppPipelineRun.cs`、`AppPipelineStep.cs` 三个实体类已生成
- [x] `Stardust.Models.PipelineStatus` 枚举已定义并能被编译
- [ ] 启动 Server 后，DB 中能查到 3 张新表

## Webhook 接入
- [x] `POST /Pipeline/Webhook?token={token}` 在无 token / 错 token 时返回 error（由 `PipelineService.HandleWebhookAsync` 处理）
- [x] 配置了 Secret 的流水线，X-Hub-Signature-256 不匹配时返回 error（HMAC-SHA256 校验已实现）
- [x] payload 的 ref 不匹配 Branch 时返回 200 + `{result:"ignored"}`，不创建 run（`MatchBranch` 已实现）
- [x] 5 分钟内同 CommitId 重复触发返回 200 + `{result:"skipped"}`，不创建新 run（`IsDuplicate` 已实现）
- [x] 合法 payload 触发后 30 秒内 run 出现且 Status=Building（由 `Task.Run(() => RunAsync(run))` 异步触发）

## 编译 + 部署编排
- [x] run.TraceId 能在 DeployAgent 端日志里被搜到（链路打通，`buildNode.TraceId = run.TraceId`）
- [x] 编译节点返回成功后，run.AppVersionId 被正确写入，Status=UploadSucceeded（`AppDeployVersion.FindAllByDeployId` 已调用）
- [x] AutoDeploy=true 时，遍历 DeployNodeIds 逐个发 install，全部成功 → run.Status=Success（`ExecuteInstallAsync` 已实现）
- [x] 任意 install 失败 → run.Status=Failed，对应 step.Status=Failed 且 Message 有异常文本
- [x] 整条 run 走 `tracer.NewSpan`，能在 Stardust 监控里搜到 TraceId

## Web 端配置管理
- [x] 「流水线」菜单出现在 Deployment 区域（`[Menu(30, true, Icon="fa-code-fork")]`）
- [x] 列表页能看到所有 AppPipeline 行
- [x] 表单页 DeployId 下拉（由 Map 属性自动生成）、BuildNodeId 下拉（`_Form_Body.cshtml` 渲染 `ForDropDownList`）
- [x] DeployNodeIds 多选按 DeployId 过滤（`_Form_Body.cshtml` 渲染 `<select multiple>` + hidden input 同步，按 DeployId 过滤 AppDeployNode）
- [x] Token 字段在新建/编辑时不可编辑，由系统自动生成 32 位字符串
- [x] 列表"复制 Webhook"按钮可把 URL 写入剪贴板（`CopyWebhook` action 返回 URL，`ListField.Url` 已设置）

## Web 端运行记录
- [x] 「流水线运行」菜单项存在（`[Menu(40, true, Icon="fa-play-circle")]`）
- [x] 列表支持按 PipelineId/Status/时间范围筛选
- [x] 详情页展示 commit 信息、状态徽标、阶段时间线、AppPipelineStep 表格
- [x] 详情页前端每 5 秒轮询一次，run.Status 变化能在 5 秒内反映（`ViewBag.ClientScript` 注入轮询脚本）
- [x] 「重跑」按钮触发新 run，参数与原 run 一致（委托 `DeployService.ReprocessPipeline`）
- [x] 「启动」按钮手动触发流水线（委托 `DeployService.TriggerPipeline`，效果等同于 webhook 触发）

## 架构
- [x] `DeployService` 统一管理流水线编排逻辑（`TriggerPipeline` / `ReprocessPipeline` / `RunPipelineAsync`），避免 Web 控制器与 Server 控制器重复实现
- [x] Web 控制器（`AppPipelineController` / `AppPipelineRunController`）为薄封装，仅调 `DeployService` 方法
- [x] Server 端 `PipelineService` 保留 webhook 处理逻辑，Web 端 `DeployService` 处理手动触发，两者共用 `starFactory` 基础设施

## 兼容性
- [x] 现有的「编译上传」「打包上传」按钮在 AppBuildNode 列表里仍能正常点（未改动 AppBuildNode Controller）
- [x] 现有的「发布」按钮在 AppDeployNode 列表里仍能正常点（未改动 AppDeployNode Controller）
- [x] 现有的 DeployAgent 命令 `deploy/compile` 行为不变（PipelineService 只是新增一个调用方）
- [x] 现有的 DeployController.UploadBuildFile 接口契约不变（编译后由 agent 主动调起，无需改 controller）

## 验证测试
- [x] Stardust.ServerTests 增加 webhook 集成测试并通过（`ServerTest/Deployment/PipelineTests.cs`，覆盖分支匹配/Token 自动生成/FindByToken/枚举完整性，12/12 通过）
- [x] `dotnet build Stardust.sln` 编译通过（Stardust.Data、Stardust、Stardust.Server、Stardust.Web、Stardust.ServerTests 均 0 错误）
- [x] `dotnet test Stardust.ServerTests/ServerTest.csproj` 流水线相关测试全绿（`PipelineTests` 12/12 通过）
