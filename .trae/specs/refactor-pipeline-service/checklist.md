# Checklist

- [x] `Stardust.Web/Services/PipelineService.cs` 存在，包含 `Trigger`、`Reprocess`（public）、`RunAsync`（private）三个方法
- [x] `PipelineService` 构造函数接收 `DeployService` 和 `ITracer`
- [x] `Trigger` 创建 `AppPipelineRun`（Status=Pending，TriggerSource=manual），`Task.Run` 异步执行 `RunAsync`
- [x] `Reprocess` 基于原 `AppPipelineRun` 创建新 run（TriggerSource=reprocess），`Task.Run` 异步执行 `RunAsync`
- [x] `RunAsync` 按顺序执行：编译 → 查版本 → 部署 → 更新状态，异常时 Status=Failed
- [x] `DeployService` 不再包含 `TriggerPipeline`、`ReprocessPipeline`、`RunPipelineAsync`
- [x] `AppPipelineController` 注入 `PipelineService`，`Trigger` action 调用 `_pipelineService.Trigger`
- [x] `AppPipelineRunController` 注入 `PipelineService`，`Reprocess` action 调用 `_pipelineService.Reprocess`
- [x] `Startup.cs` 包含 `services.AddSingleton<PipelineService>()`
- [x] `dotnet build Stardust.Web.csproj` 0 错误
- [x] `dotnet test Stardust.ServerTests/ServerTest.csproj` PipelineTests 12/12 通过