# PUB 远程发布 - 架构设计

> **版本**：v3.8 | **更新时间**：2026-07-29 | **文档状态**：最新
> 对应模块：PUB 远程发布
> 数据模型定义：[`Stardust.Data/Deployment/Model.xml`](../Stardust.Data/Deployment/Model.xml)

---

## 一、概述

星尘发布系统是分布式应用部署管理平台，采用**集中控制、分布式执行**架构。用户在管理控制台配置部署集、上传版本、关联节点，点击发布后 StarAgent 在目标节点上执行下载、解压、启停和守护。

### 核心特性

| 特性 | 说明 |
|------|------|
| 多版本管理 | 支持多平台（OS/Arch/TFM）自动匹配，一键回滚 |
| 分布式部署 | 单应用多节点部署，滚动发布 |
| 四种部署模式 | Standard / Shadow / Hosted / Task |
| 进程守护 | 崩溃自动重启，内存超限重启，文件变化重启 |
| SSL 证书管理 | 按域名自动匹配，支持多格式，自动续期 |
| CI/CD 流水线 | Webhook 触发 → 编译 → 打包 → 上传 → 部署 |

### 设计原则

1. **简洁性** — 字段和关联最小化，操作路径最短
2. **自动化** — 证书自动匹配、版本自动选择、流水线自动触发
3. **灵活性** — 节点级配置可覆盖部署集配置
4. **可靠性** — 进程守护、内存限制、文件监控、回滚机制

---

## 二、系统拓扑

```
┌──────────────────────────────────────────────────────────────┐
│                Stardust.Web（管理控制台）                      │
│  AppDeploy │ AppDeployNode │ AppDeployVersion │ Pipeline      │
└──────────────────────────┬───────────────────────────────────┘
                           │ HTTP API (6600)
┌──────────────────────────┴───────────────────────────────────┐
│                Stardust.Server（服务端）                       │
│                                                               │
│  ┌─ 接口层 ──────────────────────────────────────────────┐   │
│  │  DeployController  │  NodeController  │  WebSocket     │   │
│  └───────────────────────────────────────────────────────┘   │
│  ┌─ 服务层 ──────────────────────────────────────────────┐   │
│  │  DeployService（版本匹配/证书匹配/指令下发）           │   │
│  │  NodeService（节点管理/心跳监控）                      │   │
│  │  CertRenewJob（证书自动续期）                          │   │
│  └───────────────────────────────────────────────────────┘   │
│  ┌─ 数据层 ──────────────────────────────────────────────┐   │
│  │  10 张表，全部定义在 Model.xml 中                       │   │
│  │  核心：AppDeploy / AppDeployNode / AppDeployVersion     │   │
│  └───────────────────────────────────────────────────────┘   │
└──────────────────────────┬───────────────────────────────────┘
                           │ HTTP + WebSocket
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
   ┌─────────┐       ┌─────────┐       ┌─────────┐
   │StarAgent│       │StarAgent│       │StarAgent│
   │ Node A  │       │ Node B  │       │ Node C  │
   │x64/net8 │       │ARM/net10│       │x64/net10│
   └────┬────┘       └────┬────┘       └────┬────┘
        │                  │                  │
   ┌────┴────┐       ┌────┴────┐       ┌────┴────┐
   │ MyApp   │       │ MyApp   │       │ MyApp   │
   │ (SDK)   │       │ (SDK)   │       │ (SDK)   │
   └─────────┘       └─────────┘       └─────────┘
```

### 工程映射

| 工程 | 角色 |
|------|------|
| `Stardust.Data` | 数据层，Model.xml 统一定义表结构 |
| `Stardust.Server` | 服务端 API，版本匹配、证书匹配、指令下发 |
| `Stardust.Web` | Cube 管理界面，CRUD 控制器 + 视图 |
| `Stardust`（核心库） | `ServiceManager` + 部署策略 + `DeployInfo` 模型 |
| `StarAgent` | Windows 服务 / Linux systemd，执行部署 |
| `DeployAgent` | 编译节点，拉代码 → 编译 → 打包 → 上传 |

