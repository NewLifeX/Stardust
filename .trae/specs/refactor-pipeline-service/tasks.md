# Tasks

- [x] Task 1: 新建 `Stardust.Web/Services/PipelineService.cs`，包含 `Trigger`、`Reprocess`、`RunAsync` 三个方法，依赖 `DeployService` + `ITracer`
- [x] Task 2: 从 `Stardust.Web/Services/DeployService.cs` 删除 `TriggerPipeline`、`ReprocessPipeline`、`RunPipelineAsync` 三个方法
- [x] Task 3: `AppPipelineController` 改为注入 `PipelineService`，`Trigger` action 调用 `_pipelineService.Trigger`
- [x] Task 4: `AppPipelineRunController` 改为注入 `PipelineService`，`Reprocess` action 调用 `_pipelineService.Reprocess`
- [x] Task 5: `Stardust.Web/Startup.cs` 注册 `services.AddSingleton<PipelineService>()`
- [x] Task 6: 编译验证（`dotnet build Stardust.Web.csproj` 0 错误）

# Task Dependencies
- Task 3 依赖 Task 1
- Task 4 依赖 Task 1
- Task 5 依赖 Task 1
- Task 2 可与 Task 1 并行