# PUB-23 流水线部署流程

> 版本：v1.1 | 日期：2026-08-09（v1.0 于 2026-08-06）
> 对应需求：流水线编译上传后自动部署
> v1.1 修订：移除"未配置节点时自动回退全部启用节点"，改为仅部署勾选节点、不回退、未勾选则标记 Failed（克制原则：一条流水线对应一个分支）

---

## 功能说明

流水线（AppPipeline）把「代码提交 → 编译 → 上传 → 部署」串成自动流程：

1. 监听 Git 仓库 webhook 或用户手动触发。
2. 向编译节点下发 `deploy/compile/Build-Upload` 命令。
3. 编译节点拉代码、编译、打包、上传 zip 到 StarServer。
4. StarServer 收到编译命令回包后，根据流水线设置决定是否自动部署。
5. 若开启自动部署，向目标节点下发 `deploy/install`。

---

## 涉及实体

| 实体 | 说明 |
|------|------|
| AppPipeline | 流水线配置：关联应用部署集、编译节点、部署节点、分支、自动部署开关 |
| AppPipelineRun | 一次流水线运行记录，含状态、版本、起止时间 |
| AppPipelineStep | 单次运行中的步骤（Build / Deploy），记录命令Id与状态 |
| AppBuildNode | 编译节点配置 |
| AppDeployNode | 部署节点配置 |
| AppDeployVersion | 上传生成的应用版本 |
| NodeCommand | StarServer 下发给节点的命令 |
| AppDeployHistory | 部署历史日志 |

---

## 状态机

```
Pending -> Building -> UploadSucceeded -> Deploying -> Success
   |         |            |                  |         |
   |         |            |                  |         +-- 全部 Deploy 步骤成功
   |         |            |                  +-- 已下发 deploy/install
   |         |            +-- Build 命令成功（CommandReply）
   |         +-- 已下发 deploy/compile
   +-- 刚创建 run

失败分支：
Building/Deploying -> Failed（命令回包为错误或下发失败）
Building/Deploying -> Cancelled（用户取消）
```

---

## 完整流程

```
用户/Webhook
    |
    v
PipelineService.Trigger / HandleWebhookAsync
    |
    v
PipelineService.RunAsync
    |
    v
DeployService.Compile 下发 deploy/compile/Build-Upload
    |                              ^
    |                              |
    v                              |
DeployAgent.OnCompile              |
    |                              |
    +-- Git 拉取                   |
    +-- 编译项目                   |
    +-- 打包 zip                   |
    +-- UploadBuildPackageSync     |
        |                          |
        v                          |
    StarServer DeployController.UploadBuildFile
        |                          |
        +-- 保存附件                |
        +-- 创建/更新 AppDeployVersion
        +-- 写历史 "UploadBuildFile"
        |                          |
        v                          |
    StarClient 框架自动回复 CommandReply
        |                          |
        v                          |
NodeService.CommandReply           |
    |                              |
    +-- ProcessPipelineReplyAsync  |
        |                          |
        +-- 找到 Build 步骤        |
        +-- 写历史 "编译完成"      |
        +-- 取最新 AppDeployVersion |
        +-- 使用版本（app.Version） |
        +-- 判断是否 AutoDeploy    |
            |                      |
            +-- 否 -> Success     |
            |                      |
            +-- 是 -> Deploying   |
                |                  |
                v                  |
            DispatchDeployAsync    |
                |                  |
                +-- 仅按勾选的 DeployNodeIds 创建 Deploy 步骤
                +-- 未勾选任何节点则不下发、不回退（避免误部署）
                +-- 下发 deploy/install
                |                  |
                v                  |
            StarAgent 执行安装    |
                |                  |
                v                  |
            CommandReply（Deploy） |
                |                  |
                v                  |
            ProcessPipelineReplyAsync Deploy 分支
                +-- 写历史 "部署成功"
                +-- 全部完成 -> Success
```

---

## 自动部署判定规则

在 `NodeService.ProcessPipelineReplyAsync` 中：

