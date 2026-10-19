# APM-17 监控接入 API

> 版本：v1.1 | 日期：2026-10-17
> 对应需求：APM-17 非 .NET 平台接入 API

---

## 概述

本文档面向非 .NET 平台的开发者，描述如何通过 HTTP API 接入星尘监控平台，上报 APM 数据。

**核心概念：**

| 概念 | 说明 |
|------|------|
| **Trace（追踪）** | 一次完整的请求链路，由唯一的 `TraceId` 标识 |
| **Span（片段）** | 追踪链路中的一个操作片段，包含开始/结束时间、标签、错误信息 |
| **Builder（构建器）** | 对同名操作的聚合统计，包含总次数、错误数、耗时等，并附带采样的 Span |
| **采样** | 每个周期内，正常请求只保留少量样本（MaxSamples），异常请求保留更多样本（MaxErrors） |

---

## 认证流程

```
客户端                           星尘服务端
  │  1. POST /App/Login          │
  │  (AppId + Secret)            │
  ├─────────────────────────────►│
  │  返回 JWT Token + 过期时间    │
  │◄─────────────────────────────┤
  │  2. POST /App/Ping (Token)   │
  ├─────────────────────────────►│
  │  返回新 Token（如即将过期）    │
  │◄─────────────────────────────┤
  │  3. POST /Trace/Report       │
  │  (监控数据)                   │
  ├─────────────────────────────►│
  │  返回采样参数                  │
  │◄─────────────────────────────┤
```

Token 传递方式（按优先级）：URL 参数 `?Token=xxx` > `Authorization: Bearer xxx` > `X-Token: xxx`。

---

## 统一响应格式

服务端控制器由 `ApiFilterAttribute` 统一包装，HTTP 状态码固定为 200，业务结果在 JSON 中表达：

```json
// 成功
{ "code": 0, "data": { ... } }

// 失败
{ "code": 401, "message": "无效应用令牌" }
```

SDK 必须检查 `code`，不能只看 HTTP 状态码。遇到 `code=401` 时应重新登录并重试一次；遇到 `code=400` 时应修正报文（常见原因是时间字段类型错误）。

---

## 接口列表

### 1. 登录认证 - POST /App/Login

请求字段：

| 字段 | 类型 | 必填 | 说明 |
|------|------|:----:|------|
| `AppId` | String | ✅ | 应用标识 |
| `Secret` | String | ✅ | 应用密钥 |
| `ClientId` | String | 建议 | 实例标识，推荐 `ip@进程号`；浏览器可用持久化的 `browser@<随机串>` |
| `AppName` | String | | 应用显示名 |
| `Version` | String | 建议 | 应用版本，用于版本跟踪 |
| `IP` | String | 建议 | 客户端内网 IP；浏览器留空由服务端取请求 IP |

```json
// Request
{
    "AppId": "MyApp",
    "Secret": "MySecret",
    "ClientId": "192.168.1.100@12345",
    "AppName": "MyApp",
    "Version": "1.2.3",
    "IP": "192.168.1.100"
}

// Response data
{
    "Token": "eyJhbG...",
    "Expire": 7200,
    "Code": "MyApp",
    "Secret": "MySecret",
    "Name": "MyApp",
    "ServerTime": 1784092800000
}
```

- `Expire` 单位为秒；`Code`/`Secret` 仅在服务端自动注册或下发新凭据时返回，SDK 应覆盖本地配置。

### 2. 心跳保活 - POST /App/Ping

请求体为 `AppInfo`，同时承担**应用性能采集**职责。服务端在心跳中写入应用在线状态与应用性能数据（`AppMeter`）。

```json
// Request（Token 放在 URL 查询参数）
{
    "Id": 12345,
    "Name": "MyApp",
    "Version": "1.2.3",
    "CommandLine": "node server.js",
    "UserName": "app",
    "MachineName": "web-01",
    "IP": "192.168.1.100",
    "StartTime": "2026-10-17T10:00:00.000Z",
    "ProcessorTime": 12000,
    "CpuUsage": 0.12,
    "WorkingSet": 104857600,
    "HeapSize": 52428800,
    "Threads": 16,
    "WorkerThreads": 12,
    "IOThreads": 4,
    "AvailableThreads": 20,
    "PendingItems": 0,
    "CompletedItems": 1000,
    "Handles": 42,
    "Connections": 8,
    "GCCount": 2,
    "Listens": "0.0.0.0:8080",
    "Time": 1784092800000
}

// Response data
{
    "Time": 1784092800000,
    "ServerTime": 1784092800100,
    "Period": 60,
    "Token": "eyJhbG..."
}
```

