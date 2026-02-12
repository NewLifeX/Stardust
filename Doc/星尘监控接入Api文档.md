# 星尘监控平台接入 API 文档

本文档面向非 .NET 平台的开发者，描述如何通过 HTTP API 接入星尘（Stardust）监控平台，上报应用性能监控（APM）数据。

## 目录

- [概述](#概述)
- [认证流程](#认证流程)
- [接口列表](#接口列表)
  - [1. 登录认证 - POST /App/Login](#1-登录认证---post-applogin)
  - [2. 心跳保活 - POST /App/Ping](#2-心跳保活---post-appping)
  - [3. 上报监控数据 - POST /Trace/Report](#3-上报监控数据---post-tracereport)
  - [4. 上报压缩监控数据 - POST /Trace/ReportRaw](#4-上报压缩监控数据---post-tracereportraw)
- [数据模型](#数据模型)
- [接入流程](#接入流程)
- [注意事项](#注意事项)

---

## 概述

星尘监控平台通过 APM（Application Performance Monitoring）机制，收集应用的链路追踪数据。客户端 SDK 按照固定周期（默认60秒）将本周期内采集到的调用信息进行汇总，然后上报至星尘服务端。

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
┌─────────┐                          ┌──────────────┐
│  客户端  │                          │  星尘服务端   │
└────┬────┘                          └──────┬───────┘
     │  1. POST /App/Login                  │
     │  (AppId + Secret)                    │
     ├─────────────────────────────────────►│
     │                                      │
     │  返回 JWT Token + 过期时间            │
     │◄─────────────────────────────────────┤
     │                                      │
     │  2. POST /App/Ping (携带 Token)       │
     │  (定期心跳，刷新令牌)                 │
     ├─────────────────────────────────────►│
     │                                      │
     │  返回新 Token（如即将过期）            │
     │◄─────────────────────────────────────┤
     │                                      │
     │  3. POST /Trace/Report (携带 Token)   │
     │  (上报监控数据)                       │
     ├─────────────────────────────────────►│
     │                                      │
     │  返回采样参数（周期、最大样本数等）     │
     │◄─────────────────────────────────────┤
```

**Token 传递方式（按优先级）：**

1. URL 查询参数：`?Token=xxx`
2. HTTP 请求头：`Authorization: Bearer xxx`
3. HTTP 请求头：`X-Token: xxx`
4. Cookie：`Token=xxx`

---

## 接口列表

### 1. 登录认证 - POST /App/Login

客户端启动时，使用应用标识和密钥进行登录，获取 JWT 令牌。

**请求：**

```
POST /App/Login
Content-Type: application/json
```

**请求体：**

```json
{
    "AppId": "MyApp",
    "Secret": "MySecret",
    "ClientId": "192.168.1.100@12345",
    "AppName": "我的应用",
    "NodeCode": "",
    "Project": ""
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `AppId` | String | ✅ | 应用标识，在星尘平台注册的应用名 |
| `Secret` | String | ✅ | 应用密钥 |
| `ClientId` | String | 推荐 | 实例标识，建议格式 `IP@进程ID`，用于区分多实例部署 |
| `AppName` | String | 否 | 应用显示名称 |
| `NodeCode` | String | 否 | 节点编码 |
| `Project` | String | 否 | 项目名 |

**响应体：**

```json
{
    "code": 0,
    "data": {
        "Code": "MyApp",
        "Secret": "MySecret",
        "Name": "我的应用",
        "Token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXdCJ9.xxx.xxx",
        "Expire": 7200,
        "ServerTime": 1700000000000
    }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `code` | Int | 状态码，0 表示成功 |
| `data.Code` | String | 应用标识（可能被服务端修正） |
| `data.Secret` | String | 应用密钥（动态注册时可能更新） |
| `data.Name` | String | 应用名称 |
| `data.Token` | String | JWT 访问令牌，后续请求使用 |
| `data.Expire` | Int | 令牌有效期（秒） |
| `data.ServerTime` | Long | 服务器 UTC 时间（毫秒时间戳） |

---

### 2. 心跳保活 - POST /App/Ping

定期发送心跳，保持在线状态。当令牌即将过期（10分钟内），服务端会自动颁发新令牌。

**请求：**

```
POST /App/Ping?Token=xxx
Content-Type: application/json
```

**请求体（AppInfo，可选）：**

```json
{
    "Id": 12345,
    "Name": "MyApp",
    "Version": "1.0.0",
    "IP": "192.168.1.100",
    "MachineName": "server-01",
    "UserName": "appuser",
    "StartTime": "2024-01-01T08:00:00",
    "ProcessorTime": 50000,
    "CpuUsage": 0.15,
    "WorkingSet": 104857600,
    "Threads": 25,
    "Handles": 300,
    "Connections": 50,
    "Time": 1700000000000
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | Int | 进程 ID |
| `Name` | String | 进程名称 |
| `Version` | String | 应用版本 |
| `IP` | String | 本地 IP 地址 |
| `MachineName` | String | 机器名 |
| `UserName` | String | 运行用户名 |
| `StartTime` | DateTime | 进程启动时间 |
| `ProcessorTime` | Long | 处理器时间（毫秒） |
| `CpuUsage` | Double | CPU 使用率（0~1） |
| `WorkingSet` | Long | 物理内存占用（字节） |
| `HeapSize` | Long | 堆大小（字节） |
| `Threads` | Int | 线程数 |
| `WorkerThreads` | Int | 工作线程数 |
| `IOThreads` | Int | IO 线程数 |
| `Handles` | Int | 句柄数 |
| `Connections` | Int | 连接数 |
| `GCCount` | Int | GC 次数 |
| `Time` | Long | 本地 UTC 时间（毫秒时间戳） |

**响应体：**

```json
{
    "code": 0,
    "data": {
        "Time": 1700000000000,
        "ServerTime": 1700000000000,
        "Period": 60,
        "Token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXdCJ9.newtoken.xxx",
        "Commands": []
    }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `data.Time` | Long | 服务器时间 |
| `data.ServerTime` | Long | 服务器 UTC 时间（毫秒时间戳） |
| `data.Period` | Int | 心跳间隔（秒） |
| `data.Token` | String | 新令牌（仅当旧令牌即将过期时返回） |
| `data.Commands` | Array | 待执行的命令列表 |

> **重要**：如果响应中包含 `Token` 字段且非空，客户端必须用新 Token 替换旧 Token。

---

### 3. 上报监控数据 - POST /Trace/Report

每个采样周期结束后，将汇总的追踪数据上报至服务端。

**请求：**

```
POST /Trace/Report?Token=xxx
Content-Type: application/json
```

**请求体（TraceModel）：**

```json
{
    "AppId": "MyApp",
    "AppName": "我的应用",
    "ClientId": "192.168.1.100@12345",
    "Version": "1.0.0",
    "Info": {
        "Id": 12345,
        "Name": "MyApp",
        "CpuUsage": 0.15,
        "WorkingSet": 104857600,
        "Threads": 25,
        "Time": 1700000000000
    },
    "Builders": [
        {
            "Name": "/api/user/list",
            "StartTime": 1700000000000,
            "EndTime": 1700000060000,
            "Total": 150,
            "Errors": 2,
            "Cost": 4500,
            "MaxCost": 230,
            "MinCost": 5,
            "Samples": [
                {
                    "Id": "a1b2c3d4e5f6",
                    "ParentId": "",
                    "TraceId": "trace-001-abc",
                    "StartTime": 1700000010000,
                    "EndTime": 1700000010050,
                    "Tag": "GET /api/user/list?page=1&size=20",
                    "Error": ""
                }
            ],
            "ErrorSamples": [
                {
                    "Id": "x1y2z3w4v5u6",
                    "ParentId": "",
                    "TraceId": "trace-002-def",
                    "StartTime": 1700000020000,
                    "EndTime": 1700000020100,
                    "Tag": "GET /api/user/list?page=999",
                    "Error": "System.InvalidOperationException: Page out of range"
                }
            ]
        },
        {
            "Name": "SELECT * FROM User",
            "StartTime": 1700000000000,
            "EndTime": 1700000060000,
            "Total": 300,
            "Errors": 0,
            "Cost": 1200,
            "MaxCost": 50,
            "MinCost": 2,
            "Samples": [],
            "ErrorSamples": []
        }
    ]
}
```

#### TraceModel 字段说明

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `AppId` | String | ✅ | 应用标识 |
| `AppName` | String | 否 | 应用显示名称 |
| `ClientId` | String | 否 | 实例标识（IP@进程ID） |
| `Version` | String | 否 | 客户端版本号 |
| `Info` | AppInfo | 否 | 应用性能信息（参见 Ping 请求体） |
| `Builders` | Builder[] | ✅ | 追踪数据数组 |

#### Builder（构建器）字段说明

每个 Builder 代表同一个操作名在本采样周期内的聚合统计数据。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Name` | String | ✅ | 操作名，如 `/api/user/list`、`SELECT * FROM User` |
| `StartTime` | Long | ✅ | 周期开始时间（Unix 毫秒时间戳） |
| `EndTime` | Long | ✅ | 周期结束时间（Unix 毫秒时间戳） |
| `Total` | Int | ✅ | 本周期内该操作的总调用次数 |
| `Errors` | Int | ✅ | 本周期内该操作的错误次数 |
| `Cost` | Long | ✅ | 本周期内所有调用的总耗时（毫秒） |
| `MaxCost` | Int | ✅ | 本周期内单次调用的最大耗时（毫秒） |
| `MinCost` | Int | ✅ | 本周期内单次调用的最小耗时（毫秒） |
| `Samples` | Span[] | 否 | 正常请求的采样详情（最多 MaxSamples 条） |
| `ErrorSamples` | Span[] | 否 | 异常请求的采样详情（最多 MaxErrors 条） |

#### Span（片段）字段说明

Span 是单次调用的详细信息，用于链路追踪和问题排查。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Id` | String | ✅ | 片段唯一标识，随上下文传递，用于建立父子关系 |
| `ParentId` | String | 否 | 父级片段标识，为空表示根片段 |
| `TraceId` | String | ✅ | 追踪标识，同一条调用链路共享相同的 TraceId |
| `StartTime` | Long | ✅ | 开始时间（Unix 毫秒时间戳） |
| `EndTime` | Long | ✅ | 结束时间（Unix 毫秒时间戳） |
| `Tag` | String | 否 | 数据标签，附加信息（如 HTTP URL、SQL 语句等） |
| `Error` | String | 否 | 错误信息（异常消息和堆栈） |

**响应体（TraceResponse）：**

```json
{
    "code": 0,
    "data": {
        "Period": 60,
        "MaxSamples": 1,
        "MaxErrors": 10,
        "Timeout": 5000,
        "MaxTagLength": 1024,
        "RequestTagLength": 1024,
        "EnableMeter": true,
        "Excludes": ["/health", "/favicon.ico"]
    }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `data.Period` | Int | 采样周期（秒），客户端应按此周期上报数据 |
| `data.MaxSamples` | Int | 每周期每操作最大正常采样数 |
| `data.MaxErrors` | Int | 每周期每操作最大异常采样数 |
| `data.Timeout` | Int | 超时阈值（毫秒），超过此时间的调用视为异常 |
| `data.MaxTagLength` | Int | Tag 字段最大长度（字符），超出截断 |
| `data.RequestTagLength` | Int | 请求/响应标签最大长度（字符） |
| `data.EnableMeter` | Boolean | 是否收集应用性能信息（AppInfo） |
| `data.Excludes` | String[] | 需要排除的操作名列表 |

> 客户端应根据响应动态调整本地采样参数。

---

### 4. 上报压缩监控数据 - POST /Trace/ReportRaw

当上报数据（JSON）超过 1024 字节时，建议使用 Gzip 压缩后通过此接口上报。

**请求：**

```
POST /Trace/ReportRaw?Token=xxx
Content-Type: application/x-gzip
Content-Length: <压缩后字节数>
Body: <Gzip 压缩后的 TraceModel JSON 字节流>
```

**处理流程：**
1. 将 TraceModel 序列化为 JSON 字符串
2. 将 JSON 字符串编码为 UTF-8 字节数组
3. 对字节数组进行 Gzip 压缩
4. 以 `application/x-gzip` 内容类型发送

**响应体与 `/Trace/Report` 相同。**

> 如果不使用 Gzip 压缩，也可以直接发送 JSON 原文，此时 `Content-Type` 不应设为 `application/x-gzip`。

---

## 数据模型

### 时间戳说明

所有时间字段均使用 **Unix 毫秒时间戳**（自 1970-01-01 00:00:00 UTC 起的毫秒数）。

各语言获取方式：

```python
# Python
import time
timestamp_ms = int(time.time() * 1000)
```

```java
// Java
long timestampMs = System.currentTimeMillis();
```

```go
// Go
timestampMs := time.Now().UnixMilli()
```

```javascript
// JavaScript
const timestampMs = Date.now();
```

```php
// PHP
$timestampMs = intval(microtime(true) * 1000);
```

### TraceId 与 SpanId 生成

- **TraceId**：建议使用 UUID 或随机字符串，同一调用链路内所有 Span 共享
- **SpanId（Id）**：每个 Span 的唯一标识，建议使用 UUID 或随机字符串
- **ParentId**：指向父级 Span 的 Id，根 Span 的 ParentId 为空字符串

```
TraceId: "abc123"
├── Span(Id="s1", ParentId="")          ← 根 Span: HTTP 请求
│   ├── Span(Id="s2", ParentId="s1")    ← 子 Span: 数据库查询
│   └── Span(Id="s3", ParentId="s1")    ← 子 Span: Redis 缓存
│       └── Span(Id="s4", ParentId="s3") ← 孙 Span: 序列化
```

### ClientId 格式

`ClientId` 用于标识应用实例，推荐格式为 `IP@进程ID`，例如：`192.168.1.100@12345`。

---

## 接入流程

### 客户端生命周期

```
1. 启动 → 调用 /App/Login 获取 Token
2. 开始定时心跳（30~60秒）→ 调用 /App/Ping
3. 开始定时采集（默认60秒周期）：
   a. 收集本周期内所有操作的调用数据
   b. 按操作名聚合为 Builder
   c. 对每个 Builder 保留 MaxSamples 个正常样本和 MaxErrors 个异常样本
   d. 调用 /Trace/Report 或 /Trace/ReportRaw 上报
   e. 根据响应更新本地采样参数
4. 退出 → 上报最后一批数据
```

### 采样策略

```
每个采样周期（Period 秒）内，对于每个操作名（Builder.Name）：

1. 记录所有调用的统计信息：总次数、错误数、总耗时、最大/最小耗时
2. 正常调用只保留 MaxSamples 条完整 Span（默认1条）
3. 异常调用保留 MaxErrors 条完整 Span（默认10条）
4. 超过 Timeout 毫秒的调用也视为异常
5. Tag 超过 MaxTagLength 字符时截断
```

### 错误处理

- **上报失败**：将数据加入本地队列，下次成功时重试
- **Token 过期**：重新调用 `/App/Login` 获取新 Token
- **服务端返回 code != 0**：根据错误码处理
  - `401`：认证失败，重新登录
  - `403`：应用被禁用或无权限
  - `500`：服务端内部错误，稍后重试

---

## 注意事项

1. **排除自身调用**：上报接口 `/Trace/Report` 和 `/Trace/ReportRaw` 的调用本身不应被采集，避免递归
2. **数据时效**：服务端会拒收过期数据（默认保留天数之前）和未来时间的数据
3. **操作名长度**：Builder 的 Name 字段有长度限制（约200字符），超长会被拒收
4. **Gzip 压缩**：JSON 超过 1024 字节时建议使用 `/Trace/ReportRaw` 接口压缩上传
5. **Excludes 过滤**：服务端返回的 Excludes 列表中的操作名，客户端不应再采集
6. **心跳续期**：令牌在过期前10分钟内，Ping 接口会自动返回新令牌
7. **网络异常**：网络不可用时应在本地缓存数据，恢复后重发
