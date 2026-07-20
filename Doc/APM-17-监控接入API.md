# APM-17 监控接入 API

> 版本：v1.0 | 日期：2026-07-15
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

Token 传递方式（按优先级）：URL 参数 `?Token=xxx` > `Authorization: Bearer xxx` > `X-Token: xxx`

---

## 接口列表

### 1. 登录认证 - POST /App/Login

```json
// Request
{
    "AppId": "MyApp",
    "Secret": "MySecret",
    "ClientId": "192.168.1.100@12345"
}

// Response
{
    "Token": "eyJhbG...",
    "Expire": 7200
}
```

### 2. 心跳保活 - POST /App/Ping

```json
// Request
{
    "Token": "eyJhbG...",
    "ClientId": "192.168.1.100@12345",
    "Version": "3.7.2026.0701",
    "IP": "192.168.1.100",
    "ProcessId": 12345
}
```

### 3. 上报监控数据 - POST /Trace/Report

```json
// Request Body
{
    "AppId": "MyApp",
    "ClientId": "192.168.1.100@12345",
    "Version": "3.7",
    "Period": 60,
    "Builders": [
        {
            "Name": "http://example.com/api/users",
            "StartTime": "2026-07-15T10:00:00",
            "Total": 100,
            "Errors": 2,
            "Cost": 5000,
            "Samples": [
                {
                    "Id": "span-001",
                    "StartTime": "2026-07-15T10:00:01",
                    "EndTime": "2026-07-15T10:00:02",
                    "Tag": "GET /api/users",
                    "Error": ""
                }
            ]
        }
    ]
}

// Response
{
    "Period": 60,
    "MaxSamples": 1,
    "MaxErrors": 10,
    "Timeout": 5000,
    "Excludes": ["/health", "/metrics"]
}
```

### 4. 上报压缩数据 - POST /Trace/ReportRaw

与 Report 接口相同，但请求体为 GZip 压缩的 JSON 数据，`Content-Type: application/x-gzip`。

---

## 多语言 SDK 支持

| SDK | 位置 | 说明 |
|-----|------|------|
| Go | `SDK/Go/` | Go APM 监控 SDK |
| Python | `SDK/Python/` | Python 3.7+ APM 监控 SDK |
| JavaScript | `SDK/JavaScript/` | Node.js 和浏览器环境 |
| PHP | `SDK/PHP/` | PHP 7.4+ APM 监控和配置中心 |
| Java | `SDK/Java/` | Java APM 监控 SDK |
