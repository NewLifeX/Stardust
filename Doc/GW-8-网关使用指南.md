# GW-8 网关使用指南

> 版本：v1.0 | 日期：2026-07-15
> 对应需求：GW-8 StarGateway 使用指南

---

## 概述

StarGateway（星尘网关）是星尘分布式平台的流量网关组件，提供 Nginx 类反向代理能力。它采用**客户端代理**架构——网关实例作为 StarServer 的客户端自动获取配置，无需在每个实例上手工配置。

### 核心特性

| 特性 | 说明 |
|------|------|
| **动态路由** | 域名/路径/请求头/方法匹配，支持通配符 |
| **负载均衡** | 轮询/最少连接/IP Hash |
| **TLS 终止** | HTTPS 入站解密，支持 PEM/PFX 多格式证书 |
| **WebSocket 代理** | 透明帧转发，路由级开关，仅首次日志 |
| **健康检查** | TCP 端口主动探测，自动摘除不健康节点 |
| **配置热更新** | 配置变更实时生效，零中断 |
| **StarAgent 协同** | 冷启动唤醒应用，空闲自动回收 |
| **星尘 APM 集成** | 每次转发创建追踪 Span |
| **StarServer 注册** | 作为 AppClient 注册，在线可见 |

### 架构位置

```
客户端 (HTTP/HTTPS/WebSocket)
     │
     ▼
┌─────────────────────────────────┐
│      StarGateway (:8800)        │
│  ┌───────────────────────────┐  │
│  │  HttpReverseProxy          │  │
│  │  ├── 路由匹配              │  │
│  │  ├── 负载均衡              │  │
│  │  ├── TLS 终止              │  │
│  │  ├── 健康检查              │  │
│  │  └── Admin API             │  │
│  └───────────────────────────┘  │
│         ↕                       │
│  StarFactory (AppClient)        │
└────────────┬────────────────────┘
             │
    ┌────────┴────────┐
    ▼                 ▼
StarServer        StarAgent
(配置/注册)       (启停/守护)
```

## 安装部署

### 前置条件
- .NET 10.0 Runtime
- 已部署的 StarServer
- （可选）已部署的 StarAgent

### 编译发布

```bash
git clone https://github.com/NewLifeX/Stardust.git
cd Stardust
dotnet build StarGateway/StarGateway.csproj -c Release
dotnet publish StarGateway/StarGateway.csproj -c Release -o publish
```

### 配置

StarGateway 支持多级配置来源（优先级从高到低）：
1. 命令行参数（如 `--StarServer=http://...`）
2. 环境变量（`StarServer` / `StarAppId` / `StarSecret`）
3. `appsettings.json`
4. `config/Star.config`

详细配置项见 [GW-网关架构](GW-网关架构.md) 和 [需求文档](需求文档.md) §3.11 GW 网关管理。
