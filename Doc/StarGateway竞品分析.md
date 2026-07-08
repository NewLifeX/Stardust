# StarGateway 竞品分析报告

> 版本：v1.0 | 日期：2026-07-08

---

## 1. 概述

本报告对比 **StarGateway**（星尘网关）与市面上主流的反向代理 / API 网关竞品，覆盖动态配置、集中管理、服务发现集成、路由能力等关键维度。

StarGateway 定位为**连接中心管理服务器（StarServer）的客户端代理应用**，与 Envoy 的 xDS 客户端 + 控制面模式架构思路最为接近，但更轻量且与星尘平台原生集成。支持：
- 网关实例作为 StarServer 的客户端，自动获取配置
- 与 StarAgent 协同，实现应用按需启停和空闲回收
- 与星尘注册中心协同，实现服务发现和负载均衡

| 项目 | 类型 | 语言/技术栈 | 许可证 | 最新版本 | GitHub Stars | 一句话定位 |
|------|------|-----------|--------|---------|-------------|-----------|
| **StarGateway** | 开源 | C# / .NET 10 | MIT | v3.0.2026.0708 | — | 连接中心管理服务器的动态反向代理客户端 |
| **YARP** | 开源 | C# / .NET | MIT | v2.3.0 | 9,550 | 微软官方 .NET 高性能反向代理工具包 |
| **Ocelot** | 开源 | C# / ASP.NET Core | MIT | v24.0 (dev) | 8,715 | .NET API 网关 |
| **Nginx** | 开源 | C | BSD-2 | 1.27.x | 31,120 | 高性能 Web 服务器 / 反向代理 |
| **Nginx Plus** | 商业 | C | 商业授权 | R32 | — | Nginx 企业版（动态配置 + 管理 API） |
| **Kong** | 开源+商业 | Lua / OpenResty | Apache 2.0 | 3.9.x | 43,753 | 云原生 API 网关 + AI 网关 |
| **Traefik** | 开源 | Go | MIT | v3.x | 63,893 | 云原生应用代理（自动服务发现） |
| **Envoy** | 开源 | C++ | Apache 2.0 | 1.33.x | 28,532 | 云原生高性能代理（服务网格数据面标准） |
| **Apache APISIX** | 开源 | Lua / OpenResty | Apache 2.0 | 3.12.x | 16,824 | 云原生 API 网关（etcd 驱动动态配置） |
| **HAProxy** | 开源 | C | GPL-2 | 3.1-dev | 6,679 | TCP/HTTP 负载均衡器 |
| **Fabio** | 开源 | Go | MIT | v1.6.x | 7,334 | Consul 负载均衡器 |

> 注：GitHub Stars 为 2026-07 月近似值，完整列表见各仓库。

---

## 2. 各竞品详细分析

### 2.1 YARP（微软反向代理）

- **仓库**：https://github.com/dotnet/yarp
- **描述**：微软官方开发的 .NET 高性能反向代理，作为 ASP.NET Core 中间件运行

**配置方式**：通过 `appsettings.json` / `IConfiguration` 配置路由和集群。内建 `IProxyConfigProvider` 接口，可实现自定义提供者从远程源（数据库/API）动态获取配置，配置变更自动热重载无需重启。

**动态配置**：✅ 内建 `IConfigChangeToken` 机制，自定义 `IProxyConfigProvider` 可实现任意后端（数据库/Redis/API）的配置热更新。

**集中管理**：❌ 无内置管理界面和中心管理服务器，需自行实现。

**服务发现**：基于 ASP.NET Core 的 `HttpClient` 工厂，可通过 `IReverseProxyServiceMetadata` 集成 Consul / Eureka / K8s。

**优势**：微软官方维护、.NET 原生高性能 Socket 实现、丰富的中间件管道、支持 gRPC/WebSocket、配置热更新接口设计简洁。

**劣势**：无内置管理 UI、无插件体系、集中管理需自建、托管在 ASP.NET Core 管道中性能有上限。

