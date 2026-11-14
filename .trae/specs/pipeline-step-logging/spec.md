# 流水线详细步骤记录 Spec

## Why

当前 `AppPipelineRun` 只记录整体状态和最终失败信息，但无法查看每一阶段的逐个执行情况：
- 多节点部署时，哪些节点成功哪些失败不清晰
- 编译失败后不知道 `BuildFinishedTime`，无法计算耗时
- 被跳过的部署节点没有说明原因
- 整体失败后无法定位具体是哪一步失败

实体表 `AppPipelineStep` 已经在 `Model.xml` 定义完毕，字段齐全，只需要把写入逻辑加到 `RunAsync` 中。

## What Changes

### 修改 `PipelineService.RunAsync`

在 `RunAsync` 中为每个阶段创建 `AppPipelineStep` 记录：
- **编译阶段**：创建 `StepType=Build`，关联 `buildNode.NodeId`
- **部署阶段**：每个部署节点单独创建 `StepType=Deploy`，关联 `deployNode.NodeId`
- 每个步骤：记录 `StartedTime` → `Status=Running` → 执行后 `FinishedTime` → `Status=Success/Failed/Skipped` + `Message`
- 被跳过的节点（不存在 / 未启用 / DeployId 不匹配）：创建 `Status=Skipped` 并在 `Message` 记录原因

### 补全时间字段

解决之前分析的问题：
- 编译失败 → 设置 `BuildFinishedTime = DateTime.Now` 并写入步骤
- 部署失败 → 设置 `DeployFinishedTime = DateTime.Now` 并写入每个部署节点步骤

### UI 展示（已有基础，无需改动）

`AppPipelineRun` 详情页可通过 `FindAllByRunId` 查询所有步骤，显示步骤列表，每个步骤的状态、节点、耗时、消息。现有架构已经支持（Cube 自动关联查询），无需新增控制器/视图。

## Impact

- Affected specs:
  - [move-pipeline-to-web](../move-pipeline-to-web/spec.md) — 扩展当前实现，增加步骤记录
  - [add-pipeline-feature](../add-pipeline-feature/spec.md) — 补充了完整步骤记录
- Affected code:
  - `Stardust.Web/Services/PipelineService.cs` — 新增步骤创建逻辑
  - 不影响 `AppPipelineStep` 实体定义（已存在）
  - 不影响 UI 路由和控制器（现有详情页自然展示）

## ADDED Requirements

### Requirement: 流水线运行逐步骤记录

系统 SHALL 在 `AppPipelineRun` 执行过程中，为每个步骤（Build / 每个 Deploy 节点）创建一条 `AppPipelineStep` 记录。每个步骤 SHALL 包含：
- 步骤类型 (Build/Deploy)
- 步骤序号
- 执行节点 ID
- 状态 (Pending/Running/Success/Failed/Skipped)
- 开始时间 / 完成时间
- 消息/异常原因

#### Scenario: 编译失败

- **WHEN** `_deployService.Compile` 抛出异常
- **THEN** 创建 `Build` 步骤，`Status=Failed`，`Message=ex.Message`，设置 `FinishedTime`，`AppPipelineRun.BuildFinishedTime` 也被设置

#### Scenario: 多节点部署，部分成功部分失败

- **WHEN** 流水线配置了 3 个部署节点，节点 1 成功、节点 2 失败
- **THEN** 创建 3 条 `Deploy` 步骤
  - 节点 1：`Status=Success`
  - 节点 2：`Status=Failed`，`Message=ex.Message`
  - 节点 3：不尝试，保持未执行？→ **当前设计**：循环执行，前面失败不阻止后续，所以节点 3 仍会执行
  - 每个步骤都有独立的 `StartedTime` / `FinishedTime`

#### Scenario: 部署节点被跳过

- **WHEN** `dn == null || !dn.Enable || dn.DeployId != pipeline.DeployId`
- **THEN** 创建 `Deploy` 步骤，`Status=Skipped`，`Message` 说明跳过原因（"节点不存在" / "节点未启用" / "DeployId 不匹配"）

#### Scenario: 编译成功，写入步骤

- **WHEN** `_deployService.Compile` 成功返回
- **THEN** 创建 `Build` 步骤，`Status=Success`，设置 `StartedTime` / `FinishedTime`

## MODIFIED Requirements

### Requirement: 流水线整体时间记录

**原**：只有成功路径才设置 `BuildFinishedTime` / `DeployFinishedTime`

**改**：无论成功失败，只要进入该阶段就会设置开始时间，退出该阶段（无论成功失败）就会设置完成时间。

## REMOVED Requirements

无。
