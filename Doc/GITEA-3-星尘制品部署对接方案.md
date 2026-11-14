# GITEA-3 — 星尘制品部署对接方案（CI 产出制品 → 星尘点即部署）

> 调研竞品 + 结合星尘现状的针对性设计。目标：打通「提交代码 → 多 runner 事件触发 → 构建打包上传 Docker 镜像 → 星尘感知版本 → 点即部署到 StarAgent/k3s」最小闭环。
> 本轮更新要点（2026-08-16 晚）：① 明确**制品 = Docker 镜像**（版本类型=Docker镜像，版本号=digest 钉死 / tag 展示）；② CI 事件源**多 runner 可插拔**（自研/Gitea/GitHub/GitLab），**预留自研 runner 接口**；③ 部署目标新增 **k3s 集群**并补 **Deployment YAML 编辑**功能；④ 点击部署按节点类型分流（VM/StarAgent ↔ K8s）。
> 关联：`Doc/GITEA-1-方案规划.md`、`Doc/GITEA-2-部署步骤.md`、`Doc/K8S-功能设计.md`、`Doc/K8S-2-流水线K8s部署使用指南.md`；项目日志 `2026-08-16.md`（关键节点 1 已闭环）。

---

## 一、竞品调研结论（关键模式提炼）

| 竞品 / 模式 | 制品形态 | repo→制品 映射 | digest 发现 / 信号 | 部署触发 |
|------|------|------|------|------|
| **GitLab CI + Container Registry** | 真 Docker 镜像（`:latest` + `:sha-<commit>`） | **隐式**：registry 路径 = `registry.gitlab.com/group/project`，与 repo 同名 | 仓库自带 Packages 页；部署**钉死 digest**（`image@sha256:`）保证不可变/回滚 | 同一 pipeline 后续 job `kubectl set image` |
| **GitHub Actions + GHCR** | 真 Docker 镜像（`ghcr.io/owner/repo`） | **隐式**：package 绑定推送它的 repo | `docker/metadata-action` 打 `sha-`/`branch`/`semver` 标签；`steps.build.outputs.digest` 回显；部署钉 digest | `kubectl set image ...@<digest>` |
| **Harbor（registry）Webhook** | 镜像/OCI 制品 | project 维度 | **PUSH_ARTIFACT 事件 payload 自带 `digest` + `repo_full_name`**（`{type,event_data:{resources:[{digest,tag,resource_url}],repository:{repo_full_name}}}`）—— 这是最干净的「推送即带 digest」信号 | 外部监听端点收到 webhook 后触发构建/部署 |
| **OCI Artifacts / ORAS（Referrers API）** | 任意 blob（artifact manifest，`artifactType` + `subject`） | `oras push --artifact-type <type> repo:v1 file` | **Referrers API** `GET /v2/{repo}/referrers/{digest}` 可发现某 digest 下挂的所有制品（SBOM/签名/构建包），无需 manifest 也能列清单 | 同容器生命周期，消费者按 digest 拉 |
| **Gitea 内置 Package Registry** | 含 Container/OCI/npm 等 | **隐式**：Gitea 已知哪个 repo 推送 | Gitea 自带 Packages + webhook | 可复用 Gitea Actions webhook |
| **GitOps（ArgoCD/Flux）** | 镜像（pull 模型） | Git 仓即真相源 | 监听 Git/manifest 变更；Image Updater 看 registry 标签 | 自动同步，无需手动点 |

**提炼出的 4 条可复用原则：**
1. **repo→制品映射最好隐式**（GitLab/GHCR/Gitea 内置 registry 都把 registry 命名空间绑到 repo）。星尘的 App 模型独立于 Gitea repo，所以星尘侧必须做**显式映射**（`AppDeploy.GitRepository`）。
2. **部署必须钉 digest**（GitLab/GitHub 都强调 `image@sha256:` 而非浮动 `:latest`）—— 我们本就用 blob digest 寻址，天然契合。
3. **制品就绪信号要带 digest**（Harbor PUSH_ARTIFACT 是范本）。信号来源只有两个：① 构建侧回调（workflow 已知 digest）；② registry webhook（需 Harbor/zot 级 registry）。
4. **「刷新查看镜像」需要可发现性**（OCI Referrers / registry catalog）。裸 blob（无 manifest）无法被 catalog 列出 → 要么补 manifest（OCI artifact），要么靠回调把 digest 主动告诉星尘。