| 条件 | 行为 | 历史记录 |
|------|------|----------|
| 编译失败 | run -> Failed，Remark=错误信息 | - |
| 编译成功但取不到版本 | run -> Failed，Remark=未产出可部署版本 | pipeline/autoDeploy 失败 |
| 编译成功且 AutoDeploy=false | run -> Success，Remark=自动部署未开启 | pipeline/autoDeploy 成功 |
| 编译成功且 AutoDeploy=true | run -> Deploying，调用 DispatchDeployAsync | pipeline/autoDeploy 成功 |
| 无可用部署节点 | run -> Failed，Remark=未找到可部署节点 | deploy/install 失败 |
| 部分节点下发失败 | run -> Failed，Remark=部署命令下发失败 | - |
| 全部 Deploy 步骤成功 | run -> Success | deploy/install/Deploy 成功 |

---

## 部署节点选择规则（克制原则）

> **设计约束**：一条流水线对应一个分支，部署范围必须显式、可控。
> 仅部署流水线「勾选」的部署节点，**不自动回退到全部启用节点**，避免误部署到非预期环境。

`NodeService.DispatchDeployAsync` 按以下规则选择节点：

1. 仅使用 `AppPipeline.DeployNodeIds`（逗号分隔的 AppDeployNode.Id，即页面上勾选的节点）。
2. 若 `DeployNodeIds` 为空：不下发任何部署命令、不回退，仅记录 `pipeline/autoDeploy` 失败日志；上层据此将 run 标记为 `Failed`（Remark=未找到可部署节点），**不出现假成功**。
3. 以下节点会被跳过（Skipped）：
   - `AppDeployNode` 不存在
   - `AppDeployNode` 未启用
   - `AppDeployNode.DeployId` 与流水线不匹配
   - 对应 `Node` 不存在
4. 命令下发异常会标记为 Failed。

---

## 关键代码位置

| 文件 | 职责 |
|------|------|
| `Stardust.Web/Services/PipelineService.cs` | 流水线入口：创建 run、下发编译命令 |
| `Stardust.Web/Services/DeployService.cs` | 编译/发布控制封装 |
| `Stardust.Server/Services/NodeService.cs` | 命令回包处理、流水线续跑、自动部署下发 |
| `Stardust.Server/Controllers/DeployController.cs` | `UploadBuildFile` 接口：接收 zip 并创建版本 |
| `DeployAgent/DeployService.cs` | 编译节点执行：拉代码、编译、打包、上传 |

---

## 日志与排查

关键历史 Action：

| Action | 含义 |
|--------|------|
| `deploy/compile/Build-Upload` | 编译阶段完成 |
| `pipeline/version` | 取到/未取到可部署版本 |
| `pipeline/autoDeploy` | 自动部署判定与下发记录 |
| `deploy/install` | 部署命令下发/完成 |
| `deploy/install/Deploy` | 单个 Deploy 步骤成功 |

---

## 常见问题

### 上传成功但「没有触发部署」

**现象**：AppDeployHistory 中看到 `UploadBuildFile` 和 `上传成功`，但后续没有 `deploy/install` 或看到 `部署完成` 却没有实际安装。

**常见原因**：

1. 流水线未开启「自动部署」。检查 `AppPipeline.AutoDeploy`。
2. 流水线未勾选「部署节点」。当前版本遵循克制原则：未勾选任何节点则**不下发、不回退**，run 直接标记 `Failed`（Remark=未找到可部署节点），不会再出现"上传完成即假成功"的假象。
3. 配置的部署节点不存在、未启用、或 DeployId 不匹配，导致全部 Skipped（仍会下发 0 个，run 标记 Failed）。
4. 命令下发异常（如节点不在线），导致 Failed。

**排查方法**：

查看 `AppDeployHistory` 中 `pipeline/autoDeploy` 和 `deploy/install` 记录；
查看 `AppPipelineRun.Remark` 和 `AppPipelineStep.Message`。
