# StarAgent 架构

> 版本：v1.0 | 日期：2026-07-15
> 对应模块：M5 节点管理
> 子模块文档：[M5-9-指令下发](M5-9-指令下发.md) | [M5-14-防火墙管理](M5-14-防火墙管理.md) | [M5-15-StarAgent远程部署](M5-15-StarAgent远程部署.md)

---

## 1. 概述

StarAgent 是部署在每台应用服务器上的节点代理程序，以系统服务（Windows Service / Linux systemd）方式运行，负责：

- **进程守护**：管理本地业务应用的启动、停止、守护、自动重启
- **监控采集**：采集 CPU/内存/磁盘/网络等系统指标，上报给 StarServer
- **远程发布**：接收 StarServer 的发布指令，下载应用包并执行部署
- **命令执行**：接收并执行远程命令（启动服务、停止服务、脚本执行等）
- **自身升级**：自动检测 StarAgent 新版本并升级

---

## 2. 核心功能

### 2.1 注册与心跳

```
启动 → 读取硬件信息 → 计算节点编码 → Login → 获取令牌 → 心跳循环
```

- 节点编码公式：`Crc({ProductCode}@{UUID}@{DiskID}@{Macs})`
- 心跳周期：60 秒，同时上报 CPU/内存/磁盘/网络等指标
- 在线会话：通过 WebSocket 长连接保持，断线自动重连

### 2.2 进程守护

`ServiceManager` 管理本地应用的完整生命周期：

| 功能 | 说明 |
|------|------|
| 启动进程 | 按部署模式（Standard/Shadow/Hosted/Task）启动 |
| 自动重启 | 进程异常退出时自动拉起 |
| 内存限制 | 内存超限自动重启 |
| 文件监控 | 文件变化自动重启（热更新） |
| 优雅关闭 | SIGTERM → 超时 → SIGKILL |

### 2.3 监控采集

`MachineInfoProvider` 采集系统指标：

- CPU 使用率
- 内存使用率（总/已用/可用）
- 磁盘使用率
- 网络流量
- 进程列表
- 自定义指标

### 2.4 命令执行

通过 WebSocket 双通道接收命令：

| 命令类型 | 说明 |
|----------|------|
| `StartService` | 启动指定部署应用 |
| `StopService` | 停止指定部署应用 |
| `RestartService` | 重启指定部署应用 |
| `InstallService` | 安装系统服务 |
| `UninstallService` | 卸载系统服务 |
| `ExecuteCommand` | 执行 Shell 命令 |
| `Upgrade` | 升级 StarAgent 自身 |

---

## 3. 通信架构

```
┌─────────────────┐          HTTP/WebSocket         ┌─────────────────┐
│   StarAgent     │ ◄────────────────────────────►   │   StarServer    │
│                 │     Login / Ping / Command       │                 │
│  ┌───────────┐  │                                  │  ┌───────────┐  │
│  │ StarClient │  │                                  │  │NodeCtrl   │  │
│  └───────────┘  │                                  │  └───────────┘  │
│  ┌───────────┐  │                                  │  ┌───────────┐  │
│  │ServiceMgr │  │                                  │  │ DeploySvc │  │
│  └───────────┘  │                                  │  └───────────┘  │
│  ┌───────────┐  │                                  │                 │
│  │MachineInfo│  │                                  │                 │
│  └───────────┘  │                                  │                 │
└─────────────────┘                                  └─────────────────┘
```

---

## 4. 部署模式

| 模式 | 说明 | 进程关系 |
|------|------|----------|
| **Standard** | 直接运行子进程 | StarAgent → 子进程 |
| **Shadow** | 影子模式，新版先启动再切换 | StarAgent → 子进程（旧）→ 子进程（新）|
| **Hosted** | 宿主进程托管 | StarAgent → Host → 插件 |
| **Task** | 一次性任务 | StarAgent → 任务进程（执行后退出） |

---

## 5. 关键配置

通过 `StarAgent/Setting.cs` 管理：

