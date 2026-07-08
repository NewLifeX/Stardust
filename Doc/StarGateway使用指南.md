# StarGateway 使用指南

> 版本：v2.0 | 日期：2026-07-08

---

## 1. 概述

StarGateway（星尘网关）是星尘分布式平台的流量网关组件，提供 Nginx 类反向代理能力。它采用**客户端代理**架构——网关实例作为 StarServer 的客户端自动获取配置，无需在每个实例上手工配置。

### 核心特性

| 特性 | 说明 | 状态 |
|------|------|:----:|
| **动态路由** | 域名/路径/请求头/方法匹配，支持通配符 | ✅ |
| **负载均衡** | 轮询/最少连接/IP Hash | ✅ |
| **TLS 终止** | HTTPS 入站解密，支持 PEM/PFX 多格式证书 | ✅ |
| **WebSocket 代理** | 透明帧转发，路由级开关，仅首次日志 | ✅ |
| **健康检查** | TCP 端口主动探测，自动摘除不健康节点 | ✅ |
| **配置热更新** | 配置变更实时生效，零中断 | ✅ |
| **StarAgent 协同** | 冷启动唤醒应用，空闲自动回收 | ✅ |
| **星尘 APM 集成** | 每次转发创建追踪 Span，完整链路追踪 | ✅ |
| **证书统一管理** | 复用星尘部署中心 SslCertificate，支持自动续期 | ✅ |
| **StarServer 注册** | 作为 AppClient 注册到 StarServer，在线可见 | ✅ |

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
    │                 │
    ▼                 ▼
 StarWeb          后端应用池
(管理后台)        (App-A/B/C)
```

---

## 2. 安装部署

### 2.1 前置条件

- .NET 10.0 Runtime
- 已部署的 StarServer（星尘服务端）
- （可选）已部署的 StarAgent（用于冷启动和空闲回收）

### 2.2 编译发布

```bash
# 克隆仓库
git clone https://github.com/NewLifeX/Stardust.git
cd Stardust

# 编译 StarGateway
dotnet build StarGateway/StarGateway.csproj -c Release

# 发布
dotnet publish StarGateway/StarGateway.csproj -c Release -o publish
```

### 2.3 配置文件

StarGateway 支持多级配置来源（优先级从高到低）：

1. 命令行参数（如 `--StarServer=http://...`）
2. 环境变量（`StarServer` / `StarAppId` / `StarSecret`）
3. `appsettings.json`
4. `config/Star.config`
5. 本地 StarAgent（UDP 5500 端口探测）

#### appsettings.json

```json
{
  "StarServer": "http://star.newlifex.com:6600",
  "StarAppId": "StarGateway",
  "StarSecret": "",

  "StarGateway": {
    "Debug": false,
    "Port": 8800,
    "LocalConfigFile": "gateway.json",
    "HealthCheckInterval": 10,
    "ConfigRefreshInterval": 15,
    "IdleTimeout": 900
  }
}
```

#### config/Star.config

```
Server=http://star.newlifex.com:6600
AppKey=StarGateway
Secret=
Debug=false
```

### 2.4 配置项说明

| 配置项 | 默认值 | 说明 |
|--------|:------:|------|
| `StarGateway:Debug` | `true` | 调试模式，开启会话级日志 |
| `StarGateway:Port` | `8800` | 监听端口 |
| `StarGateway:LocalConfigFile` | `gateway.json` | StarServer 不可达时的本地兜底配置路径 |
| `StarGateway:HealthCheckInterval` | `10` | 健康检查间隔（秒） |
| `StarGateway:ConfigRefreshInterval` | `15` | 配置刷新间隔（秒） |
| `StarGateway:IdleTimeout` | `900` | 后端空闲超时（秒），超过此时间无流量将被回收 |

### 2.5 启动

```bash
# 直接运行
cd publish
dotnet StarGateway.dll

# 指定端口
dotnet StarGateway.dll --StarGateway:Port=8080

# 指定 StarServer
dotnet StarGateway.dll --StarServer=http://192.168.1.100:6600
```

启动后日志：
```
Starting......
正在初始化星尘……
StarGateway 已连接 StarServer: http://192.168.1.100:6600
数据库初始化完成
StarGateway 已启动，监听端口 8800，远程服务器 http://192.168.1.100:6600
Application started. Press Ctrl+C to shut down.
```

---

## 3. 路由配置

### 3.1 通过 StarWeb 管理后台

1. 登录 StarWeb（星尘管理后台）
2. 进入 **网关管理** 区域
3. **先创建集群** → 定义后端服务器组（含负载均衡算法、健康检查参数）
4. **在集群中添加节点** → 后端地址（`http://localhost:5000`）
5. **创建路由** → 定义请求匹配规则并关联到集群

