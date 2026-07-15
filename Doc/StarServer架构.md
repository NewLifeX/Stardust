# StarServer 架构

> 版本：v1.0 | 日期：2026-07-15
> 对应模块：M4 应用管理 · M6 配置中心 · M7 注册中心 · M9 日志中心 · M3 平台管理 · M12 数据中间件 · M2 基础能力

---

## 1. 概述

StarServer（`Stardust.Server`）是星尘平台的服务端核心，提供所有面向客户端和 StarWeb 的 API 接口。基于 ASP.NET Core 构建，可横向扩展部署。

**核心职责：**
- 接受 StarAgent/App 客户端的登录、心跳、数据上报
- 接受 StarWeb 的管理操作请求
- 处理监控数据的统计计算
- 管理节点/应用/配置/注册/日志的生命周期

---

## 2. 分层架构

```
┌─────────────────────────────────────────────────────────────────┐
│                    Controller 层 (API 接口)                      │
│  NodeController  AppController  ConfigController  TraceController│
│  LogController   DeployController  GatewayController  OAuthCtrl  │
│  AgentDeployController  CubeController  ApiController            │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────────┐
│                    Service 层 (业务逻辑)                         │
│  NodeService  NodeOnlineService  NodeSessionManager              │
│  ConfigService  RegistryService  DeployService  GatewayService   │
│  TraceStatService  TraceItemStatService  AppDayStatService       │
│  MonitorService  AlarmService  AppOnlineService                  │
│  DataRetentionService  DotNetSyncService                         │
│  AgentDeployService  FileStorageService  RedisService            │
│  MySqlService  UplinkService  ApolloService                      │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────────┐
│                    Stardust.Data (数据层)                        │
│  Nodes/  Entity/  Configs/  Monitors/  Deployment/  Gateway/    │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────────┐
│                    基础设施 (MySQL/SQLite/Redis/文件)            │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. 核心组件

### 3.1 API 控制器

| 控制器 | 路由 | 说明 | 对应模块 |
|--------|------|------|----------|
| `NodeController` | `/node` | 节点注册/心跳/升级/命令 | M1 |
| `AppController` | `/app` | 应用注册/心跳 | M2 |
| `ConfigController` | `/config` | 配置拉取/设置 | M3 |
| `TraceController` | `/trace` | 监控数据上报 | M5 |
| `LogController` | `/log` | 日志接收 | M6 |
| `DeployController` | `/deploy` | 发布接口 | M7 |
| `GatewayController` | `/gateway` | 网关配置下发 | M8 |
| `OAuthController` | `/oauth` | OAuth 令牌服务 | M9 |
| `AgentDeployController` | `/agentdeploy` | Agent 远程部署 | M1 |
| `CubeController` | `/cube` | 魔方数据接口（附件等） | M7 |
| `ApiController` | `/api` | 系统信息/接口列表 | M12 |

### 3.2 核心服务

| 服务 | 说明 |
|------|------|
| `NodeService` | 节点管理：注册、心跳处理、在线状态 |
| `NodeOnlineService` | 节点在线会话管理 |
| `NodeStatService` | 节点统计计算 |
| `NodeSessionManager` | 节点 WebSocket 会话管理 |
| `ConfigService` | 配置中心核心：配置存储/版本/作用域 |
| `RegistryService` | 注册中心核心：服务注册/发现/解析 |
| `DeployService` | 发布核心：版本匹配/证书匹配/依赖检查 |
| `GatewayService` | 网关配置管理 |
| `TraceStatService` | 追踪统计（核心引擎，80% 资源） |
| `TraceItemStatService` | 埋点项统计 |
| `AppDayStatService` | 应用天统计 |
| `MonitorService` | 监控数据处理 |
| `AlarmService` | 告警触发服务 |
| `AppOnlineService` | 应用在线管理 |
| `AppSessionManager` | 应用会话管理 |
| `AppTokenService` | 应用令牌管理 |
| `OnlineService` | 在线状态基类 |
| `DataRetentionService` | 数据保留与清理 |
| `DotNetSyncService` | .NET 运行时同步 |
| `AgentDeployService` | 远程部署服务 |
| `FileStorageService` | 文件存储服务 |
| `RedisService` | Redis 代理服务 |
| `MySqlService` | MySQL 代理服务 |
| `ShardTableService` | 分表服务 |
| `UplinkService` | 上行链路服务 |
| `ApolloService` | Apollo 配置集成 |

---

## 4. 关键配置

通过 `StarServerSetting` 管理（存储在数据库 `StarServer` 分类）：

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| Port | 6600 | 服务端口 |
| TokenSecret | — | JWT 令牌密钥 |
| TokenExpire | 7200 | 令牌有效期（秒） |
| SessionTimeout | 600 | 会话超时（秒） |
| AutoRegister | true | 节点自动注册 |
| AppAutoRegister | true | 应用自动注册 |
| WhiteIP | — | 准入白名单 |
| MonitorFlowPeriod | 5 | 监控流统计周期（秒） |
| MonitorBatchPeriod | 30 | 监控批统计周期（秒） |
| MonitorSavePeriod | 60 | 监控落盘周期（秒） |
| DataRetention | 3 | 数据保留天数 |

---

## 5. 通信协议

### 5.1 客户端令牌鉴权

```
请求: POST /node/login  { code, secret, node: { UUID, ... } }
响应: { token, expire, code, secret }

后续请求: Authorization: Bearer {token}
```

### 5.2 心跳协议

```
请求: POST /node/ping  { token, ... }
响应: { time, config, commands: [...] }
```

### 5.3 配置拉取协议

```
请求: GET /config/getall?appId=xxx&token=xxx&version=N&scope=xxx
响应: { version, scope, configs: {...}, nextVersion, updateTime }
```

> 详见各子模块架构文档。