**⭐ 参考价值**：`IProxyConfigProvider` 的配置热更新接口设计是 .NET 生态中可直接参考的模式。

### 2.2 Ocelot

- **仓库**：https://github.com/ThreeMammals/Ocelot
- **描述**：基于 ASP.NET Core 的 .NET API 网关

**配置方式**：本地 JSON 文件（`ocelot.json`），支持 Administration API 运行时重载配置。

**动态配置**：⚠️ 可通过自定义 `FileConfigurationRepository` 从数据库/API 获取配置，调用 Administration API 触发重载；重载过程非零中断。

**集中管理**：❌ 无内置管理界面，需自行构建。

**服务发现**：Consul、Eureka、Kubernetes、Service Fabric、自定义提供者。

**优势**：纯 .NET 生态、丰富的路由/聚合/认证/限流功能、成熟的社区积累。

**劣势**：配置变更需重载、性能受限于 ASP.NET Core 管道、零中断重载支持不足。

### 2.3 Nginx / Nginx Plus

- **仓库**：https://github.com/nginx/nginx
- **描述**：全球最流行的开源反向代理 / Web 服务器

**配置方式**：静态配置文件（`nginx.conf`），修改后需 `nginx -s reload` 进行重载。

**动态配置**：开源版 ❌ 配置修改需 reload（但有零中断能力）。Nginx Plus ✅ 提供 `/api/` 端点动态修改 upstream 服务器，无需 reload。OpenResty 生态通过 Lua 脚本可实现部分动态能力。

**集中管理**：开源版 ❌ 无内置集中管理。Nginx Plus 有 Nginx Controller 商业产品管理多个实例。社区方案有 Nginx Amplify 监控。

**服务发现**：开源版 ❌ 无原生服务发现，需通过 DNS SRV 或 Lua 脚本实现。Nginx Plus ✅ 支持 DNS SRV 主动健康检查。

**优势**：极致性能与稳定性、极低资源消耗（~1MB 二进制）、丰富的第三方模块生态、极高的市场占有率。

**劣势**：配置热更新需 Nginx Plus 或有额外开发、Lua 扩展开发门槛高、无原生 .NET 集成。

### 2.4 Kong

- **仓库**：https://github.com/Kong/kong
- **描述**：云原生 API 网关，基于 OpenResty（Nginx + LuaJIT）

**配置方式**：Restful Admin API 增删改查 Route/Service/Upstream/Plugin 等实体，所有配置变更实时生效。支持 DB-less 模式通过声明式配置文件。

**动态配置**：✅ 全动态——Admin API 修改实时生效，无需重载。支持 CP/DP（控制面/数据面）分离部署。

**集中管理**：✅ 内建 Admin API + Kong Manager Web UI（企业版增强）。Kong Konnect 云管理平台。

**服务发现**：✅ 内置 DNS 解析。企业版支持 Consul、K8s 等。

**优势**：丰富的插件生态（认证/限流/日志/转换/AI 代理）、优雅的 Admin API 设计、CP/DP 分离架构、AI 网关能力。

**劣势**：核心基于 Lua，扩展需 Lua 编程、依赖 PostgreSQL/Cassandra、资源占用相对较高、Nginx 抽象层调试复杂性。

**⭐ 参考价值**：Admin API + 插件体系的设计模式，StarGateway 可定义类似 `GatewayRoute` / `Upstream` / `Plugin` 实体并暴露 Admin API。

### 2.5 Traefik

- **仓库**：https://github.com/traefik/traefik
- **描述**：云原生应用代理，自动从编排器发现服务并生成路由

**配置方式**：自动从 Docker / Kubernetes / Consul / etcd 发现服务并生成路由，也支持静态文件配置。

**动态配置**：✅ 全动态——服务变更自动触发路由更新，零中断热切换。支持 Provider 模型，可从多种后端获取配置。

**集中管理**：⚠️ 内置 Web UI Dashboard 显示路由/服务/健康状态。Traefik Hub 提供企业级 API 管理平台。