### 3.2 路由匹配规则

| 字段 | 说明 | 示例 |
|------|------|------|
| **域名 (Domain)** | 精确/通配符/全部 | `app.example.com` / `*.example.com` / `*` |
| **路径 (Path)** | 前缀/精确 | `/api/*` / `/api/v1/users` |
| **HTTP 方法 (Methods)** | 逗号分隔 | `GET,POST` / `GET` |
| **请求头 (Headers)** | JSON 匹配规则 | `{"X-Region": "cn*"}` |
| **WebSocket** | 是否允许 WebSocket 升级 | `true` / `false` |
| **StripPrefix** | 转发时去除匹配路径前缀 | `/api/users` → `/users` |
| **AddHeaders** | 转发时添加的请求头 | `{"X-Proxy": "StarGateway"}` |
| **优先级 (Priority)** | 数值越大越优先匹配 | `100` |

### 3.3 路由匹配顺序

每条路由有优先级（`Priority` 字段），数值越大越优先匹配。匹配顺序：

1. **域名匹配**：精确 → `*.example.com` 通配符 → `*` 全部
2. **路径匹配**：精确 → 前缀 `/api/*`
3. **HTTP 方法匹配**：指定方法 → 全部
4. **请求头匹配**：所有指定 Headers 全部匹配

### 3.4 负载均衡算法

| 算法 | 说明 | 适用场景 |
|------|------|----------|
| **RoundRobin** | 轮询选择后端节点 | 通用场景，节点性能均匀 |
| **LeastConnection** | 选择当前活跃连接数最少的节点 | 请求处理时间差异大 |
| **IPHash** | 根据客户端 IP 哈希选择节点 | 需要会话保持 |

### 3.5 本地配置文件兜底

当 StarServer 和数据库均不可达时，使用 `gateway.json` 本地兜底：

```json
[
  {
    "name": "example-app",
    "domain": "app.example.com",
    "path": "/api/*",
    "target": "http://localhost:5000"
  }
]
```

---

## 4. 证书配置

### 4.1 证书管理入口

StarGateway 的 SSL 证书统一由**星尘部署中心**的 `SslCertificate` 管理。支持：

- **PEM 格式**（通用，推荐）
- **PFX 格式**（Windows/IIS）
- **CRT+KEY 格式**（Linux/Nginx）

### 4.2 配置步骤

1. 登录 StarWeb → **部署管理** → **SSL证书**
2. 新增证书：
   - 域名：`*.example.com`（支持通配符）
   - 上传证书文件（PEM / PFX / CRT+KEY）
   - 设置启用状态
3. 网关启动时自动加载所有启用证书
4. 证书变更后，网关在下次配置刷新周期（默认15秒）自动热加载

### 4.3 证书与发布中心的关系

StarGateway 不再维护独立的 `GatewayCert` 表，而是**统一使用部署中心的 `SslCertificate`**。这意味着：

- 证书只需在部署中心上传一次，网关和发布系统共享使用
- 证书自动续期（Let's Encrypt / 阿里云）后，网关自动获取最新证书
- 支持按域名 SNI 匹配多证书

> **注意**：`GatewayCert` 表已废弃，现有数据建议迁移到 `SslCertificate`。

---

## 5. StarAgent 协同

### 5.1 配置前提

确保本机运行 StarAgent，StarGateway 自动通过 `http://127.0.0.1:5500` 与其通信。

### 5.2 冷启动（应用唤醒）

当请求到达但后端端口未监听时：

```
1. Gateway → 探测后端端口 → 未监听
2. Gateway → 返回 503 Service Unavailable
3. Gateway → 调用 StarAgent StartService API
4. StarAgent → 启动后端应用进程
5. 应用就绪后 → 后续请求正常转发
```

### 5.3 空闲回收

后端持续无流量超过 `IdleTimeout`（默认 900 秒/15 分钟）：

```
1. Gateway → 检测到后端超过 IdleTimeout 无活动
2. Gateway → 调用 StarAgent StopService API
3. StarAgent → 发送 SIGTERM（优雅关闭）
4. 超时未退出 → SIGKILL（强制杀死）
5. 资源释放
```

---

## 6. 运行监控

### 6.1 本地 Admin API

StarGateway 内置管理 API（仅限本地访问）：

```bash
# 运行状态
curl http://127.0.0.1:8800/api/status

# 路由列表
curl http://127.0.0.1:8800/api/routes

# 手动刷新配置（路由+证书）
curl http://127.0.0.1:8800/api/refresh
```