---

## 二、关键区别澄清（先讲清，避免混淆）

用户原始设计说「githook 通知到星尘，根据仓库找到应用，可刷新仓库地址查看镜像」。

> **第一性原理区分两个事件：**
> - **代码推送 webhook（Gitea push）**：发生在「构建之前」，此时**还没有制品、没有 digest**。它只能告诉星尘「repo X 有个 push，构建开始了」。
> - **制品就绪信号（artifact-ready）**：发生在「构建+推送完成之后」，此时**才有 digest**。它是「可部署制品」的权威信号。
>
> Gitea 的 push webhook **带不出 digest**（构建还没跑）；Harbor 的 PUSH_ARTIFACT 能带，是因为 Harbor 是 *registry* 级事件、推送完成才触发。
> **因此「githook → 查看镜像」要成立，制品就绪信号必须从构建侧（workflow 回调）或 registry 侧（Harbor 级 webhook）发出，而不是从代码 push webhook。** 本方案以 **workflow 回调** 作为制品就绪的权威信号；Gitea push webhook 仅用作「构建中」状态提示（可选）。

---

## 三、星尘现状约束（必须适配，不能硬套竞品）

- **部署实体已存在**：`AppDeploy`（部署集）、`K8sCluster`、`AppDeployVersion`（版本记录 `Image/ImageTag/Registry`）、`PipelineTrigger`（编排）、`K8sDeployExecutor`（构建+部署）、`DeploymentManifestBuilder`、`DirectK8sOperator`（apply k3s）、`DeployMethod` 枚举（K8s / VM …）。
- **`K8sDeployExecutor.BuildAsync` 当前强耦合真 Docker 镜像**：`dotnet publish → docker build → docker push → image=registry/app:tag`；`DeployAsync` apply `image=context.Image` 的 Deployment。**不支持「按 digest 拉 tar 包 blob 跑成应用」** —— 这是 P3 根因。
- **registry:2 限制**：本环境 `registry:2` 拒 OCI *image* manifest（`MANIFEST_INVALID`）；无 webhook 能力。所以：① 制品以**裸 blob + digest** 存储（已验证，见 `D:\ci\artifact.sh`）；② 不能靠 registry catalog/referrers 发现；③ 不能靠 registry webhook 发信号。
- **网络限制**：容器裸奔不出企业网，但 `host.docker.internal` 回环到本机 Gitea/registry 不受限（已验证）；运行时不能 `apt-get`。
- **Gitea Actions 限制**：github.com 不可达 → 不能用 `actions/checkout` 等官方 action，必须纯 `run:` + Gitea 自带 `GITHUB_TOKEN` 克隆；secrets API 用 `PUT`。

---

## 四、方案设计（针对性，最小闭环优先）

### 4.0 CI 事件源抽象（多 runner 可插拔，预留自研）

提交代码 → **事件触发**是整个链路的入口。设计上把「谁构建」与「构建后通知什么」解耦：

- **统一事件契约（runner 无关）**：无论哪种 runner，构建成功后的产物都是「一个 Docker 镜像引用 + digest + tag + commit」。星尘只认这份契约，不关心 runner 是谁。
- **可插拔 runner 清单**（当前打通 Gitea，其余预留）：
  | runner | 状态 | 触发方式 | 备注 |
  |------|------|------|------|
  | **自研 Runner** | **预留接口** | 未来自研 agent 拉取任务执行 | 抽象 `ICiRunner`/`ICiEventSource`，先定义接口与事件模型，不实现 |
  | **Gitea Runner** | ✅ 当前打通 | `act_runner` + Gitea Actions | 本环境已验证（see `D:\ci`） |
  | **GitHub Actions** | 预留 | workflow `push` | 换 provider 即可 |
  | **GitLab Runner** | 预留 | `.gitlab-ci.yml` | 换 provider 即可 |
