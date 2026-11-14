# 抽取 Web 端 PipelineService 规格

## Why
`DeployService` 职责过重，同时承担底层指令下发（compile、install、control）和高层流水线编排（创建 run、异步编排、状态管理）。抽取 `PipelineService` 实现关注点分离。

## What Changes
- 新建 `Stardust.Web/Services/PipelineService.cs`，从 `DeployService` 迁出 `Trigger`、`Reprocess`、`RunAsync` 三个方法
- 从 `DeployService` 删除上述三个方法
- `AppPipelineController` 和 `AppPipelineRunController` 改为注入 `PipelineService` 替代 `DeployService`
- `Startup.cs` 注册 `PipelineService` 到 DI 容器

## Impact
- 依赖规格：[add-pipeline-feature](../add-pipeline-feature/spec.md)
- Affected specs: add-pipeline-feature
- Affected code:
  - `Stardust.Web/Services/DeployService.cs`（删方法）
  - `Stardust.Web/Services/PipelineService.cs`（新建）
  - `Stardust.Web/Areas/Deployment/Controllers/AppPipelineController.cs`（改注入）
  - `Stardust.Web/Areas/Deployment/Controllers/AppPipelineRunController.cs`（改注入）
  - `Stardust.Web/Startup.cs`（注册 DI）

## ADDED Requirements
### Requirement: PipelineService 独立管理流水线编排
系统 SHALL 在 `Stardust.Web.Services` 命名空间下提供 `PipelineService`，依赖 `DeployService` 和 `ITracer`，暴露 `Trigger(Int32 pipelineId, String userHost)` 和 `Reprocess(Int64 runId, String userHost)` 两个公开方法，内部通过 `RunAsync` 私有方法执行编译→查版本→部署编排。

#### Scenario: 手动触发流水线
- **WHEN** 调用 `PipelineService.Trigger(pipelineId, userHost)`
- **THEN** 创建 `AppPipelineRun`（Status=Pending，TriggerSource=manual），异步执行编译→查版本→部署，返回 run 对象

#### Scenario: 重跑流水线
- **WHEN** 调用 `PipelineService.Reprocess(runId, userHost)`
- **THEN** 基于原 `AppPipelineRun` 创建新 run（TriggerSource=reprocess），异步执行编排，返回新 run 对象

### Requirement: DeployService 不再包含流水线编排逻辑
`DeployService` SHALL 仅保留底层指令下发方法（`Compile`、`Control`、`Install`、`Start`、`Stop`、`Restart`、`Uninstall`、`ReadDotNet`、`ReadNginx`、`BuildNginx`），不再包含 `TriggerPipeline`、`ReprocessPipeline`、`RunPipelineAsync`。

### Requirement: Web 控制器依赖 PipelineService
`AppPipelineController` 和 `AppPipelineRunController` SHALL 通过构造函数注入 `PipelineService`，`Trigger` 和 `Reprocess` action 委托 `PipelineService` 执行。

## MODIFIED Requirements
无。新增服务，不修改已有接口契约。

## REMOVED Requirements
无。