`/api/status` 响应示例：
```json
{
  "uptime": 3600,
  "activeSessions": 5,
  "totalRequests": 1024,
  "routeCount": 8,
  "port": 8800
}
```

### 6.2 访问日志

- 每次请求自动记录到 AdminLog
- 格式：`{METHOD} {path} -> {target}:{port} [{routeName}]`
- WebSocket 仅首次升级记录日志
- 通过 `XTrace.Log` 输出到控制台/文件

### 6.3 链路追踪

- 每次转发创建 `gateway:{METHOD}:{path}` Span
- 自动透传 `Trace-Id` / `traceparent` 请求头
- 可在星尘 APM 中查看完整调用链路

### 6.4 Prometheus 指标

当前版本暂未暴露 Prometheus 格式指标。可通过 `/api/status` JSON API 由 StarAgent 采集。

---

## 7. StarServer 对接

### 7.1 注册到 StarServer

配置 `appsettings.json` 中的 `StarServer` 地址和 `StarAppId`，StarGateway 启动后自动：

1. 创建 `StarFactory` 初始化 `AppClient`
2. 连接到 StarServer 进行登录认证
3. 在 StarServer 的应用管理页面上显示为在线客户端
4. 通过心跳维持连接

### 7.2 配置加载优先级

```
1. StarServer API (GET /gateway/config) — 预留扩展
2. 本地数据库 (XCode ORM) — 当前主要方式
3. 本地配置文件 (gateway.json) — 离线兜底
```

### 7.3  StarWeb 管理后台

在 StarWeb 中可以：

- **网关管理** → 管理路由、集群、节点
- **部署管理** → 管理 SSL 证书（SslCertificate）
- **应用管理** → 查看 StarGateway 在线状态
- **链路追踪** → 查看网关转发的请求链路

---

## 8. 常见问题 FAQ

### Q: StarGateway 启动后端口被占用？

```
Error: System.Net.Sockets.SocketException (10048): 通常每个套接字地址
(协议/网络地址/端口) 只允许使用一次。
```

**解决**：修改 `appsettings.json` 中的 `StarGateway:Port`，或关闭占用端口的程序。

### Q: 路由配置修改后未生效？

**原因**：StarGateway 默认每 15 秒从数据库刷新配置。

**解决**：
- 等待自动刷新周期
- 调用 `curl http://127.0.0.1:8800/api/refresh` 手动触发
- 减少 `ConfigRefreshInterval` 配置值

### Q: 后端节点显示不健康？

**检查项**：
- 确认后端应用已启动并可访问
- 确认网络防火墙允许端口访问
- 健康检查默认 TCP 端口探测，确保端口可连接
- 查看 StarGateway 日志中的健康检查结果

### Q: 证书加载失败？

**检查项**：
- 确认证书文件路径正确
- 确认证书未过期
- 确认私钥文件与证书匹配
- PEM 文件需包含 `-----BEGIN CERTIFICATE-----` 和 `-----BEGIN PRIVATE KEY-----`
- 查看启动日志中的证书加载错误信息

### Q: 如何查看当前版本？

```bash
dotnet StarGateway.dll --version
```

或查看二进制文件属性。

### Q: 如何实现 HTTPS 访问？

1. 在 StarWeb → 部署管理 → SSL证书 中上传证书
2. 确保证书域名与客户端访问的域名匹配
3. 重启或等待配置刷新，网关自动加载证书
4. 客户端通过 `https://your-domain.com:8800` 访问

### Q: StarGateway 支持 HTTP/2 吗？

当前版本仅支持 HTTP/1.1 和 WebSocket。HTTP/2 支持在路线图中。

### Q: 如何处理 StarServer 故障？

StarGateway 采用多级兜底策略：
1. StarServer API 不可用 → 回退到本地数据库读取
2. 数据库不可用 → 回退到本地 `gateway.json` 文件
3. 配置文件不可用 → 启动失败并报错

---

## 9. 相关文档

| 文档 | 说明 |
|------|------|
| [StarGateway架构.md](StarGateway架构.md) | 架构设计文档，含 Mermaid 架构图和交互时序 |
| [StarGateway需求文档.md](StarGateway需求文档.md) | 需求规格说明 |
| [StarGateway功能清单.md](StarGateway功能清单.md) | 功能完成状态追踪 |
| [StarGateway竞品分析.md](StarGateway竞品分析.md) | 竞品对比与差距分析 |

---

> 更多信息请访问 [星尘平台](https://newlifex.com) | [GitHub 仓库](https://github.com/NewLifeX/Stardust)
