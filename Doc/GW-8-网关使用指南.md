# GW-8 网关使用指南

> 版本：v1.0 | 日期：2026-07-15
> 对应需求：GW-8 StarGateway 使用指南

---

## 概述

StarGateway（星尘网关）是星尘分布式平台的流量网关组件，提供 Nginx 类反向代理能力。它采用**客户端代理**架构——网关实例作为 StarServer 的客户端自动获取配置，无需在每个实例上手工配置。

### 核心特性

| 特性                | 说明                                                         |
| ------------------- | ------------------------------------------------------------ |
| **动态路由**        | 域名/路径/请求头/方法匹配，支持通配符                        |
| **负载均衡**        | 轮询/最少连接/IP Hash                                        |
| **TLS 终止**        | HTTPS 入站解密，支持 PEM/PFX 多格式证书                      |
| **WebSocket 代理**  | 透明帧转发，路由级开关，仅首次日志                           |
| **健康检查**        | TCP 端口主动探测，自动摘除不健康节点                         |
| **配置热更新**      | 配置变更实时生效，零中断                                     |
| **StarAgent 协同**  | 冷启动唤醒应用，空闲自动回收                                 |
| **星尘 APM 集成**   | 每次转发创建追踪 Span                                        |
| **StarServer 注册** | 作为 AppClient 注册，在线可见                                |
| **静态文件托管**    | ✅ 直接托管本地静态目录，支持默认首页、目录浏览、SPA fallback |

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
dotnet publish StarGateway/StarGateway.csproj -c Release -o ./publish/gateway
```

发布后得到 `publish/gateway/` 目录，包含网关完整程序。

### 配置

StarGateway 支持多级配置来源（优先级从高到低）：
1. 命令行参数（如 `--StarServer=http://...`）
2. 环境变量（`StarServer` / `StarAppId` / `StarSecret`）
3. `appsettings.json`
4. `config/Star.config`

#### appsettings.json 配置项

```json
{
  "StarServer": "http://你的星尘服务端:6600",
  "StarAppId": "StarGateway",
  "StarSecret": "你的AppSecret",

  "StarGateway": {
    "Debug": true,
    "Port": 8800,
    "LocalConfigFile": "gateway.json",
    "HealthCheckInterval": 10,
    "ConfigRefreshInterval": 15,
    "IdleTimeout": 900
  }
}
```

| 配置项                  | 说明                       | 默认值                  |
| ----------------------- | -------------------------- | ----------------------- |
| `StarServer`            | 星尘服务端地址             | `http://127.0.0.1:6600` |
| `StarAppId`             | 网关应用标识               | `StarGateway`           |
| `StarSecret`            | 应用密钥                   | `""`                    |
| `Debug`                 | 调试日志开关               | `true`                  |
| `Port`                  | 网关监听端口               | `8800`                  |
| `LocalConfigFile`       | 本地兜底配置文件路径       | `gateway.json`          |
| `HealthCheckInterval`   | 健康检查间隔（秒）         | `10`                    |
| `ConfigRefreshInterval` | 配置刷新间隔（秒）         | `15`                    |
| `IdleTimeout`           | 空闲回收超时（秒，15分钟） | `900`                   |

#### 运行网关

```bash
cd publish/gateway
./StarGateway
```

启动成功日志示例：
```
StarGateway 已连接 StarServer: http://star.newlifex.com:6600
数据库初始化完成
加载SSL证书: *.newlifex.com -> CN=*.newlifex.com
Http反向代理启动，监听端口：8800，路由数：3
Application started. Press Ctrl+C to shut down.
```

详细配置项见 [GW-网关架构](GW-网关架构.md) 和 [需求文档](需求文档.md) §3.11 GW 网关管理。

---

## 路由配置（核心）

网关的路由配置存储在数据库的 `GatewayRoute`（网关路由）、`GatewayCluster`（网关集群）、`GatewayNode`（网关节点）三张表中。均通过 StarServer 后台管理界面配置，配置变更后网关自动热更新，无需重启。

