# PUB-23 流水线部署流程

> 版本：v1.2 | 日期：2026-11-24（v1.1 于 2026-08-09，v1.0 于 2026-08-06）
> 对应需求：流水线编译上传后自动部署
> v1.1 修订：移除"未配置节点时自动回退全部启用节点"，改为仅部署勾选节点、不回退、未勾选则标记 Failed（克制原则：一条流水线对应一个分支）
> v1.2 修订：触发统一入队（同流水线串行）；按触发提交构建；产物版本与运行绑定；超时自愈；开放运行取消；构建日志回显；支持手动指定分支/提交与标签触发

---

## 功能说明

流水线（AppPipeline）把「代码提交 → 编译 → 上传 → 部署」串成自动流程：

1. 监听 Git 仓库 webhook、标签推送或用户手动触发，创建**排队记录**（Pending）。
2. 后台服务 `PipelineRunner` 按流水线串行派发；同一流水线同时只允许一个活动运行。
3. 向编译节点下发 `deploy/compile/Build-Upload` 命令，携带**触发分支与提交号**。
4. 编译节点拉代码、检出指定提交、编译、打包、上传 zip 到 StarServer。
5. StarServer 收到编译命令回包后，按**提交号 + 构建开始时间**匹配本次运行产出的版本，并设为当前版本。
6. 若开启自动部署，向目标节点下发 `deploy/install`。
7. 运行超时（排队/构建/部署）由后台服务兜底标记失败；用户可随时取消未到终态的运行。

---

## 触发方式

| 方式 | 入口 | 说明 |
|------|------|------|
| Git 分支推送 | `POST /Deployment/Pipeline/Webhook?token=` | 分支精确或前缀通配（`release/*`） |
| Git 标签推送 | 同上 | 流水线分支配置 `tag:` 前缀后启用，如 `tag:v*`、`tag:v1.0.0` |
| 手动触发 | 发布中心「启动」/ `GET /Deployment/AppPipeline/Trigger?id=` | 默认使用流水线分支最新提交，也可指定 `branch`、`commit` |
| 重跑 | 运行列表「重跑」/ `GET /Deployment/AppPipelineRun/Reprocess?id=` | 沿用原运行的分支与提交 |

Webhook 与手动触发都只创建排队记录并立即返回（`{result:"queued", runId}` / 「流水线已排队，运行编号[N]」），真正编译由后台服务派发。

---

## 涉及实体

| 实体 | 说明 |
|------|------|
| AppPipeline | 流水线配置：关联应用部署集、编译节点、部署节点、分支、自动部署开关 |
| AppPipelineRun | 一次流水线运行记录，含状态、版本、提交信息、起止时间 |
| AppPipelineStep | 单次运行中的步骤（Build / Deploy），记录命令Id与状态、日志尾部 |
| AppBuildNode | 编译节点配置 |
| AppDeployNode | 部署节点配置 |
| AppDeployVersion | 上传生成的应用版本（含提交号） |
| NodeCommand | StarServer 下发给节点的命令 |
| AppDeployHistory | 部署历史日志 |

---

## 状态机

```
Pending（排队）
   |
   v  PipelineRunner 条件更新抢占（同流水线串行）
Building
   |
   +-- 构建成功回包 --> Success（AutoDeploy=false）
   |                 \-> Deploying --> Success
   +-- 回包错误/命令下发失败 --> Failed
   +-- 排队/构建/部署超时 --> Failed（PipelineRunner 兜底）
   +-- 用户取消 --> Cancelled

注：UploadSucceeded 为历史预留状态，当前实现直接由 Building 进入 Deploying/Success。
```

---

## 并发与排队规则

1. 每个流水线维护一个待执行队列，按运行 Id 升序派发。
2. 同一流水线存在 `Building/Deploying` 的运行时不派发下一条（串行）；不同流水线可并行。
3. 派发前以数据库条件更新 `Pending -> Building` 抢占，多个宿主实例同时运行也不会重复下发。
4. 队列长度由 `PipelineQueueLength` 限制（默认 20），超限时 webhook 返回 `rejected: queue full`，手动触发返回错误。
5. 编译节点 `DeployAgent` 内部串行执行编译命令，避免同一源码目录并发 `git pull` / `publish` 清空互相破坏。

---

## 提交级构建

- webhook 触发时，运行记录保存推送的分支与提交号；下发编译命令时一并传给编译节点。
- 编译节点优先 `git fetch --depth 1 origin <commit>` 后 `git checkout --force <commit>`，并校验 `HEAD` 与目标提交一致；服务端不支持按 SHA 获取时回退拉取分支。
- 手动触发可指定 `branch` / `commit`；留空时使用流水线具体分支（通配符或 `tag:` 规则回退部署集分支）的最新提交。
- 分支名带通配符（`release/*`）或标签规则（`tag:v*`）时，**构建分支**取部署集 `AppDeploy.Branch`，构建提交取触发事件的提交号。

---

## 产物版本归属

编译成功后不再简单取“部署集最新版本”，而是：

