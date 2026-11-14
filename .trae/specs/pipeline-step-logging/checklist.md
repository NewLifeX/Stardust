# 流水线详细步骤记录 验收清单

## RunAsync 编译阶段步骤

- [x] 编译开始前创建 `AppPipelineStep`（StepType=Build, StepIndex=0, NodeId=buildNode.NodeId, Status=Running, StartedTime=now）
- [x] 编译成功：step `Status=Success, FinishedTime=now`，`run.BuildFinishedTime=now`
- [x] 编译失败：step `Status=Failed, FinishedTime=now, Message=ex.Message`，`run.BuildFinishedTime=now`，异常继续向上抛到外层 catch
- [x] 使用 `(Int32)run.Id` 作为 RunId（匹配 AppPipelineStep.RunId 的 Int32 类型）
- [x] 步骤 Insert() 后 update

## RunAsync 部署阶段步骤

- [x] 每个部署节点创建一条 `AppPipelineStep`（StepType=Deploy, StepIndex=idx, NodeId=dn?.NodeId ?? 0）
- [x] 节点不存在：`Status=Skipped, Message="节点不存在"`
- [x] 节点未启用：`Status=Skipped, Message="节点未启用"`
- [x] DeployId 不匹配：`Status=Skipped, Message="DeployId 不匹配"`
- [x] 部署成功：step `Status=Success, FinishedTime=now`
- [x] 部署失败：step `Status=Failed, FinishedTime=now, Message=ex.Message`
- [x] 单个节点失败不中断后续节点，firstError 汇总到 run.Remark
- [x] 全部节点执行完后，若有 firstError → `run.Status=Failed, run.Remark=firstError`；否则 `run.Status=Success`

## 时间字段补全

- [x] 编译失败时 `run.BuildFinishedTime` 被设置
- [x] 部署失败时 `run.DeployFinishedTime` 被设置

## 编译与测试

- [x] `Stardust.Web` 项目编译通过
- [x] `PipelineTests` 12/12 通过
- [x] 手动触发流水线后 `AppPipelineStep` 表中有正确记录