- **统一入口**：所有 runner 最终都向星尘 `POST /api/deploy/artifact`（见 4.1）投递同一份 payload。新增 runner = 新增一个 provider 适配（产出同样 payload），**星尘侧零改动**。
- **预留自研 runner 的动作**：在 `Stardust.Data` 预留 `CiProvider` 枚举（`Self/Gitea/GitHub/GitLab`）+ `AppDeploy.CiProvider` 字段，记录该应用由哪个 runner 构建（当前可不填，默认 Gitea）；自研 runner 的拉取/回报协议后续单独设计，接口先占位。

> 第一性原理：提交即事件、构件即镜像、信号即 digest——这三件事与 runner 实现无关，所以把它们抽成稳定契约，runner 只是「事件产生器」。这也让未来替换/新增 CI 不波及星尘部署核心。

### 4.1 信号投递（制品就绪）
- **采用 workflow 回调**（零基建，digest 在 `artifact.sh` 推完即打印到 stdout）：
  `build.yml` 末步解析 digest，向星尘 `POST /api/deploy/artifact` 发：
  ```json
  { "repo_full_name": "stardust/stardust-flows-demo",
    "digest": "sha256:25d4fadf...",
    "tag": "main", "commit": "<sha>", "branch": "main", "built_at": "<iso>" }
  ```
  负载形状**对齐 Harbor PUSH_ARTIFACT**（`repo_full_name` + `digest` + `tag` + `resource_url`），未来若换 Harbor/zot 级 registry 可无缝替换。
- **可选**：Gitea push webhook → 星尘标记「构建中」状态（非必需，闭环不依赖）。

### 4.2 repo→应用映射（显式）
- `AppDeploy` 新增字段 `GitRepository`（如 `stardust/stardust-flows-demo`）；分支复用既有 `Branch` 字段（默认 `master`），不单列 `GitBranch`。
- 回调按 `GitRepository` 查 `AppDeploy`（1 个 AppDeploy ↔ 1 个 repo，先 1:1；后续可扩展多 repo）。

### 4.3 版本模型（类型 = Docker 镜像；版本号 = digest 钉死 / tag 展示）

> 用户原问：「更新应用的版本，版本类型为 docker 镜像，版本号是多少？」—— 答案如下。

- **版本类型 `Kind`**：复用 `AppDeployVersion`，新增字段 `Kind`（0=应用包/zip，1=Docker 镜像）。本链路产生的版本 `Kind=1`，与原 zip 包版本并存不冲突（VM 路径只读 zip，K8s 路径只读 Image）。
- **版本号 `Version` 取什么**：
  - **落库身份 = digest 钉死**：`AppDeployVersion.Image = registry/repo@sha256:<digest>`（不可变、可回滚，对齐 GitLab/GitHub 最佳实践）。本环境 registry:2 拒 OCI manifest，制品以裸 blob + digest 存储，其引用恰为 `repo@sha256:D`，天然就是「钉 digest」。
  - **展示号 = tag / 可读串**：`AppDeployVersion.Version` 存人类可读号（如 `main`、`commit短sha`、`语义版本 1.0.0`）；`ImageTag` 存 tag；`Registry` 存仓库地址。部署时以 `Image`（含 digest）为准，展示以 `Version`/`ImageTag` 优先。
  - 结论：**版本号本质是「镜像引用」—— 部署钉 digest，展示用 tag**。