### 路由配置流程

```
1. 创建集群（Cluster）→ 2. 添加节点（Node）→ 3. 配置路由（Route）
```

### 1. 创建集群（GatewayCluster）

一个集群 = 一组后端节点，共享负载均衡策略。

| 字段          | 说明         | 示例                                        |
| ------------- | ------------ | ------------------------------------------- |
| `Name`        | 集群名称     | `用户服务集群`                              |
| `LoadBalance` | 负载均衡算法 | `RoundRobin` / `LeastConnection` / `IPHash` |
| `Enable`      | 启用         | `true`                                      |

负载均衡算法说明：

| 算法              | 说明                                       | 适用场景           |
| ----------------- | ------------------------------------------ | ------------------ |
| `RoundRobin`      | 轮询，逐个分发请求                         | 各节点性能均匀     |
| `LeastConnection` | 最少连接，分发到当前活跃连接最少的节点     | 请求处理时间差异大 |
| `IPHash`          | 客户端 IP 哈希，同一 IP 始终转发到同一节点 | 需要会话保持       |

### 2. 添加节点（GatewayNode）

节点就是后端实际提供服务的地址。

| 字段        | 说明     | 示例                    |
| ----------- | -------- | ----------------------- |
| `ClusterId` | 所属集群 | 集群ID                  |
| `Name`      | 节点名称 | `用户服务-节点1`        |
| `Address`   | 服务地址 | `http://127.0.0.1:5001` |
| `Weight`    | 权重     | `1`                     |
| `Enable`    | 启用     | `true`                  |

同一集群下添加多个节点可实现负载均衡。网关每 10 秒对这些节点做 TCP 端口探测，不健康的节点自动摘除，恢复后自动加回。

### 3. 配置路由（GatewayRoute）

路由 = 匹配进来的请求 → 转发到哪个集群。

| 字段                         | 说明                                                                        | 示例                                     |
| ---------------------------- | --------------------------------------------------------------------------- | ---------------------------------------- |
| `Name`                       | 路由名称                                                                    | `用户管理`                               |
| `Enable`                     | 启用                                                                        | `true`                                   |
| `Priority`                   | 优先级，越大越优先                                                          | `0`                                      |
| `ClusterId`                  | 目标集群                                                                    | 集群ID                                   |
| **`Domain`**                 | **域名匹配**                                                                | `users.newlifex.com` 或 `*.newlifex.com` |
| **`Path`**                   | **路径匹配**                                                                | `/api/*` 或 `/api/v1/users`              |
| **`Methods`**                | **HTTP 方法**                                                               | `GET,POST,PUT,DELETE`（空=全部）         |
| `Headers`                    | 请求头匹配                                                                  | JSON 格式 `{"X-Region":"cn"}`            |
| `StripPrefix`                | 转发时去除匹配的路径前缀                                                    | `true`                                   |
| `AddHeaders`                 | 转发时添加请求头                                                            | JSON 格式 `{"X-Forwarded-By":"Gateway"}` |
| **`WebSocket`**              | **允许 WebSocket 升级**                                                     | `true` / `false`                         |
| *新增* **`StaticRoot`**      | **静态文件根目录**。设置后路由不再转发到后端集群，改为直接托管本地静态文件  | `/var/www/html` 或 `./wwwroot`           |
| *新增* **`IndexFile`**       | **默认首页**                                                                | `index.html`                             |
| *新增* **`DirectoryBrowse`** | **目录浏览**。是否允许浏览目录列表                                          | `true` / `false`                         |
| *新增* **`SPAFallback`**     | **SPA回退**。文件不存在时回退到 `index.html`，用于支持前端 history 路由模式 | `true` / `false`                         |

**路由匹配规则**：按 `Priority` 降序 → 依次检查 `Domain` → `Path` → `Methods` → `Headers`，全部命中即匹配成功。

