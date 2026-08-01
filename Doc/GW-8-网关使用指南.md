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

| 配置项                  | 说明                                        | 默认值                  |
| ----------------------- | ------------------------------------------- | ----------------------- |
| `StarServer`            | 星尘服务端地址                              | `http://127.0.0.1:6600` |
| `StarAppId`             | 网关应用标识                                | `StarGateway`           |
| `StarSecret`            | 应用密钥                                    | `""`                    |
| `Debug`                 | 调试日志开关                                | `true`                  |
| `Port`                  | 网关监听端口                                | `8800`                  |
| `LocalConfigFile`       | 本地兜底配置文件路径                        | `gateway.json`          |
| `HealthCheckInterval`   | 健康检查间隔（秒）                          | `10`                    |
| `ConfigRefreshInterval` | 配置刷新间隔（秒）                          | `15`                    |
| `IdleTimeout`           | 空闲回收超时（秒，15分钟）                  | `900`                   |
| `AdminToken`            | Admin API 访问令牌（留空=仅本机回环可访问） | `""`                    |

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

网关的路由配置支持**三种来源**，按优先级从高到低，**「首选胜出」覆盖（非合并）**：

| 优先级    | 来源                    | 说明                                                               | 适用场景                          |
| --------- | ----------------------- | ------------------------------------------------------------------ | --------------------------------- |
| 1（最高） | **StarServer 远程配置** | 网关作为 AppClient 向 StarServer `/Gateway/config` 拉取路由 + 证书 | 标准部署，集中管理、热更新        |
| 2         | **本地数据库**          | 直接读 `GatewayRoute`/`GatewayCluster`/`GatewayNode` 表            | Server 与网关共享同一 DB 时的兜底 |
| 3（最低） | **本地 JSON 文件**      | 读 `LocalConfigFile`（默认 `gateway.json`）                        | Server 与 DB 均不可达时的应急兜底 |

> **兜底链说明**：数据库共享模式下，StarServer 后台写入的就是同一份 DB，因此 Server API 与 DB 读的是同一份数据；**真实有意义的兜底链是：DB → 本地文件**。只要 StarServer 可达，第 1 级即生效；第 1 级失败/超时/返回空路由才回退第 2 级；第 2 级也失败才用第 3 级。
>
> 三种来源只决定「配置从哪读」，匹配、转发逻辑完全一致。其中 JSON 文件为**直连兜底**（见来源三），能力较 DB/Server 受限。

### 来源一：StarServer 远程配置（推荐）

- 前提：网关 `appsettings.json` 已配置 `StarServer` 地址与 `StarAppId`/`StarSecret`（网关以此应用身份向服务端鉴权）。
- 网关每 `ConfigRefreshInterval`（默认 15s）拉取一次；证书在 StarServer 为 **https** 时同源下发并覆盖本地证书，http 时为安全考虑拒绝下发（证书回退数据库加载）。
- 支持全部路由字段：动态路由、负载均衡、健康检查、静态文件托管、WebSocket。
- 在 StarServer 后台（或三张表）完成配置，变更后网关自动热更新，无需重启。

### 来源二：本地数据库

网关直接连接数据库，读取 `GatewayRoute`（网关路由）、`GatewayCluster`（网关集群）、`GatewayNode`（网关节点）三张表（仅启用项）。配置变更后网关自动热更新，无需重启。

### 来源三：本地 JSON 文件（应急兜底）

当 StarServer 与数据库**都不可达**时，网关从 `LocalConfigFile`（默认 `gateway.json`，相对网关工作目录）读取路由作为最终兜底。该方式为**直连 `target`** 语义：`ClusterId=0`，请求直接转发到 `target`，**不经数据库节点查询、无负载均衡、无健康检查、无故障转移**。

配置格式为 JSON 数组，每条路由字段（**本地 JSON 用小驼峰 camelCase**；StarServer 后台 / 数据库 `GatewayRoute` 表则使用实体属性名 PascalCase，如 `StaticRoot`，两套命名约定不同但语义一致）：