- **镜像三种来源（都可落到同一条 `AppDeployVersion`）**：
  1. **回调自动**（主链路）：workflow 构建成功 → `POST /api/deploy/artifact` → 写 `Image`(digest)/`ImageTag`/`CommitId`/`Kind=1`/`Enable=true`（见 4.1）。
  2. **查询镜像库最新**（完善项 R3）：星尘调 registry（Harbor/zot 级）`GET /v2/{repo}/tags/list` 或 Referrers API，回填最新 `Image`/`ImageTag`。本环境 registry:2 无此能力，先占位。
  3. **手动填写**（当前打通链路用）：UI 表单直接填 `Image`(含 digest 或 tag) + `Version`，存为 `Kind=1` 版本。**本地测试即走这条**：手工把已就绪镜像写进 `AppDeployVersion`，即可触发部署闭环。
- **latest 指针**：`AppDeploy.Version` 仍指向当前生效版本号；新增「最新镜像」视图 = 该 AppDeploy 下 `Kind=1` 且 `Enable` 的最新一条 `AppDeployVersion`。

> 注：此前草案的 `AppArtifact` 独立实体与 `AppDeployVersion` 职责重叠，本版**折叠进 `AppDeployVersion`（加 `Kind`）**，避免双份版本表；历史 digest 列表由 `AppDeployVersion` 按 `Kind=1` 过滤即得。

### 4.4 部署落地（Docker 镜像制品 → k8s Pod）

「Docker 镜像制品」如何变成运行的 Pod，取决于 registry 能力：

- **生产形态（Harbor/zot 等真镜像仓库）**：`AppDeployVersion.Image` 是 `registry/app:tag`（含 digest）。`K8sDeployExecutor.DeployAsync` 直接 apply Deployment，`image=Image`，k8s 正常拉镜像。**无需 init 容器**。
- **本环境形态（registry:2 拒 OCI manifest）**：制品以 `repo@sha256:D` 裸 blob 存储（不是真 OCI 镜像），k8s 不能直接 `pull` 它。落地方式 = **base 运行时 + init 容器拉 blob**（与 2026-08-16 早班设计一致）：
  - `DeploymentManifestBuilder` 增加重载 `Build(deploy, imageRef, ns, replicas, ports, envs, cpu, mem)` 的「artifact 模式」分支：
    - `initContainers`：`curlimages/curl` 拉 `registry/repo@sha256:D` → 解 tar.gz 到 `emptyDir` 卷 `/app-data`。
    - `containers[0]`：`image = mcr.microsoft.com/dotnet/aspnet:10.0`，挂 `/app-data → /app`，`command ["dotnet","App.dll"]`。
    - `volumes`：`emptyDir` 共享。
  - 触发：`K8sDeployExecutor.DeployAsync`（或新增 `ArtifactDeployExecutor`）经 `DirectK8sOperator` apply + wait ready；回写 `AppDeployVersion.Image = repo@sha256:D`。
  - 复用 `PipelineTrigger`（`DeployMethod.K8s` 分支，经 `DeployExecutorFactory` 选择；`DeployMethod` 已在 `AppDeploy` 存在）。

> 抽象统一：无论哪种形态，星尘内部都叫「Docker 镜像制品」，`Kind=1`、`Image` 存引用。差异只在「apply 时 image 是否可被 k8s 直接 pull」——由 registry 能力决定，对上层的版本/部署概念无侵入。

### 4.5 Deployment YAML 编辑功能（新增，补全 K8S 表单式配置的不足）

现有 K8S 文档（K8S-功能设计 / K8S-2）的 k8s 配置是**表单式**（`DeploymentName`/`ImageTagRule`/`Replicas`/`CpuLimit`/`K8sPorts`/`K8sEnv` 等字段），**无 raw YAML 编辑**。补一个「Deployment 文件编辑」能力：