1. 取构建步骤开始时间（允许 5 分钟时钟偏差）之后创建的版本；
2. 运行有提交号时，优先匹配 `AppDeployVersion.CommitId`（支持长短号前缀互匹）；
3. 无提交号的版本（非 git 源码目录）仅在全部候选都无提交号时接受；
4. 附注标签推送的 `after` 是标签对象 SHA，与编译节点读到的提交 SHA 不同；构建窗口内仅有一个候选时视为本次产物；
5. 找不到匹配版本时：AutoDeploy=false 仍按成功结束；AutoDeploy=true 标记 Failed（无法安全部署）。

---

## 超时与自愈

| 配置项 | 默认 | 说明 |
|--------|------|------|
| `PipelineQueueEnabled` | true | 关闭后只入队不自动派发 |
| `PipelineQueueLength` | 20 | 单流水线排队上限，0 不限制 |
| `PipelineQueueTimeout` | 3600 秒 | 排队超时 |
| `PipelineBuildTimeout` | 4200 秒 | 构建超时（命令超时 3600 + 600 余量） |
| `PipelineDeployTimeout` | 1800 秒 | 部署超时 |

- `PipelineRunner` 每 10 秒扫描一次，超时的运行置 `Failed`，其 `Running` 步骤同步置 `Failed`。
- `NodeCommand.CancelExpired` 取消过期命令时，联动失败对应流水线步骤与运行，避免节点离线后永久停在 `Building`。

---

## 取消运行

- 入口：Web / WebApi `GET /Deployment/AppPipelineRun/Cancel?id=`、Vue 运行列表「取消」、运行详情抽屉「取消运行」、MCP `stardust_pipeline_cancel`。
- 语义：**逻辑取消**。运行置 `Cancelled`，不再续跑、不再自动部署；已下发到节点的构建/部署进程不会被强制中断。
- 仅未到终态（Success/Failed/Cancelled）的运行可取消。

---

## 构建日志

- 编译命令回包（成功或失败）都会把日志尾部写入 `AppPipelineStep.Message`（列宽 2000，保留尾部）。
- 编译节点还通过事件上报 gitclone/build/package/done 阶段日志到部署历史，便于排查。
- 完整构建日志不落库，只保留尾部；需要长期保留时请在编译节点侧收集。


## 完整流程

```
用户/Webhook/标签
    |
    v
PipelineService.Trigger / HandleWebhookAsync
    |  仅创建 Pending 排队记录（不直接编译）
    v
PipelineRunner（每 10 秒）
    |  1) 清理超时运行  2) 同流水线串行派发
    v
PipelineService.RunAsync
    |
    v
DeployService.Compile 下发 deploy/compile/Build-Upload（带 Branch/CommitId）
    |                              ^
    |                              |
    v                              |
DeployAgent.OnCompile（节点内串行） |
    |                              |
    +-- Git 拉取                   |
    +-- 检出指定提交并校验 HEAD    |
    +-- 编译项目                   |
    +-- 打包 zip                   |
    +-- UploadBuildPackageSync     |
        |                          |
        v                          |
    StarServer DeployController.UploadBuildFile
        |                          |
        +-- 保存附件                |
        +-- 创建/更新 AppDeployVersion（带提交号）
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
        +-- 按提交号+构建开始时间匹配本次版本
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
| 编译成功但 AutoDeploy=false | run -> Success，Remark=自动部署未开启 | pipeline/autoDeploy 成功 |
| 编译成功且 AutoDeploy=true，但未匹配到本次版本 | run -> Failed，Remark=未找到本次构建产物版本 | pipeline/autoDeploy 失败 |
| 编译成功且 AutoDeploy=true，匹配到版本 | run -> Deploying，调用 DispatchDeployAsync | pipeline/autoDeploy 成功 |
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
| `Stardust.Web/Services/PipelineService.cs`（WebApi 同源） | 排队的创建/派发、并发与超时控制、运行取消、提交级构建参数 |
| `Stardust.Web/Services/PipelineRunner.cs` | 后台服务：周期派发排队运行、超时自愈 |
| `Stardust.Web/Services/DeployService.cs` | 编译/发布控制封装（支持 branch/commit 覆盖） |
| `Stardust.Server/Services/NodeService.cs` | 命令回包处理、版本匹配、流水线续跑、自动部署下发 |
| `Stardust.Server/Controllers/DeployController.cs` | `UploadBuildFile` 接口：接收 zip 并创建版本 |
| `Stardust.Data/Nodes/节点命令.Biz.cs` | 命令过期时联动失败流水线步骤与运行 |
| `DeployAgent/DeployService.cs` | 编译节点执行：拉代码、检出提交、编译、打包、上传（节点内串行） |

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

### 推送后运行一直停在 Pending

**原因**：流水线队列串行执行，同一流水线前一个运行尚未结束；或 `PipelineQueueEnabled=false`。

**排查**：查看同流水线是否存在 `Building/Deploying` 的运行；确认平台设置 `PipelineQueueEnabled` 为 true。

### 运行一直停在 Building/Deploying

**原因**：编译/部署节点离线，命令回包不会到达。

**处理**：`PipelineRunner` 会在超过 `PipelineBuildTimeout`/`PipelineDeployTimeout` 后自动置为 Failed；
`NodeCommand.CancelExpired` 也会在命令过期时联动失败步骤与运行。也可手动「取消」运行。

### 取消运行后节点仍在构建

**说明**：取消是逻辑取消，只阻止后续续跑与自动部署，不会强制中断节点上已开始的编译/部署进程。

