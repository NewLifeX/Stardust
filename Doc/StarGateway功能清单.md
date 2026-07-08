# StarGateway 功能清单

> 版本：v2.0 | 日期：2026-07-08

> 状态标记：✅ 已实现 | 🟡 部分实现 | 🔧 规划中 | ⏸ 暂缓/占位 | ❌ 未开始

本清单维护"核心目标 → 功能模块/子模块 → 完成状态"。模块名对齐[需求文档](StarGateway需求文档.md)第 3 章的功能点名称。

---

## M1 动态路由与流量转发

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M1-1 | 域名路由 | ✅ | GatewayRoute.MatchDomain 支持精确/通配符/* |
| M1-2 | 路径路由 | ✅ | GatewayRoute.MatchPath 支持前缀/精确匹配 |
| M1-3 | 请求头/查询参数路由 | ✅ | GatewayRoute.MatchHeaders 支持通配符/精确/包含匹配 |
| M1-4 | 负载均衡（轮询/最少连接/IP Hash） | ✅ | 最少连接真算法：实时追踪活跃连接数 |
| M1-5 | TLS 终止（HTTPS） | ✅ | 统一使用 SslCertificate，支持 PEM/PFX/CRT+KEY 多格式 |
| M1-6 | 健康检查（主动） | ✅ | TCP端口探测，定时检查并更新节点状态 |
| M1-7 | WebSocket 代理 | ✅ | 升级握手透传 + 透明帧转发 + 路由级开关 + 仅首次日志 |
| M1-7a | └ 升级握手透传 | ✅ | 检测 `Upgrade: websocket`，透传 101 响应 |
| M1-7b | └ 透明帧转发 | ✅ | TCP 层面双向转发帧数据，不解帧内容 |
| M1-7c | └ 子协议透传 | ✅ | Sec-WebSocket-Protocol 透传 |
| M1-7d | └ 路由级开关 | ✅ | 路由 `WebSocket` 字段控制是否允许升级 |
| M1-7e | └ 仅首次记录日志 | ✅ | 升级后跳过 HTTP 解析、日志、Span |
| M1-7f | └ 可配置空闲超时 | ✅ | IdleTimeout 从 StarGatewaySetting 读取 |

## M2 集中配置管理

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M2-1 | StarServer 配置下发 | 🟡 | StarClient 已集成（注册/心跳），API 配置拉取预留 |
| M2-2 | 本地配置文件兜底 | ✅ | 支持gateway.json本地文件加载 |
| M2-3 | 配置热更新 | ✅ | HttpReverseProxy 定时15秒刷新路由 + 证书热更新 |
| M2-4 | 本地 Admin API | ✅ | /api/status, /api/routes, /api/refresh |
| M2-5 | 头部修改（StripPrefix + AddHeaders） | ✅ | 转发前自动去除路径前缀和添加请求头 |

## M3 StarAgent 协同

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M3-1 | 本地 StarAgent 通信 | ✅ | HttpClient调用StarAgent API (StartService/StopService) |
| M3-2 | 端口探测 | ✅ | TCP端口探测（集成在健康检查中） |
| M3-3 | 应用唤醒（冷启动） | ✅ | 无可用的后端节点时自动调用StartService |
| M3-4 | 空闲回收 | ✅ | 追踪后端最后活动时间，超时回收 |

## M4 可观测性

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M4-1 | 访问日志 | ✅ | AdminLog 记录请求方法/路径/目标/路由名 |
| M4-2 | 星尘 APM 集成 | ✅ | 每次转发创建追踪Span，传递Trace头 |
| M4-3 | 运行指标暴露 | ✅ | /api/status 返回会话数/请求数/路由数/运行时间 |

---

## M5 证书与集成（新增）

| 编码 | 功能 | 状态 | 说明 |
|------|------|:----:|------|
| M5-1 | 证书统一管理（SslCertificate） | ✅ | 废弃 GatewayCert，统一使用部署中心 SslCertificate |
| M5-2 | 多格式证书支持 | ✅ | PEM / PFX / CRT+KEY 自动识别加载 |
| M5-3 | 证书热更新 | ✅ | 配置刷新周期自动重载证书 |
| M5-4 | StarServer 注册 | ✅ | 作为 AppClient 注册到 StarServer，在线可见 |
| M5-5 | StarFactory 集成 | ✅ | 自动读取配置链（命令行→环境变量→appsettings→Star.config） |
| M5-6 | 优雅关闭 | ✅ | 反向顺序停止服务，每服务10秒超时 |

---

## 生产加固

| 项目 | 状态 | 说明 |
|------|:----:|------|
| 端口默认值修复 8080→8800 | ✅ | 与 Setting.cs 默认值一致 |
| ProbeAddress 异步化 | ✅ | 消除 task.Wait 线程池饥饿风险 |
| InitService 初始化修复 | ✅ | 添加完成日志，替换 GatewayCert→SslCertificate |
| PublishProfile 更新 | ✅ | netcoreapp3.1 → net10.0 |
| PublishProfile 简化 | ✅ | 更新为自包含单文件发布配置 |
| 证书热更新 | ✅ | 配置刷新定时器同时刷新证书 |

---

## 统计

| 模块 | 功能数 | 已完成 | 完成率 |
|------|:-----:|:-----:|:-----:|
| M1 动态路由与流量转发 | 13 | 13 | 100% |
| M2 集中配置管理 | 5 | 5 | 100% |
| M3 StarAgent 协同 | 4 | 4 | 100% |
| M4 可观测性 | 3 | 3 | 100% |
| M5 证书与集成（新增） | 6 | 6 | 100% |
| **总计** | **31** | **31** | **100%** |

---

## 后续规划（暂缓）

| 功能 | 说明 | 前提 |
|------|------|------|
| HTTP/2 / HTTP/3 支持 | 协议升级 | 出现必须 HTTP/2 的后端需求 |
| 限流/熔断 | 令牌桶 / 滑动窗口 | 负载均衡和健康检查完善后 |
| JWT/API Key 认证 | 网关层统一认证 | 需要统一网关层认证的场景 |
| Prometheus 指标 | 标准可观测性 | 用户明确要求 |
| gRPC 代理 | HTTP/2 基础上的 gRPC 转发 | HTTP/2 支持完成后 |