**服务发现**：✅ 原生集成 Docker / K8s / Consul / etcd / ECS / Nomad 等 20+ Provider，自动监听变更。

**优势**：自动服务发现体验极佳、自动 Let's Encrypt TLS、Go 单二进制部署、内置 Prometheus/OpenTelemetry 指标。

**劣势**：核心路由能力不如 Nginx 丰富、高并发场景性能不如 Nginx/Envoy、配置灵活性受限于 Provider 模型。

**⭐ 参考价值**：Provider 模型（自动从多种后端发现服务生成路由）与 StarGateway 的"从 StarServer 获取配置"模式高度相似，可参考其 Provider 抽象设计。

### 2.6 Envoy

- **仓库**：https://github.com/envoyproxy/envoy
- **描述**：CNCF 毕业项目，C++ 实现的高性能代理，服务网格（Istio）的默认数据面

**配置方式**：通过 xDS API（基于 gRPC 流）或静态文件下发配置。xDS 包括 LDS（Listener）、CDS（Cluster）、RDS（Route）、EDS（Endpoint）、SDS（Secret）等子系统。

**动态配置**：✅ 全动态——xDS API 支持热更新所有资源配置；控制面（如 Istiod）推送增量更新，零中断。Envoy 本身是无状态的 xDS 客户端。

**集中管理**：✅ 需外部控制面（Istio / Consul Connect / 自行实现）提供配置。控制面聚合多个配置源并转化为 xDS 协议下发给所有 Envoy 实例。

**服务发现**：✅ 通过 EDS（Endpoint Discovery Service）集成任意注册中心。

**优势**：极致性能（C++）、最丰富的网络过滤器体系（HTTP/gRPC/MongoDB/Redis/Thrift 等七层协议）、服务网格标准数据面。**xDS 客户端 + 控制面的架构模式与 StarGateway 的"客户端代理"模式最为接近**。

**劣势**：配置极其复杂（数百 Protobuf 字段）、必须配合控制面使用、调试困难、资源占用高于 Nginx。

**⭐ 参考价值**：xDS 协议设计——控制面-数据面分离的思想与 StarGateway 的"网关实例连接 StarServer 获取配置"完全一致。可参考其 LDS/RDS/CDS/EDS 的资源模型划分，以及增量更新推送模式。

### 2.7 Apache APISIX

- **仓库**：https://github.com/apache/apisix
- **描述**：Apache 基金会毕业的云原生 API 网关

**配置方式**：Restful Admin API 实时配置，基于 etcd 存储。也支持声明式文件。

**动态配置**：✅ 全动态——所有 Route/Service/Upstream/Plugin 变更通过 Admin API 实时生效，变更自动同步到所有网关节点。

**集中管理**：✅ 内建 Admin API + Dashboard Web UI。APISIX Ingress Controller 对接 K8s。

**服务发现**：✅ 通过插件支持 Consul / Nacos / ZooKeeper / Eureka / K8s。

**优势**：超高吞吐、80+ 内置插件、插件热加载、低延迟、AI 代理支持。

**劣势**：依赖 etcd 集群、Lua 开发门槛、社区版 Dashboard 功能有限。

### 2.8 HAProxy

- **仓库**：https://github.com/haproxy/haproxy
- **描述**：老牌 TCP/HTTP 负载均衡器，以极致稳定性和性能著称

**配置方式**：静态配置文件（`haproxy.cfg`），支持 Unix Socket Runtime API 部分动态修改（server 权重/状态等）。

**动态配置**：⚠️ 路由 / 监听器等核心配置需 reload，Runtime API 可动态启停 server、修改权重、切换维护模式。

**集中管理**：❌ 无内置集中管理。HAProxy Enterprise 有管理平台。社区有 Data Plane API（RESTful）。

**服务发现**：❌ 无原生服务发现，需通过 Data Plane API 或 Service Discovery Agent（如 consul-haproxy）实现。

**优势**：极致性能（零拷贝转发）、极低延迟、最大单机连接数极高、稳定性口碑极佳。

