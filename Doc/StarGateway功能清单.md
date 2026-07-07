# StarGateway 功能清单

> 版本：v1.0 | 日期：2026-07-08

> 状态标记：✅ 已实现 | 🟡 部分实现 | 🔧 规划中 | ⏸ 暂缓/占位 | ❌ 未开始

本清单维护"核心目标 → 功能模块/子模块 → 完成状态"。模块名对齐[需求文档](StarGateway需求文档.md)第 3 章的功能点名称。

---

## M1 动态路由与流量转发

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M1-1 | 域名路由 | ✅ | GatewayRoute.MatchDomain 支持精确/通配符/* |
| M1-2 | 路径路由 | ✅ | GatewayRoute.MatchPath 支持前缀/精确匹配 |
| M1-3 | 请求头/查询参数路由 | ✅ | GatewayRoute.MatchHeaders 支持通配符/精确/包含匹配 |
| M1-4 | 负载均衡（轮询/最少连接/IP Hash） | 🟡 | 三种算法已实现，最少连接为简化版 |
| M1-5 | TLS 终止（HTTPS + SNI） | ❌ | GatewayCert 模型已定义，运行时待接入 |
| M1-6 | 健康检查（主动+被动） | ❌ | 节点状态字段已定义，检查逻辑待实现 |

## M2 集中配置管理

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M2-1 | StarServer 配置下发 | 🟡 | GatewayService + GatewayController API 已创建 |
| M2-2 | 本地配置文件兜底 | ❌ | — |
| M2-3 | 配置热更新 | ✅ | HttpReverseProxy 定时15秒刷新路由 |
| M2-4 | 本地 Admin API | ✅ | /__admin/status, /__admin/routes, /__admin/refresh |

## M3 StarAgent 协同

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M3-1 | 本地 StarAgent 通信 | ❌ | — |
| M3-2 | 端口探测 | ❌ | 架构文档已有设计，代码未实现 |
| M3-3 | 应用唤醒（冷启动） | ❌ | 架构文档已有流程设计 |
| M3-4 | 空闲回收 | ❌ | 架构文档已有流程设计 |

## M4 可观测性

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M4-1 | 访问日志 | ✅ | AdminLog 记录请求方法/路径/目标/路由名 |
| M4-2 | 星尘 APM 集成 | ❌ | — |
| M4-3 | 运行指标暴露 | ✅ | /__admin/status 返回会话数/请求数/路由数/运行时间 |

---

## 已实现（基础框架）

| 编码 | 功能 | 说明 |
|------|------|------|
| — | TCP/UDP 代理引擎 | `ProxySession` 双向数据转发完整可用 |
| — | NAT 代理 | `NATProxy` 固定目标 TCP/UDP 转发 |
| — | HTTP 反向代理基础 | `HttpReverseProxy` + `HttpReverseSession`，HTTP 请求解析完成 |
| — | 轻量服务主机 | `Host` + `IHostedService` 生命周期管理 |
| — | 配置模型 | `StarGatewaySetting`（`Config<T>` 基类） |

## 需修复

| 位置 | 问题 |
|------|------|
| `HttpReverseProxy.cs` | HTTP 头部修改代码被注释，`OnRequest` 定义了但未连线 |
| `HttpReverseProxy.cs` | `WriteDebugLog(LocalUri + "")` 在 DEBUG 下会 NRE |
| `MyService.cs` | 端口硬编码为 8080，`StarGatewaySetting.Port` 未使用 |
| `InitService.cs` | 数据库初始化 fire-and-forget，存在竞态条件 |
| `Properties/PublishProfiles/` | 发布配置目标 `netcoreapp3.1`，项目实际为 `net10.0` |

## 统计

| 模块 | 功能数 | 已完成 | 完成率 |
|------|:-----:|:-----:|:-----:|
| M1 动态路由与流量转发 | 6 | 3 | 50% |
| M2 集中配置管理 | 4 | 3 | 75% |
| M3 StarAgent 协同 | 4 | 0 | 0% |
| M4 可观测性 | 3 | 2 | 67% |
| **总计** | **17** | **8** | **47%** |