**域名匹配**：
- `users.newlifex.com` — 精确匹配
- `*.newlifex.com` — 通配符匹配（匹配任意子域名）
- 多个域名用逗号分隔

**路径匹配**：
- `/api/*` — 前缀匹配（匹配 `/api/xxx`）
- `/api/v1/users` — 精确匹配
- 多个路径用逗号分隔

### 典型配置示例

#### 场景1：简单反向代理

> 请求 `http://网关:8800` → 转到 `http://127.0.0.1:5000`

| 表                 | 数据                                         |
| ------------------ | -------------------------------------------- |
| **GatewayCluster** | `Name=默认集群, LoadBalance=RoundRobin`      |
| **GatewayNode**    | `ClusterId=1, Address=http://127.0.0.1:5000` |
| **GatewayRoute**   | `Domain=*, Path=/, ClusterId=1`              |

#### 场景2：多域名多服务

> `api.mysite.com/users/*` → 用户服务（`:5001`），`api.mysite.com/orders/*` → 订单服务（`:5002`）

**路由1：用户服务**
```
Domain: api.mysite.com
Path: /users/*
Methods: GET,POST,PUT,DELETE
ClusterId: 1 → 节点 :5001
WebSocket: true
```

**路由2：订单服务**
```
Domain: api.mysite.com
Path: /orders/*
Methods: GET,POST,PUT,DELETE
ClusterId: 2 → 节点 :5002
WebSocket: true
```

#### 场景3：负载均衡 + 健康检查

> 用户服务在两个端口运行，轮询分发

**集群**：`LoadBalance=RoundRobin`

**节点1**：`Address=http://127.0.0.1:5001, Weight=1`
**节点2**：`Address=http://127.0.0.1:5002, Weight=2`（权重更高，分配到更多流量）

网关每 10 秒 TCP 探测两个端口，端口挂了自动摘除，恢复后自动加回。

#### 场景4：直接托管静态文件

> 前端项目打包产物放在 `/var/www/frontend`，网关直接读取文件返回，无需额外静态服务器

**适用场景**：Vue/React 等前端项目构建后的 `dist/` 目录，或任何静态网站目录。**不需要 Nginx/Express 等额外的静态服务器。**

**集群和节点不需要配置**，只需在 `GatewayRoute` 表里配一条路由：

| 字段              | 数据                                               |
| ----------------- | -------------------------------------------------- |
| `Name`            | `前端静态文件`                                     |
| `Domain`          | `app.mysite.com`                                   |
| `Path`            | `/*`                                               |
| **`StaticRoot`**  | **`/var/www/frontend`**（或相对路径 `./frontend`） |
| `IndexFile`       | `index.html`（默认值，可不填）                     |
| `DirectoryBrowse` | `false`                                            |
| **`SPAFallback`** | **`true`** ← 关键！前端 history 模式必须开启       |
| `Methods`         | `GET`                                              |

网关收到 `GET /` 请求后匹配此路由 → 发现 `StaticRoot=/var/www/frontend` → 直接从磁盘读取 `/var/www/frontend/index.html` 返回，**不走反向代理转发**。

**SPA 路由处理**（History 模式）：
- 前端项目本身用 history 模式时（如 Vue Router `createWebHistory()`、React Router `BrowserRouter`），生成的 HTML 中 JS/CSS 路径是相对或绝对路径，**无需额外处理**
- 浏览器访问 `/login` 时，网关在磁盘上找不到 `login.html` 文件
- **开启 `SPAFallback=true`** 后 → 网关自动回退返回 `index.html`，前端 JS 读取 URL 路径渲染对应页面
- **不开 `SPAFallback`**（纯静态文件服务）→ 按磁盘文件路径查找，找不到返回 404

---

## HTTPS 配置

在 `SslCertificate`（SSL证书）表中添加一条记录，网关启动时自动加载证书。