**劣势**：核心配置静态需 reload、无原生管理 UI、路由功能较基础、扩展能力有限。

### 2.9 Fabio

- **仓库**：https://github.com/fabiolb/fabio
- **描述**：Go 实现的最小化 HTTP 负载均衡器，专门与 Consul 集成

**配置方式**：自动从 Consul 发现服务，从 Consul KV Store 读取路由配置。

**动态配置**：✅ 全动态——Consul 变更即时反应，路由表热更新。

**集中管理**：⚠️ 依赖 Consul 作为配置中心，内建简洁 Web UI 显示路由/状态。

**服务发现**：✅ 仅支持 Consul（无其他提供者）。

**优势**：极简设计、与 Consul 深度集成、单二进制、自动 Let's Encrypt。

**劣势**：仅支持 Consul、功能有限（无认证/限流/转换）、维护活跃度一般。

**⭐ 参考价值**：与 StarGateway 同为"客户端代理"模式（Fabio 连接到 Consul 获取配置）。其失败模式——强依赖单一配置源且无本地兜底——值得警惕。

---

## 3. 功能对比矩阵

### 3.1 动态配置与集中管理

| 功能维度 | StarGateway | YARP | Ocelot | Nginx(+) | Kong | Traefik | Envoy | APISIX | HAProxy | Fabio |
|---------|:-----------:|:----:|:------:|:---------:|:----:|:-------:|:-----:|:------:|:-------:|:-----:|
| **配置热更新** | ✅ 中心下发 | ✅ IConfigProvider | ⚠️ API 重载 | ⚠️ Plus | ✅ Admin API | ✅ 自动发现 | ✅ xDS | ✅ Admin API | ⚠️ Runtime API | ✅ Consul |
| **零中断重载** | ✅ | ✅ | ⚠️ | ⚠️ Plus | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **中心管理 Server** | ✅ StarServer | ❌ 需自建 | ❌ 需自建 | ⚠️ Controller | ✅ Manager | ⚠️ Hub | ✅ 控制面 | ✅ Dashboard | ❌ | ❌ Consul |
| **网关实例为客户端** | ✅ | ❌ 独立进程 | ❌ 独立进程 | ❌ 独立进程 | ❌ CP/DP | ❌ 独立进程 | ✅ xDS 客户端 | ❌ 独立进程 | ❌ 独立进程 | ✅ Consul 客户端 |
| **数据库存储配置** | ✅ XCode ORM | ⚠️ 需自建 | ⚠️ 需自建 | ❌ 文件 | ✅ PostgreSQL | ⚠️ KV Store | ⚠️ 控制面 | ✅ etcd | ❌ 文件 | ✅ Consul KV |
| **配置源热切换** | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **本地配置兜底** | ✅ 配置文件 | ✅ 文件 | ✅ 文件 | ✅ 文件 | ⚠️ DB-less | ✅ 文件 | ⚠️ 静态文件 | ✅ 声明式 | ✅ 文件 | ❌ 仅 Consul |
| **管理 API** | ❌ | ❌ | ⚠️ Admin API | ✅ Plus | ✅ Admin API | ❌ | ✅ Admin | ✅ Admin | ⚠️ Runtime | ❌ |

### 3.2 路由与协议支持

| 功能维度 | StarGateway | YARP | Ocelot | Nginx(+) | Kong | Traefik | Envoy | APISIX | HAProxy | Fabio |
|---------|:-----------:|:----:|:------:|:---------:|:----:|:-------:|:-----:|:------:|:-------:|:-----:|
| **HTTP/1.1** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **HTTP/2** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **HTTP/3** | ❌ | ❌ | ❌ | ✅ | ⚠️ | ❌ | ✅ | ⚠️ | ❌ | ❌ |
| **WebSocket** | ✅ v3.0 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **gRPC** | ❌ | ✅ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **TCP/UDP 代理** | ✅ | ❌ | ❌ | ✅ | ⚠️ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| **TLS 终止** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **路径/头/查询路由** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ |
| **SNI 多证书** | ❌ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |

### 3.3 服务发现与负载均衡

| 功能维度 | StarGateway | YARP | Ocelot | Nginx(+) | Kong | Traefik | Envoy | APISIX | HAProxy | Fabio |
|---------|:-----------:|:----:|:------:|:---------:|:----:|:-------:|:-----:|:------:|:-------:|:-----:|
| **内置服务发现** | ✅ 星尘注册中心 | ⚠️ 需扩展 | ✅ Consul/Eureka/K8s | ❌ | ⚠️ 插件 | ✅ 原生 | ✅ EDS | ✅ 插件 | ❌ | ✅ Consul |
| **健康检查（主动）** | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **健康检查（被动）** | ❌ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **负载均衡算法** | ❌ 固定转发 | ✅ 轮询/最少请求/随机/幂等优先 | ✅ 轮询/最少连接/Cookie 粘性 | ✅ 轮询/最少连接/IP Hash/通用 Hash | ✅ 轮询/最少连接/一致性哈希/最小延迟 | ✅ 轮询/最少连接/WRR/P2C | ✅ 轮询/最少请求/环形哈希/Maglev/随机 | ✅ 轮询/最少连接/一致性哈希/EWMA | ✅ 轮询/最少连接/源 IP Hash/URI Hash | ✅ 轮询/最少连接/随机 |
| **断路器** | ❌ | ⚠️ Polly | ✅ Polly | ❌ | ✅ 插件 | ✅ | ✅ | ✅ 插件 | ❌ | ❌ |
| **限流** | ❌ | ❌ | ✅ | ✅ Plus | ✅ 插件 | ✅ | ✅ | ✅ 插件 | ✅ | ❌ |
| **认证（JWT/Key）** | ❌ | ⚠️ 中间件 | ✅ JWT | ✅ Plus | ✅ 插件 | ✅ 中间件 | ✅ 过滤器 | ✅ 插件 | ❌ | ❌ |

### 3.4 可观测性

| 功能维度 | StarGateway | YARP | Ocelot | Nginx(+) | Kong | Traefik | Envoy | APISIX | HAProxy | Fabio |
|---------|:-----------:|:----:|:------:|:---------:|:----:|:-------:|:-----:|:------:|:-------:|:-----:|
| **访问日志** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **星尘 APM 链路追踪** | ✅ 原生集成 | ⚠️ 自定义 | ⚠️ | ❌ | ✅ | ✅ | ✅ 原生 | ✅ 插件 | ❌ | ✅ |
| **Prometheus 指标** | ❌ | ✅ | ❌ | ✅ | ✅ 插件 | ✅ 原生 | ✅ 原生 | ✅ 插件 | ✅ | ✅ |
| **OpenTelemetry** | ❌ | ⚠️ | ❌ | ❌ | ✅ | ✅ | ✅ 原生 | ✅ 插件 | ❌ | ✅ |
| **运行状态 API** | ❌ | ❌ | ❌ | ✅ Plus | ✅ Admin | ✅ Dashboard | ✅ Admin | ✅ Admin | ⚠️ Stats | ✅ |
| **管理 Web UI** | ❌ 依赖 StarWeb | ❌ | ❌ | ✅ Plus | ✅ Manager | ✅ Dashboard | ✅ Admin | ✅ Dashboard | ❌ | ✅ |

> 标记说明：✅ 完整支持 | ⚠️ 部分可用/需扩展 | ❌ 不支持

---

## 4. 非功能维度对比