| 配置项 | 说明 |
|--------|------|
| `ServerUrl` | StarServer 地址 |
| `NodeCode` | 节点编码（自动计算） |
| `Secret` | 节点密钥（自动注册后获取） |
| `LocalPort` | 本地 API 端口（供 StarGateway 调用） |
| `HeartbeatPeriod` | 心跳周期（默认 60 秒） |
| `StartTimeout` | 应用启动超时（默认 30 秒） |
| `StopTimeout` | 应用停止超时（默认 10 秒） |
| `MemoryLimit` | 内存限制（MB，0 不限制） |
| `AutoStart` | 开机自启 |

## 6. Web 管理面板

### 6.1 概述

StarAgent 基于 NewLife.Agent 内置的 `AgentWebPanel` 框架，提供了轻量级 Web 管理面板。该面板内嵌于 StarAgent 进程中，通过 HTTP 访问，无需额外部署。

- **框架**：`NewLife.Agent.WebPanel.AgentWebPanel`
- **实现**：`StarAgent.WebPanel.StarPanel`（继承定制）
- **API**：`StarAgent.WebPanel.StarApi`
- **前端**：嵌入式 `index.html` SPA（单页应用）
- **监听端口**：默认 5580（由 `agent.config` 中 `WebPort` 配置）

### 6.2 面板功能

面板采用 7 个 Tab 的布局：

| Tab | 类型 | 功能 |
|-----|------|------|
| 📊 **状态** | 增强 | 服务状态、资源监控（CPU/内存/线程/句柄/网络/磁盘IO）、本机详情（磁盘分区/网卡/进程 Top/系统信息） |
| 📦 **子服务** | 新增 | 子服务 CRUD（添加/编辑/删除）、启停重启操作、运行状态实时查看 |
| ⚡ **控制** | 保留 | StarAgent 自身服务启停控制 |
| ⚙ **配置** | 保留 | AgentSetting 配置查看与修改 |
| 🌐 **星尘设置** | 新增 | StarSetting（Server/AppKey/Secret）+ StarAgentSetting（LocalPort/Code/Channel/Delay 等）配置查看与修改 |
| 📋 **日志** | 保留 | 日志文件浏览与实时查看 |
| 🐕 **看门狗** | 保留 | 看门狗服务状态监控 |

### 6.3 架构说明

```
浏览器 ──HTTP──► StarAgent Web 面板 (Port 5580)
                     │
          ┌──────────┴──────────┐
          ▼                     ▼
   HttpServer              EmbeddedResource
   ┌─────────────┐        ┌──────────────┐
   │  /api/*      │        │  /*           │
   │  ApiController│        │  index.html   │
   │  (内置)       │        │  favicon.ico  │
   │              │        └──────────────┘
   │  /api/star/* │
   │  StarAgentApi│
   │  Controller  │
   └──────┬──────┘
          │
          ▼
   ServiceManager / StarSetting / StarAgentSetting
```

- **内置 API**（`/api/*`）：登录鉴权、服务状态、Agent配置、日志、看门狗 — 由 `AgentWebPanel.ApiController` 提供
- **StarAgent API**（`/api/star/*`）：子服务管理、星尘配置、本机信息 — 由 `StarApi` 提供
- **静态文件**：`/` 根路径映射到嵌入式 `index.html`

### 6.4 扩展机制

`AgentWebPanel` 提供 `GetExtensions()` 虚方法，支持通过 `PanelExtension` 注册自定义面板。未来插件可通过重写此方法动态添加 Tab。

### 6.5 鉴权策略

| 级别 | 说明 |
|------|------|
| `None` | 不鉴权，任何人都可访问 |
| `LocalOnly`（默认） | 本地地址（127.0.0.1）免鉴权，远程访问需登录 |
| `Full` | 全部请求需登录 |

凭据在 `agent.config` 中通过 `WebUserName` / `WebPassword` 配置。登录后签发 Bearer Token，有效期 24 小时。

---

> 详细子模块文档见：[M5-9-指令下发](M5-9-指令下发.md)、[M5-15-StarAgent远程部署](M5-15-StarAgent远程部署.md)、[M5-14-防火墙管理](M5-14-防火墙管理.md)
