# GITEA-1 流水线基础设施方案规划

> 版本：v1.0 | 日期：2026-08-15
> 对应需求：本机部署 Gitea + Gitea Actions Runner 做流水线构建；部署独立镜像库；对接 Stardust Flows（K8sDeployExecutor）打通"触发→构建→推制品→k8s 部署"完整流程。
> 关联文档：[K8S-3-k8s化架构](K8S-3-k8s化架构.md) · [K8S-2-流水线K8s部署使用指南](K8S-2-流水线K8s部署使用指南.md) · [GITEA-2-部署步骤](GITEA-2-部署步骤.md)
> 定位：本机开发/验证用 CI/CD 基础设施规划。**只组织已定设计与推荐方案，关键岔路口见 §6 待确认项**，落地前需笑笑确认。

---

## 1. 目标与范围

| 目标 | 说明 |
|------|------|
| 部署 Gitea | 本机自托管 Git 服务，托管源码仓库 |
| 部署 Gitea Actions Runner | 执行流水线构建任务（`act_runner`，注册到 Gitea） |
| 部署镜像库 | 独立容器镜像/制品仓库（非 Gitea 内置），存构建产物与基础镜像 |
| 打通完整流程 | Gitea 触发 → `dotnet publish` 构建 → 推制品到镜像库 → k8s 拉取部署（本机 Rancher Desktop k3s） |
| 约束：**不用真 Docker 打包** | 流水线**不** `docker build` 应用镜像；构建只产出"发布产物压缩包"，以 OCI 制品方式入镜像库 |

**范围外**：生产级高可用、HTTPS 证书、Gitea 与 Stardust 用户体系打通、多 runner 集群、镜像库鉴权加固（本机验证阶段匿名/弱鉴权）。

---

## 2. 关键约束与现状

- **Docker 守护进程现状**：本机已装 Rancher Desktop，`docker compose` CLI 存在，但 **Docker daemon 未运行**（连不上 `npipe://./pipe/docker_engine`）。→ 落地前必须先启用 Docker 运行时（§6 Q1）。
- **act_runner 依赖 Docker**：`act_runner` 基于 `act`，执行 job 步骤时需在容器内运行（即便是 `dotnet publish` 也要一个 `.NET SDK` 容器提供环境）。因此**本方案整体上无法完全脱离 Docker**，"不用 Docker 打包"仅指**不构建应用镜像**，runner 的任务容器仍走 Docker。
- **本机已具备**：.NET SDK 10.0.302（runner 宿主机自带，可用于排查/直跑）、可达的 k3s（Rancher Desktop）、`kubectl`、`sqlite3`。
- **端口现状**：3000/222/5000/8080 均空闲，无相关容器残留。

---

## 3. 总体架构

```
┌──────────────────────────────────────────────────────────────┐
│ 开发者 / Stardust 流水线触发                                    │
└───────────────┬──────────────────────────────────────────────┘
                │ git push / webhook / API
                ▼
┌──────────────────────────┐         ┌──────────────────────────┐
│  Gitea (3000/222)         │ 注册    │  act_runner (容器)         │
│  - 仓库 / Actions 引擎     │◄────────│  - 监听 Gitea 任务          │
│  - 用户信息(本机)          │  API    │  - 起 .NET SDK 容器跑 job   │
└──────────────────────────┘         └───────────┬──────────────┘
                                                  │ docker(任务容器)
                                                  ▼
                                     ┌────────────────────────────┐
                                     │  job: dotnet publish         │
                                     │  → tar 产物                  │
                                     │  → oras push 制品            │
                                     └───────────┬─────────────────┘
                                                  │ OCI 制品 push/pull
                                                  ▼
┌──────────────────────────┐         ┌────────────────────────────┐
│  k3s (本机 Rancher)        │  apply   │  镜像库 registry:2 (5000)   │
│  Deployment/Service        │◄────────│  - 存 OCI 制品(构建产物)     │
│  initContainer 拉制品       │  pull   │  - 存基础运行时镜像          │
│  main=dotnet/aspnet 跑应用  │         └────────────────────────────┘
└──────────────────────────┘
                ▲
                │ HTTPS(k8s REST)
┌───────────────┴──────────────────────────────────────────────┐
│  Stardust Flows（K8sDeployExecutor 制品模式）                   │
│  PipelineTrigger → Build(调 Gitea 触发/拉制品) → Deploy(apply) │
└──────────────────────────────────────────────────────────────┘
```

> Stardust.Flows 侧的对接点：现有 `K8sDeployExecutor.BuildAsync` 走 `dotnet publish + docker build + push`；本方案改用"制品模式"——构建产物经 **Gitea Actions** 完成并推到镜像库，`K8sDeployExecutor` 仅负责**部署**（apply 一个以基础运行时镜像 + init 容器拉制品的 Deployment）。构建权从 Flows 内 `DotNetBuilder` 移交给 Gitea Runner。

---