| 字段              | 必填 | 说明                                                | 示例                    |
| ----------------- | ---- | --------------------------------------------------- | ----------------------- |
| `name`            | 是   | 路由名称                                            | `用户服务`              |
| `domain`          | 是   | 域名匹配（支持 `*` 通配、逗号分隔）                 | `*.example.com`         |
| `target`          | 否   | 后端直连地址（反向代理用；静态路由无需）            | `http://127.0.0.1:5000` |
| `path`            | 否   | 路径匹配（默认空 = 全部）                           | `/api/*`                |
| `methods`         | 否   | HTTP 方法（默认空 = 全部）                          | `GET,POST`              |
| `priority`        | 否   | 优先级（越大越优先，默认 0）                        | `10`                    |
| `staticRoot`      | 否   | 静态文件根目录；设置后路由为静态托管（无需 target） | `/var/www/html`         |
| `indexFile`       | 否   | 默认首页（默认 `index.html`）                       | `index.html`            |
| `directoryBrowse` | 否   | 是否允许目录浏览（默认 false）                      | `false`                 |
| `spaFallback`     | 否   | SPA 回退（history 路由模式设为 true）               | `true`                  |

**示例 `gateway.json`**（含反向代理与静态文件两种形态）：

```json
[
  {
    "name": "用户服务反向代理",
    "domain": "api.example.com",
    "path": "/api/*",
    "methods": "GET,POST,PUT,DELETE",
    "target": "http://127.0.0.1:5000",
    "priority": 10
  },
  {
    "name": "官网静态文件",
    "domain": "www.example.com",
    "path": "/*",
    "staticRoot": "/var/www/html",
    "indexFile": "index.html",
    "directoryBrowse": false,
    "spaFallback": true
  }
]
```

> 静态文件路由只需 `staticRoot`（无需 `target`）；反向代理路由需 `target`。两者不要混写（`staticRoot` 与 `target` 同时出现时以静态优先，反向代理 `target` 被忽略）。

### 配置生效分析（结合代码）

| 路由                     | 场景         | 是否生效 | 说明                                                                                                                                                                                                                                                                                                                                             |
| ------------------------ | ------------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 路由1 `用户服务反向代理` | 反向代理     | ✅ 生效   | `name/domain/path/methods/target/priority` 均被 `LoadConfigFromLocalFile` 解析；`ClusterId` 固定为 `0`，`target` 写入直连表；请求命中后 `SelectNode` 直接 `new NetUri(target)` 转发。`api.example.com/api/xxx` → `http://127.0.0.1:5000`                                                                                                         |
| 路由2 `官网静态文件`     | 静态文件服务 | ✅ 生效   | `LoadConfigFromLocalFile` 解析到 `staticRoot` 非空即标记 `IsStaticRoute=true`（并读取 `indexFile`/`directoryBrowse`/`spaFallback`），因无 `target` 不写入直连表；请求命中后 `route.IsStaticRoute==true` 触发静态分支，按 `staticRoot` 从磁盘读取文件返回，`www.example.com/` → `/var/www/html/index.html`（spaFallback 开启时 history 路由回退） |

> 本地 JSON 兜底现已支持静态文件服务，与 StarServer / 数据库来源的静态路由行为一致。`ClusterId=0` 的静态路由不查数据库节点、无负载均衡/健康检查（直连磁盘）。

> ⚠️ **JSON 兜底的限制**：支持「反向代理直连」与「静态文件托管」，**不支持** WebSocket（默认关闭）、负载均衡与健康检查。需要 WebSocket 或负载均衡，请用来源一/二在 StarServer 后台或数据库 `GatewayRoute` 表配置。

### 路由配置流程（适用于来源一/二）

```
1. 创建集群（Cluster）→ 2. 添加节点（Node）→ 3. 配置路由（Route）
```

> 注：来源三（JSON 文件）无需集群/节点，反向代理写 `target`、静态托管写 `staticRoot` 即可。

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

| 接口               | 说明                                                             |
| ------------------ | ---------------------------------------------------------------- |
| `GET /api/status`  | 运行状态（运行时间、活跃连接数、请求总数、路由数、当前配置来源） |
| `GET /api/routes`  | 列出所有路由配置                                                 |
| `GET /api/refresh` | 手动触发配置刷新                                                 |

### 访问控制（AdminToken）

Admin API 暴露路由拓扑等敏感信息，必须鉴权：

- **未配置 AdminToken**（默认）：仅本机回环地址（`127.0.0.1`、`::1`）可访问，外部地址一律拒绝。适合单机运维。
- **已配置 AdminToken**：所有来源（含本机回环）都须携带匹配令牌，防止同机其它进程或 SSRF 无令牌调用 `/api/refresh` 或读取 `/api/routes`。