---

## 三、核心概念

### 3.1 实体关系

```
AppDeploy（部署集）           SslCertificate（证书）
  ├── AppDeployNode（1:N）       └── 通过 Domain 匹配 AppDeploy.Urls
  │     └── Node（目标机器）
  ├── AppDeployVersion（1:N）  AppPipeline（流水线）
  │     └── Attachment（zip包）   ├── AppPipelineRun（1:N）
  ├── AppDeployHistory（1:N）     │     └── AppPipelineStep（1:N）
  └── AppBuildNode（1:N）         └── BuildNode → DeployNodes
        └── Node（编译机器）
```

> 表结构定义见 [`Model.xml`](../Stardust.Data/Deployment/Model.xml)，执行 `xcode` 命令生成实体类。

### 3.2 部署集（AppDeploy）

应用的可部署单元。一个应用可有多个部署集（如 arm 版和 x64 版），每个部署集关联一组节点和多个版本。

核心配置：启动文件名、参数、工作目录、环境变量、最大内存、部署模式。

### 3.3 部署版本（AppDeployVersion）

每个部署集可上传多个版本（zip 包）。版本标注 OS / Arch / TargetFramework，服务端根据目标节点平台自动选择最佳匹配版本。

### 3.4 部署节点（AppDeployNode）

部署集与目标机器的关联。节点级可覆盖部署集的启动配置（FileName / Arguments / WorkingDirectory 等）。`Delay` 字段支持滚动发布。

### 3.5 SSL 证书（SslCertificate）

按域名管理证书。部署时通过 `AppDeploy.Urls` 自动提取域名，匹配有效期内的证书，根据节点 OS 选择 Pfx（Windows）或 Pem（Linux）格式。

### 3.6 流水线（AppPipeline）

CI/CD 自动化：Webhook 触发 → 编译节点拉代码编译打包 → 上传版本 → 自动发布到目标节点。

---

## 四、部署模式

| 模式 | 值 | 行为 | 适用场景 |
|------|-----|------|---------|
| **Standard** | 10 | 解压到工作目录，直接运行 | 大多数应用（推荐） |
| **Shadow** | 11 | 解压到影子目录，配置保留在工作目录 | 热更新、频繁发布 |
| **Hosted** | 12 | 仅解压，不启动进程 | IIS / Nginx 托管 |
| **Task** | 13 | 运行一次后完成，不守护 | 脚本、数据库迁移 |

**兼容性**：旧版模式值 0-4 被新版客户端自动映射为 10-13，新旧客户端共存平滑过渡。

策略实现：
- `StandardDeployStrategy` / `ShadowDeployStrategy` / `HostedStrategy` / `TaskStrategy`
- 统一接口 `IDeployStrategy`，`ServiceController` 通过工厂创建

---

## 五、版本匹配机制

`DeployService.GetDeployVersion()` 根据节点平台从版本列表中筛选：

```
1. 取 Enable=true 的版本，按 ID 倒序（最新优先）
2. 依次匹配：OS → Arch → TargetFramework
3. OS/Arch=0 或 TFM 为空表示通用，匹配所有节点
4. TFM 向上兼容：net8.0 可运行在 net9.0/net10.0 节点
```

**回滚**：禁用问题版本 → 发布 → 系统自动选取次新的匹配版本。

---

## 六、发布流程