## 4. 组件清单与推荐选型

| 组件 | 推荐 | 说明 |
|------|------|------|
| Gitea | 官方镜像 `gitea/gitea` + Docker Compose | 单实例；DB 默认内置 SQLite（本机验证够用，见 Q2 备注） |
| Actions Runner | 官方 `gitea/act_runner` 容器 | 挂载宿主 `docker.sock` 以起任务容器 |
| 镜像库 | 官方 `registry:2`（Docker Distribution） | 轻量独立仓库；匿名 pull + 弱 push 鉴权（本机） |
| 制品推送工具 | `oras` CLI | 将 tar 包作为 OCI artifact 推/拉到 registry:2（避免 docker build） |
| 任务容器镜像 | `mcr.microsoft.com/dotnet/sdk:10.0` | runner 内跑 `dotnet publish` |
| 部署基础镜像 | `mcr.microsoft.com/dotnet/aspnet:10.0` | k8s 内运行应用，制品由 init 容器注入 |
| init 拉制品镜像 | `curlimages/curl`（或含 oras 的轻量镜像） | 从 registry:2 匿名拉取制品 tar 包到 emptyDir |

---

## 5. 制品模式（核心设计，对应"不用 Docker 打包"）

### 5.1 构建侧（Gitea Actions）
1. `actions/checkout`
2. `dotnet restore` + `dotnet publish -c Release -o out`
3. `tar czf app.tgz -C out .`
4. `oras push registry:5000/<repo>/<app>:<version> app.tgz`

> 全程不出现 `docker build`/`docker push` 应用镜像。

### 5.2 部署侧（k3s，Stardust Flows 制品模式）
- `Deployment` 结构：
  - `initContainers[0]`：`curlimages/curl` 从 `registry:5000/<repo>/<app>:<version>` 拉取 tar 包，解压到 `emptyDir:/app`。
  - `containers[0]`：`mcr.microsoft.com/dotnet/aspnet:10.0`，`workingDir:/app`，`command: ["dotnet","<app>.dll"]`，挂载同一 `emptyDir`。
- 镜像引用 = 固定基础运行时镜像 + 制品版本（通过 init 容器参数传入），`K8sDeployExecutor` 写 `AppDeployVersion.Image` 为**基础镜像**（制品版本存 `ImageTag`/`Url` 指向制品）。

### 5.3 与现有 Flow 字段映射
| Stardust 字段 | 制品模式取值 |
|------|------|
| `AppDeployVersion.Image` | 基础运行时镜像 `mcr.microsoft.com/dotnet/aspnet:10.0` |
| `AppDeployVersion.ImageTag` | 应用版本号 |
| `AppDeployVersion.Registry` | `registry:5000` |
| `AppDeploy.Url`（原 zip） | 复用为**制品 OCI 引用** `registry:5000/<repo>/<app>:<version>` |

---

## 6. 待确认项（落地前需笑笑拍板）

- **Q1 Docker 运行时**：Rancher Desktop 切到 `dockerd(Moby)` 运行时 / 安装 Docker Desktop / 其它安排？
- **Q2 Gitea 数据库**：内置 SQLite（最简，本机验证）/ 外接 Postgres（更接近生产）？
- **Q3 镜像库选型**：独立 `registry:2`（推荐）/ Gitea 内置 OCI 仓库 / Harbor（偏重）。
- **Q4 部署消费方式**：制品模式（base 镜像 + init 拉制品，真·无镜像构建，推荐）/ 仍构建轻量应用镜像（需 runner 支持 docker build，违背"不用 Docker 打包"）/ 仅验证链路用现成镜像。

> 约束收敛：在当前"不用真 Docker 打包"前提下，推荐 Q4=制品模式。其余三项默认取推荐值，可在确认时一并定。

---

## 7. 端口与数据目录规划

| 服务 | 容器端口 | 宿主机端口 | 数据卷（建议放仓库外 `D:\ci\`） |
|------|----------|------------|----------------------------------|
| Gitea HTTP | 3000 | 3000 | `D:\ci\gitea\data` |
| Gitea SSH | 22 | 222 | 同上 |
| registry | 5000 | 5000 | `D:\ci\registry\data` |
| act_runner | — | — | 注册令牌存 `D:\ci\runner\` |

> 数据一律放 `D:\ci\`（仓库之外），不进 git；若放仓库内需加 `.gitignore`。

---

## 8. 风险

- Docker 守护进程未运行 → 必须先解决（Q1），否则 runner 无法起任务容器。
- `registry:2` 匿名 push 须显式配置（`REGISTRY_AUTH` 关闭），否则 push 被拒。
- init 容器匿名拉制品需 registry 允许匿名 pull，且 `curlimages/curl` 需处理 OCI 分发（digest 取 manifest 再取 blob，建议用 `oras` 容器或脚本）。
- Gitea Actions 语法与 GitHub Actions 近似但有差异，workflow 需按 Gitea 文档微调。