- **新增字段** `AppDeploy.DeploymentYaml`（Text，-1 长度，可空）：存放用户手写的 Deployment 清单覆盖。
- **行为**：
  - 为空 → 沿用表单式：`DeploymentManifestBuilder.Build(...)` 由 `AppDeploy` 字段渲染（现状）。
  - 非空 → **以 YAML 为准**：`DeploymentManifestBuilder` 优先解析 `DeploymentYaml`（反序列化 → `Deployment`），仅把 `image` 强制替换为 `AppDeployVersion.Image`（保证版本钉死），其余用户自定（resources/ports/env/probes/volumes 随意）。
- **UI**：AppDeploy 编辑页「K8s 配置区」增加「Deployment YAML（高级覆盖）」多行文本框；提供「生成默认 YAML」按钮（由表单值渲染预览），用户可改后保存。
- **校验**：保存时轻量校验 YAML 合法性（k8s 字段名 / `apiVersion: apps/v1` / `kind: Deployment`）；不强校验集群可达。
- 与现有 `K8sPorts`/`K8sEnv` 等表单字段**并存**：填了 YAML 就忽略表单的 ports/env（避免双源冲突），UI 提示「YAML 优先」。

### 4.6 点击部署 → 按节点类型分流（闭环收口）

- 用户在 AppDeploy 点「部署 / 启动」→ 星尘原动作下发指令（与原 StarAgent 部署同源）。
- **判断 `DeployMethod`**（已存在于 `AppDeploy`）：
  - `VM/StarAgent`（默认）→ 原流程 `deploy/install` → 目标 StarAgent 回传状态（字节级不变）。
  - `K8s` → 走 `K8sDeployExecutor`（Flows）→ apply Deployment → watch Ready → 回写（见 K8S-功能设计 F9/F12）。
- 部署目标列表：VM 模式显示 **StarAgent 节点**（`AppDeployNode.TargetType=0`）；K8s 模式显示 **k3s 集群**（`K8sCluster` + `AppDeployNode.TargetType=1`）。即用户所见「节点」随部署方式切换为「集群」。
- 至此与 `Stardust.K8s.Flows` 的 `PipelineTrigger`/`K8sDeployExecutor` 完全对齐——点击启动即进入 flows 的 K8s 部署功能。

> 注：`NodeService.DispatchDeployAsync` 当前**尚未**按 `DeployMethod` 路由到 `K8sDeployExecutor`（K8S-功能设计 的 Phase 2 接线仍待落地，属「完善」）。本地最小闭环验证可先直连 `K8sDeployExecutor.DeployAsync`（绕过 NodeService 接线），证明 k8s 部署本身可行；NodeService 接线留作下一步。

### 4.7 触发闭环（全链路）
```
提交代码 → 任一 runner 事件触发 → build.yml: dotnet publish → 打包 → 上传 Docker 镜像(registry/repo@sha256:D)
  → 回调 POST /api/deploy/artifact{repo_full_name, digest, tag, commit}
星尘: 按 GitRepository 找 AppDeploy → 写 AppDeployVersion(Kind=1, Image=digest引用, ImageTag, CommitId, Enable=true) → UI 展示[部署]
用户点击[部署] → 判断 DeployMethod:
  VM   → deploy/install → StarAgent 回传
  K8s  → K8sDeployExecutor(Flows) → apply Deployment(image=版本) → watch Ready → 回写  → 闭环
```
> 构建日志实时回流（用户提及的「构建期间了解构建过程」）**预留动作**，本期不做；当前只保证「打包成功 + 产生镜像」信号到达。预留 `ICiRunner`/`ICiEventSource` 接口承接未来日志流。

---

## 五、最小闭环拆解（先完成）