字段约定：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | Int32 | 进程号；浏览器填 0 |
| `Name` | String | 进程/应用名 |
| `Version` | String | 应用版本 |
| `CommandLine` | String | 启动命令行；浏览器填 User-Agent |
| `UserName` | String | 运行用户 |
| `MachineName` | String | 机器名；浏览器填平台 |
| `IP` | String | 本机 IP；浏览器留空 |
| `StartTime` | String | 启动时间，ISO 8601 字符串 |
| `ProcessorTime` | Int64 | 累计处理器时间，毫秒 |
| `CpuUsage` | Double | CPU 使用率，0~1 |
| `WorkingSet` | Int64 | 工作集/常驻内存，字节 |
| `HeapSize` | Int64 | 托管堆/JS 堆大小，字节 |
| `Threads` | Int32 | 线程数（Go 可用协程数近似） |
| `WorkerThreads` / `IOThreads` / `AvailableThreads` | Int32 | 线程池指标，无对应概念时填 0 |
| `PendingItems` / `CompletedItems` | Int64 | 线程池任务指标，无对应概念时填 0 |
| `Handles` | Int32 | 句柄/文件描述符数 |
| `Connections` | Int32 | 连接数 |
| `GCCount` | Int32 | 采样周期内 GC 次数 |
| `Listens` | String | 监听地址；浏览器可填 `location.origin` |
| `Time` | Int64 | **Unix 毫秒**时间戳 |

浏览器环境映射建议：

| `AppInfo` 字段 | 浏览器取值 |
|----------------|-----------|
| `WorkingSet` | `performance.memory?.usedJSHeapSize` |
| `HeapSize` | `performance.memory?.totalJSHeapSize` |
| `Threads` | `navigator.hardwareConcurrency` |
| `CommandLine` | `navigator.userAgent` |
| `MachineName` | `navigator.userAgentData?.platform ?? navigator.platform` |
| `Listens` | `location.origin` |
| `StartTime` | `new Date(performance.timeOrigin).toISOString()` |

> 浏览器不支持的能力（CPU、GC、句柄）填 0 即可；服务端按数值展示，不做强制校验。

### 3. 上报监控数据 - POST /Trace/Report

```json
// Request Body
{
    "AppId": "MyApp",
    "AppName": "MyApp",
    "ClientId": "192.168.1.100@12345",
    "Version": "1.2.3",
    "Info": {
        "Id": 12345,
        "Name": "MyApp",
        "Version": "1.2.3",
        "Time": 1784092800000,
        "WorkingSet": 104857600,
        "HeapSize": 52428800,
        "Threads": 16,
        "GCCount": 2
    },
    "Builders": [
        {
            "Name": "GET /api/users",
            "StartTime": 1784092800000,
            "EndTime": 1784092860000,
            "Total": 100,
            "Errors": 2,
            "Cost": 5000,
            "MaxCost": 200,
            "MinCost": 1,
            "Samples": [
                {
                    "Id": "span-001",
                    "ParentId": "",
                    "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
                    "StartTime": 1784092801000,
                    "EndTime": 1784092802000,
                    "Tag": "GET /api/users",
                    "Error": ""
                }
            ],
            "ErrorSamples": []
        }
    ]
}
```

- `Version` 必传：服务端仅在报文包含 `Version` 时返回 `Excludes`、`EnableMeter` 等动态参数。
- `Info` 可选，受应用设置 `EnableMeter` 控制；携带时服务端会写入应用性能数据。
- `Period` 不是请求字段，由服务端在响应中下发。

