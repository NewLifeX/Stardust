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

---

> 详细子模块文档见：[M5-9-指令下发](M5-9-指令下发.md)、[M5-15-StarAgent远程部署](M5-15-StarAgent远程部署.md)、[M5-14-防火墙管理](M5-14-防火墙管理.md)