| 编号 | 内容 | 归属 | 是否首次动作 | 可逆性 |
|------|------|------|------|------|
| **M1** | Gitea 侧：`build.yml` 末步加回调 POST digest；可选配 push webhook | Gitea / `D:\ci\artifact.sh` | 否（配置） | 可逆 |
| **M2** | 星尘侧新代码：`AppDeploy.GitRepository` + `AppDeploy.CiProvider`（预留）、`AppDeployVersion.Kind`（1=Docker镜像）、接收端点 `POST /api/deploy/artifact`、UI 展示+[部署]、Deployment YAML 编辑、artifact/镜像模式部署（`DeploymentManifestBuilder` 重载 + `K8sDeployExecutor` 分支） | Stardust 源码 | **是（首次 Stardust 源码动作）** | 代码，需评审 |
| **M3** | 本地表数据打通（本期主线）：镜像已就绪前提下，**手动维护版本数据**——直接写 `AppDeploy`（`DeployMethod=1`、`K8sClusterId`、命名空间/端口等）+ 插 `AppDeployVersion`（`Kind=1`、`Image=repo@sha256:D`、`Enable=true`）；再驱动部署流程走完（先直连 `K8sDeployExecutor.DeployAsync`，绕过 NodeService 接线） | 本地 DB + k3s | 否（数据/本地） | 可逆（清数据） |
| **M4** | 验证：点即部署 → k3s pod 起 .NET 应用，`kubectl` + HTTP 核对真实运行 | 本地 k3s | 否 | — |
| **M5** | 接线 `NodeService.DispatchDeployAsync` 按 `DeployMethod` 路由到 `K8sDeployExecutor`（K8S-功能设计 Phase 2，完善项，非本期最小闭环必需） | Stardust 源码 | 是 | 代码，需评审 |

> 执行顺序：M1（可逆，可先推）→ M2（首次代码动作，需用户授权范围）→ **M3 本地表数据打通（本期就能跑通链路）** → M4 验证 → M5 接线（完善）。
> 本期重点在 M3：不依赖 Gitea 回调也能验通「版本数据就绪 → k8s 部署闭环」，先把数据模型与部署执行跑顺，再补回调与 UI。

---

## 六、完善项（后补，不阻塞主链路）

- **R1 生产鉴权**：demo 仓库 public+匿名克隆；生产改 `GITHUB_TOKEN` 私有克隆 + 回调端点鉴权（共享 token / HMAC）。
- **R2 信号解耦**：换 Harbor/zot 级 registry，用 registry webhook 取代 workflow 回调（更解耦）；或改用 **Gitea 内置 Package Registry**（隐式 repo 映射，省掉独立 registry:2 + 显式映射）。
- **R3 可发现性**：补 OCI artifact manifest + Referrers API，支持「刷新列出多 digest 历史」；当前靠回调/手动存 latest，列表展示由 `AppDeployVersion`（`Kind=1`）历史过滤提供。
- **R4 UI 打磨**：制品历史列表、按 digest 回滚、push webhook 驱动的「构建中」状态。
- **R5 清理**：runs 6/7/8 冗余排队构建；标签/分支策略。
- **R6 安全**：回调校验 repo 归属、防伪造；registry 不暴露外网。

---

## 七、决策说明（为什么这么定）

1. **信号用 workflow 回调而非 registry webhook**：registry:2 无 webhook 能力，换 Harbor 是额外基建；workflow 推完即知 digest，零依赖。对齐 Harbor payload 形状，未来可换。
2. **不先修 OCI manifest**：registry:2 对 OCI manifest 校验严格（MANIFEST_INVALID），是 mediaType 坑；「先完成再完善」下回调比修 manifest 更简单可靠，多 digest 列表留作 R3。
3. **Docker 镜像制品模式不改 docker 构建链**：根因是部署目标模型不同（裸 blob vs 真镜像），解法在**清单构造层**（base runtime + init 容器拉 blob，或真镜像直接 pull），而非构建链。复用 `DeploymentManifestBuilder`/`DirectK8sOperator`，改动收敛。
4. **显式 repo→App 映射**：星尘 App 模型独立于 Gitea repo（竞品靠隐式绑定，星尘没有），必须显式字段；先 1:1 简单可靠。
5. **钉 digest 部署**：直接沿用竞品最佳实践（GitLab/GitHub 均强调 `image@sha256:`），不可变、可回滚。