```json
// Response data
{
    "Period": 60,
    "MaxSamples": 1,
    "MaxErrors": 10,
    "Timeout": 5000,
    "MaxTagLength": 1024,
    "RequestTagLength": 1024,
    "EnableMeter": true,
    "Excludes": ["/health", "/metrics"]
}
```

| 字段 | 说明 |
|------|------|
| `Period` | 上报周期（秒），SDK 应据此调整定时器 |
| `MaxSamples` | 周期内正常样本上限 |
| `MaxErrors` | 周期内异常样本上限 |
| `Timeout` | 超时阈值（毫秒），服务端会把超时样本计入错误 |
| `MaxTagLength` | Tag 最大长度，超出截断 |
| `RequestTagLength` | 请求/响应标签最大长度，小于 0 时不使用 |
| `EnableMeter` | 是否采集应用性能信息 |
| `Excludes` | 需要排除的埋点名列表，支持通配匹配 |

### 4. 上报压缩数据 - POST /Trace/ReportRaw

与 Report 接口相同，但请求体为 GZip 压缩的 JSON 数据，`Content-Type: application/x-gzip`。适合埋点数据超过 1KB 的场景。

---

## 令牌生命周期

1. 登录成功后使用 `Token` 调用 Ping/Report，`Expire` 为有效秒数。
2. 心跳时若令牌剩余有效期不足 10 分钟，服务端会在响应中返回新 `Token`，SDK 应替换旧令牌。
3. SDK 应在到期前（建议 5 分钟）主动重新登录，避免请求失败。
4. 任意接口返回 `code=401` 时，SDK 应重新登录并重试一次；仍失败则丢弃本批数据并等待下个周期。
5. 令牌只具备该应用的写入权限，不应写入日志或暴露给无关页面。浏览器场景推荐由后端签发短期令牌（`tokenProvider`）。

---

## 字段约定

| 字段 | 类型 | 说明 |
|------|------|------|
| `StartTime` / `EndTime` | 数值 | **Unix 毫秒**时间戳（如 `1784092800000`），不是日期字符串。SDK/Go(`time.Now().UnixMilli()`)、SDK/JavaScript(`Date.now()`)、SDK/Java(`System.currentTimeMillis()`)、SDK/PHP/ASP 均按此格式上报 |
| `Total` / `Errors` | 数值 | 采样周期内该埋点名的总次数与错误次数 |
| `Cost` / `MaxCost` / `MinCost` | 数值 | 总耗时、最大耗时、最小耗时，单位毫秒 |
| `Samples` / `ErrorSamples` | 数组 | 正常采样与异常采样的 Span 列表，字段同上 |
| `Info.StartTime` | 字符串 | 与埋点时间不同，这是 ISO 8601 日期字符串 |
| `Info.Time` | 数值 | Unix 毫秒 |

> ⚠️ 时间字段必须为 Unix 毫秒数值。写成日期字符串等非法取值时，服务端整份报文解析失败并返回错误（`code:400` 与提示信息），不会落库。接入调试时请检查该字段格式。

---

## 多语言 SDK 支持

| SDK | 位置 | 登录 | 心跳+性能 | 埋点 | 配置中心 | 说明 |
|-----|------|:----:|:--------:|:----:|:--------:|------|
| JavaScript（浏览器） | `SDK/JavaScript/` | ✅ | ✅ | ✅ | - | UMD 单文件，支持 `secret` 与 `tokenProvider` |
| JavaScript/Node.js | `SDK/JavaScript/` | ✅ | ✅ | ✅ | - | Express/Koa 中间件，零外部依赖 |
| Java | `SDK/Java/` | ✅ | ✅ | ✅ | 🔧 | Java 8+，Maven，零运行时依赖 |
| Go | `SDK/Go/` | ✅ | ✅ | ✅ | ✅ | APM 与配置中心 |
| Python | `SDK/Python/` | ✅ | 🟡 | ✅ | ✅ | 心跳性能待补齐 |
| PHP | `SDK/PHP/` | ✅ | 🟡 | ✅ | ✅ | 心跳性能待补齐 |
| ASP | `SDK/ASP/` | ✅ | 🟡 | ✅ | - | Classic ASP |

> 完整能力矩阵、安装方式与路线图见 [SDK/README.md](../SDK/README.md)。
