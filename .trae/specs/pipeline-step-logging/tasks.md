# Tasks

## Task 1: 修改 RunAsync，为编译阶段写入步骤并补全时间

- [x] SubTask 1.1: 编译开始前，创建 `AppPipelineStep`（StepType=Build, StepIndex=0, NodeId=buildNode.NodeId, Status=Running, StartedTime=now），Insert 后保存 step 引用
- [x] SubTask 1.2: 编译成功时，设置 step `Status=Success, FinishedTime=now, Message=null`，step.Update()
- [x] SubTask 1.3: 编译失败时（catch 路径），设置 `BuildFinishedTime=now`，step `Status=Failed, FinishedTime=now, Message=ex.Message`，step.Update()，然后 throw 到外层 catch 设置 run.Status=Failed
- [x] SubTask 1.4: RunId 使用 `(Int32)run.Id` 截断（已知约束：AppPipelineStep.RunId 是 Int32，AppPipelineRun.Id 是 Int64）
- [x] SubTask 1.5: 确保编译阶段仍在一个 try-catch 内，编译失败时也设置 `BuildFinishedTime`

## Task 2: 修改 RunAsync，为部署阶段写入步骤并补全时间

- [x] SubTask 2.1: 部署阶段整体包裹在 try-catch 内，catch 中设置 `DeployFinishedTime=now`
- [x] SubTask 2.2: 遍历每个 deployNodeId 时创建步骤并记录状态（Skipped/Success/Failed）
- [x] SubTask 2.3: 每个部署节点异常不向上抛，记录 firstError 但不中断后续节点执行
- [x] SubTask 2.4: 所有部署节点执行完后，若有 firstError，run.Status=Failed, run.Remark=firstError；否则 run.Status=Success

## Task 3: 编译与验证

- [x] SubTask 3.1: 编译 `Stardust.Web`，通过
- [x] SubTask 3.2: 运行 `Stardust.ServerTests/Deployment/PipelineTests.cs`，12 个测试仍通过
- [x] SubTask 3.3: 手动触发流水线，确认 `AppPipelineStep` 表中有步骤记录，状态正确

# Task Dependencies

- Task 1 与 Task 2 都改 `RunAsync`，应顺序执行或合并在一次修改中
- Task 3 依赖 Task 1-2