在 `appsettings.json`或者 `Config/StarGateway.config` 的 `StarGateway` 节配置：

```json
{
  "StarGateway": {
    "AdminToken": "你的强令牌"
  }
}
```

### 如何携带令牌

- **浏览器访问**（最直观）：未携带令牌时网关返回 401 并带 `WWW-Authenticate: Basic`，浏览器自动弹出原生登录框。**用户名随意填写（如 `admin`），密码填 AdminToken**，确定即完成认证。
- **curl / 命令行**：
  ```bash
  # 方式一：自定义头（推荐脚本使用）
  curl -H "X-Gateway-Token: 你的令牌" http://127.0.0.1:8800/api/status
  # 方式二：Bearer
  curl -H "Authorization: Bearer 你的令牌" http://127.0.0.1:8800/api/status
  # 方式三：Basic（与浏览器弹框一致）
  curl -u admin:你的令牌 http://127.0.0.1:8800/api/status
  ```
- **程序调用**：请求头 `Authorization: Bearer <token>` 或 `X-Gateway-Token: <token>`。

> ⚠️ **生产务必使用 HTTPS**：`X-Gateway-Token` / `Bearer` / `Basic` 均为明文编码（非加密），经 HTTP 传输时 AdminToken 可被中间人截获。请将 Admin API 置于 TLS 之后——给网关配置证书监听 443（见下方 HTTPS 配置），或由前置 Nginx 等做 TLS 终止后转发到网关管理端口。AdminToken **区分大小写**，且网关采用恒定时间比较以防护时序侧信道攻击。

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
  "port": 8800,
  "configSource": "server"
}
```

> `configSource` 字段标识当前生效的配置来源：`server`（来自 StarServer）/ `database`（本地数据库）/ `file`（本地 JSON 兜底）/ `none`（尚未加载）。排查配置未生效时，先看它确认走的是哪一级。

---

## StarAgent 协同（可选）

如果和 StarAgent 部署在同一台机器上，Gateway 通过 `http://127.0.0.1:5500` 调用 Agent API，实现：

| 特性             | 说明                                                                         |
| ---------------- | ---------------------------------------------------------------------------- |
| **冷启动唤醒**   | 请求进来发现目标端口未监听，返回 503 并通知 Agent 启动进程，客户端重试后恢复 |
| **空闲自动回收** | 节点超过 15 分钟无流量，通知 Agent 停止进程释放内存                          |

---

## 常见问题

| 问题                           | 原因                           | 解决                                             |
| ------------------------------ | ------------------------------ | ------------------------------------------------ |
| 启动报错 `StarServer 连接失败` | StarServer 地址配置错误        | 检查 `appsettings.json` 中 `StarServer` 地址     |
| 访问报 404 匹配不到路由        | 路由表的 Domain/Path 没匹配上  | 检查 `GatewayRoute` 表中的域名和路径             |
| HTTPS 访问不了                 | SSL 证书没加载成功             | 检查 `SslCertificate` 表数据和证书文件路径       |
| WebSocket 连接失败             | 路由 `WebSocket` 字段未勾选    | 把路由的 `WebSocket` 设为 `true`                 |
| 后端服务不通                   | 节点地址写错或服务没运行       | 检查 `GatewayNode` 地址和端口                    |
| 静态文件 404                   | 文件路径不对或根目录未正确配置 | 检查 `StaticRoot` 路径和磁盘上实际文件位置       |
| 静态文件 403                   | 路径穿越攻击被拦截             | 路径不能包含 `..` 跳出 `StaticRoot` 目录         |
| 不确定配置从哪级加载           | 多级兜底，来源不直观           | `curl /api/status` 看 `configSource` 字段        |
| 配了 StarServer 但路由没生效   | 远程返回空/鉴权失败，已回退 DB | 看启动日志「从 StarServer 拉取…」/「回退数据库」 |

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
- [ ] `curl http://127.0.0.1:8800/api/status` 返回正常，且 `configSource` 符合预期（server/database）
- [ ] （应急兜底）在 `gateway.json` 写好直连路由，Server 与 DB 均不可达时仍可转发
- [ ] 代理路由：`curl http://127.0.0.1:8800/配置的路径` 能正常返回数据
- [ ] 静态路由：`curl http://127.0.0.1:8800/` 返回前端首页