| 维度 | StarGateway | YARP | Ocelot | Nginx | Kong | Traefik | Envoy | APISIX | HAProxy | Fabio |
|------|:----------:|:----:|:------:|:-----:|:----:|:-------:|:-----:|:------:|:-------:|:-----:|
| **外部依赖** | 最少（NewLife.Core + Stardust） | 中（.NET ASP.NET Core） | 中（ASP.NET Core） | 无 | 高（PostgreSQL/Cassandra） | 低（单二进制） | 高（需控制面） | 高（etcd） | 无 | 低（需 Consul） |
| **二进制大小** | ~5MB | ~10MB | ~15MB | ~1MB | ~40MB | ~20MB | ~50MB | ~30MB | ~1MB | ~10MB |
| **运行时内存** | ~20MB | ~50MB | ~60MB | ~5MB | ~80MB | ~30MB | ~100MB | ~70MB | ~5MB | ~20MB |
| **框架兼容性** | .NET 10 | .NET | .NET | — | — | — | — | — | — | — |
| **许可证** | MIT | MIT | MIT | BSD-2 | Apache-2.0 | MIT | Apache-2.0 | Apache-2.0 | **GPL-2** | MIT |
| **许可证风险** | ✅ 低 | ✅ 低 | ✅ 低 | ✅ 低 | ✅ 低 | ✅ 低 | ✅ 低 | ✅ 低 | ⚠️ 注意 | ✅ 低 |
| **维护状态** | ✅ 活跃 | ✅ 活跃（微软） | ✅ 活跃 | ✅ 活跃 | ✅ 活跃 | ✅ 活跃 | ✅ 活跃 | ✅ 活跃 | ✅ 活跃 | ⚠️ 维护模式 |
| **最近发版** | 2026-07 | 2026-Q2 | 2026-Q1 | 2026-06 | 2026-Q2 | 2026-Q2 | 2026-Q2 | 2026-Q2 | 2026-Q2 | 2024 |
| **学习曲线** | 低 | 中 | 中 | 中-高 | 中-高 | 中 | 极高 | 中-高 | 中 | 低 |
| **.NET 原生集成** | ✅ 原生 | ✅ 原生 | ✅ 原生 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 5. 架构模式对比：客户端代理 vs 独立进程

### 核心维度

| 维度 | 客户端代理模式（StarGateway / Envoy / Fabio） | 独立进程模式（Nginx / Traefik / HAProxy） |
|------|----------------------------------------------|------------------------------------------|
| **配置来源** | 连接中心 Server / 控制面获取 | 本地文件 / 直接读取注册中心 |
| **配置同步** | 接收 Server 推送（增量变更） | 轮询 / Watch 注册中心变更 |
| **多实例管理** | 中心 Server 统一推送，一致性保证 | 各实例独立拉取，需额外协调 |
| **离线容错** | 本地缓存配置，Server 恢复后同步 | 本地文件始终可用 |
| **典型代表** | Envoy（xDS）、Fabio（Consul） | Nginx、Traefik、HAProxy |

StarGateway 选择客户端代理模式的理由：

1. **与星尘平台架构一致**：StarAgent、Stardust SDK 均采用此模式连接 StarServer
2. **配置一致性强**：中心 Server 统一下发，避免各网关实例配置不一致
3. **运维简化**：在 StarWeb 上配置一次即推送到所有网关实例
4. **可离线兜底**：参考 StarAgent 的本地配置文件模式，Server 不可达时仍可用

---

## 6. 差距分析

### 6.1 StarGateway 优势领域

1. **架构创新**——网关实例作为客户端连接中心管理服务器，是独特的"客户端代理"模式。与 Envoy 的 xDS 客户端 + 控制面架构思路一致，但 StarGateway 更轻量（无 Protobuf / gRPC 依赖），且内建于星尘平台。
2. **超轻量依赖**——基于 NewLife.Core 的网络栈，零外部重量级依赖。对比 Kong（依赖 PostgreSQL/Cassandra）、APISIX（依赖 etcd）、Envoy（需控制面），StarGateway 单进程即可运行。
3. **星尘平台一体化**——天然集成星尘的配置中心、注册中心、APM 链路追踪、日志中心。其他竞品需额外集成多个系统。
4. **.NET 生态原生**——对 .NET 开发者友好，可直接复用 NewLife 系列组件（MemoryCache、TimerX、ApiHttpClient 等）。

### 6.2 差距领域（按优先级排序）