```
用户在 AppDeployNode 列表点击"发布"
    │
    ▼
Web DeployService.Control("install")
    ├── 设置 deployNode.Enable = true
    └── SendNodeCommandAsync("deploy/install", {Id, DeployName, AppName})
        │
        ▼  WebSocket 实时推送，HTTP 轮询兜底
        │
StarAgent.ServiceManager.OnInstall()
    ├── PullService() → GET /node/getDeploy → 获取 DeployInfo
    │     └── Server DeployService.BuildDeployInfo()
    │           ├── GetDeployVersion() 平台匹配
    │           ├── 查找 SSL 证书（Domain 匹配）
    │           └── 构建 DeployInfo {Url, Hash, Service, ...}
    │
    ├── Download(info, svc) → 下载 zip → MD5 校验 → 覆盖原文件
    ├── ServiceController.Start()
    │     └── IDeployStrategy.Extract() → 解压到工作目录/影子目录
    │     └── IDeployStrategy.Execute() → 启动进程
    │
    └── 守护：ServiceManager.DoWork() 定时（30s）
          ├── 进程存活检查
          ├── 内存超限检查
          ├── 文件变化检查（ReloadOnChange）
          └── 上报心跳
```

---

## 七、客户端架构（StarAgent）

### 组件关系

```
ServiceManager（总管）
  ├── 注册指令："deploy/install" "deploy/start" "deploy/stop" ...
  ├── 定时器 DoWork()：健康检查 + 配置拉取
  └── 管理多个 ServiceController（每个应用一个）
        │
        └── ServiceController
              ├── IDeployStrategy（解压 + 启动）
              ├── 进程监控（存活/内存/文件变化）
              └── 事件上报（EventProvider）
```

### 指令处理

| 指令 | 处理 |
|------|------|
| `deploy/install` | PullService → Download → Strategy.Extract → Strategy.Execute |
| `deploy/start` | 设置 Enable=true → 启动进程 |
| `deploy/stop` | 终止进程 |
| `deploy/restart` | Stop + Start |
| `deploy/uninstall` | 终止进程 → 设置 Enable=false |
| `deploy/compile` | 仅编译节点：拉代码 → 编译 → 打包 → 上传 |

---

## 八、关键设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 版本匹配 | OS → Arch → TFM 逐层筛选 | 一个版本包只为一个平台编译，精确匹配确保兼容 |
| MultiVersion | 开关控制，开启后自动选最新匹配版本 | 新应用默认开启，老应用保持单版本行为 |
| 节点级配置覆盖 | AppDeployNode 可覆盖 FileName/Arguments 等 | 同一应用不同节点可能需要不同参数 |
| 部署模式兼容 | 旧版 0-4 映射为新版 10-13 | 平滑升级，无需同时升级所有 Agent |
| 证书格式选择 | Windows → Pfx，Linux → Pem | 自动适配，减少人工配置 |
| 数据清理 | Truncate / Delete 双模式 | 灵活适配不同数据库权限 |
| 通信双通道 | HTTP 心跳 + WebSocket 指令 | 实时性 + 可靠性 |
| 流水线状态 | Pending → Building → UploadSucceeded → Deploying → Success/Failed | 细粒度追踪，快速定位失败环节 |

---

## 九、最佳实践

| 场景 | 建议 |
|------|------|
| 版本命名 | `{主}.{次}.{年}.{月日}`，如 `1.0.2025.0701` |
| 部署模式 | 绝大多数应用用 Standard；频繁更新用 Shadow |
| 滚动发布 | 给不同节点设置不同的 Delay（0/60/120 秒） |
| 回滚 | 禁用问题版本 → 重新发布（自动选次新版） |
| 灰度发布 | 先发布少数节点观察，确认后全量 |

---

## 十、故障排查

| 现象 | 可能原因 | 排查 |
|------|----------|------|
| 发布后节点未更新 | WebSocket 断开 | 检查 LastActive、StarAgent 日志 |
| 进程启动失败 | 文件权限 / 依赖缺失 | 检查 StarAgent 日志，手动运行 |
| 证书未匹配 | Domain 不一致 / 证书过期 | 检查 Urls 和证书有效期 |
| 版本选错 | OS/Arch/TFM 设置不正确 | 检查版本平台字段 |

### 日志位置

| 组件 | 路径 |
|------|------|
| Web / Server | `Logs/*.log` |
| StarAgent | `Logs/staragent.log` |
| 应用 | `<工作目录>/logs/*.log` |