| 字段                  | 说明         | 示例                                      |
| --------------------- | ------------ | ----------------------------------------- |
| `Domain`              | 证书域名     | `*.newlifex.com`                          |
| `Enable`              | 启用         | `true`                                    |
| `PfxFile`             | PFX 证书路径 | `certs/gateway.pfx`                       |
| `PfxPassword`         | PFX 密码     | 你的密码                                  |
| 或                    |              |                                           |
| `PemFile`             | PEM 证书路径 | `certs/gateway.pem`                       |
| 或                    |              |                                           |
| `CrtFile` + `KeyFile` | CRT + 私钥   | `certs/gateway.crt` + `certs/gateway.key` |

证书支持三种格式，任选一种配置即可。配置后客户端用 `https://网关:8800` 访问即可建立 TLS 加密连接。

---

## Admin API（调试接口）

网关内置管理 API，方便调试查看状态：

| 接口               | 说明                                               |
| ------------------ | -------------------------------------------------- |
| `GET /api/status`  | 运行状态（运行时间、活跃连接数、请求总数、路由数） |
| `GET /api/routes`  | 列出所有路由配置                                   |
| `GET /api/refresh` | 手动触发配置刷新                                   |

查看状态：
```bash
curl http://127.0.0.1:8800/api/status
```

返回示例：
```json
{
  "uptime": 3600,
  "activeSessions": 5,
  "totalRequests": 1024,
  "routeCount": 3,
  "port": 8800
}
```

---

## StarAgent 协同（可选）

如果和 StarAgent 部署在同一台机器上，Gateway 通过 `http://127.0.0.1:5500` 调用 Agent API，实现：

| 特性             | 说明                                                                         |
| ---------------- | ---------------------------------------------------------------------------- |
| **冷启动唤醒**   | 请求进来发现目标端口未监听，返回 503 并通知 Agent 启动进程，客户端重试后恢复 |
| **空闲自动回收** | 节点超过 15 分钟无流量，通知 Agent 停止进程释放内存                          |

---

## 常见问题

| 问题                           | 原因                           | 解决                                         |
| ------------------------------ | ------------------------------ | -------------------------------------------- |
| 启动报错 `StarServer 连接失败` | StarServer 地址配置错误        | 检查 `appsettings.json` 中 `StarServer` 地址 |
| 访问报 404 匹配不到路由        | 路由表的 Domain/Path 没匹配上  | 检查 `GatewayRoute` 表中的域名和路径         |
| HTTPS 访问不了                 | SSL 证书没加载成功             | 检查 `SslCertificate` 表数据和证书文件路径   |
| WebSocket 连接失败             | 路由 `WebSocket` 字段未勾选    | 把路由的 `WebSocket` 设为 `true`             |
| 后端服务不通                   | 节点地址写错或服务没运行       | 检查 `GatewayNode` 地址和端口                |
| 静态文件 404                   | 文件路径不对或根目录未正确配置 | 检查 `StaticRoot` 路径和磁盘上实际文件位置   |
| 静态文件 403                   | 路径穿越攻击被拦截             | 路径不能包含 `..` 跳出 `StaticRoot` 目录     |

---

## 快速自查清单

- [ ] 编译发布 `StarGateway` 成功
- [ ] `appsettings.json` 配置了 `StarServer` 地址和密钥
- [ ] StarServer 后台 `GatewayCluster` 表已有集群数据
- [ ] StarServer 后台 `GatewayNode` 表已有节点数据（地址正确）
- [ ] StarServer 后台 `GatewayRoute` 表已有路由数据（域名/路径正确）
- [ ] 托管静态文件时，路由的 `StaticRoot` 指向正确的目录
- [ ] 需要 HTTPS 时，`SslCertificate` 表已配置证书
- [ ] 运行 `./StarGateway` 启动日志无报错
- [ ] `curl http://127.0.0.1:8800/api/status` 返回正常
- [ ] 代理路由：`curl http://127.0.0.1:8800/配置的路径` 能正常返回数据
- [ ] 静态路由：`curl http://127.0.0.1:8800/` 返回前端首页