| 优先级 | 缺失功能 | 对标竞品 | 建议 |
|--------|---------|---------|------|
| **P0** | **路由规则丰富度**（路径/头/方法/查询匹配） | Ocelot / YARP | 实现类似 ASP.NET Core 路由的配置模型，支持 `{controller}/{action}` 模板和约束 |
| **P0** | **负载均衡算法**（当前仅固定转发） | 所有竞品 | 优先实现轮询（RoundRobin）、最少连接（LeastConnection）、一致性哈希（IP Hash） |
| **P0** | **TLS 终止（HTTPS）** | 所有竞品 | 支持 HTTPS 入站监听和出站转发，集成 StarServer 的 SSL 证书管理 |
| **P1** | **健康检查（主动+被动）** | Traefik / Kong | 实现定时心跳探测 + 连续失败自动摘除 |
| **P1** | **动态路由热更新** | Kong / YARP | 从 StarServer 订阅路由配置变更，零中断生效 |
| **P1** | **管理 API** | Kong Admin API | 提供 RESTful API 查询网关状态（路由表、后端健康、连接数） |
| **P1** | **限流 / 断路器** | Ocelot / Kong | 集成或自建基础限流、熔断能力 |
| **P2** | **HTTP/2 支持** | YARP / Nginx | 协议升级，提升多路复用效率 |
| **P2** | **认证过滤器（JWT/API Key）** | Ocelot / Kong | 在网关层完成身份验证 |
| **P2** | **管理 Web UI** | Traefik / Kong | 基于 StarWeb 扩展网关可视化配置 |
| **~~P3~~ ✅** | **WebSocket 代理** | 多数竞品 | ✅ v3.0 已支持：升级握手透传 + 透明帧转发 + 路由级开关 + 仅首次日志 |
| **P3** | **Prometheus / OpenTelemetry 指标** | Traefik / Envoy | 暴露标准可观测性指标 |

### 6.3 竞品值得关注的设计亮点

| 竞品 | 亮点 | StarGateway 可借鉴处 |
|------|------|-------------------|
| **YARP** | `IProxyConfigProvider` 接口设计 | 实现类似的 `IGatewayConfigProvider` 抽象，支持多种配置源 |
| **Kong** | Admin API + 插件体系 | 定义 `GatewayRoute` / `Upstream` 实体，暴露标准 RESTful 管理 API |
| **Traefik** | Provider 模型自动服务发现 | 实现"StarServer Provider"自动从星尘注册中心同步服务→路由映射 |
| **Envoy** | xDS 协议（LDS/RDS/CDS/EDS） | 参考其资源模型划分：Listener（监听端口）、Route（路由规则）、Cluster（后端集群）、Endpoint（后端节点） |
| **Fabio** | 极简设计 + Consul 深度集成 | 参考其与 Consul 的集成深度，但**必须保留本地配置兜底**（避免 Fabio 仅靠 Consul 的失败模式） |

---

## 7. 结论与建议

### 7.1 StarGateway 的差异化定位

在"中心管理服务器下发的动态配置"这一场景中，StarGateway 是唯一与 .NET / 星尘平台原生集成的轻量级解决方案。非 .NET 竞品（Kong / Traefik / Envoy）均需额外适配才能接入星尘配置中心和注册中心。

### 7.2 核心建议

1. **短期（P0）**：补齐路由规则丰富度、负载均衡算法、TLS 终止三项基础能力，达到可用状态
2. **中期（P1）**：利用星尘配置中心和注册中心，实现路由配置热更新（从 StarServer 订阅变更）+ 健康检查
3. **参考 YARP**：`IProxyConfigProvider` 设计简化配置热更新实现
4. **参考 Traefik**：Provider 模型，实现"从星尘注册中心自动发现服务并生成路由"的能力
5. **参考 Envoy**：xDS 资源模型（Listener → Route → Cluster → Endpoint）抽象，为后续扩展打下基础
6. **长期（P2~P3）**：逐步补齐限流、认证、HTTP/2、管理 Web UI 等高级功能
