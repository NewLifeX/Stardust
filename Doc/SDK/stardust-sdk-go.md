# 星尘监控 Go SDK

> 正式实现与最新 API：[SDK/Go/README.md](../../SDK/Go/README.md) | 接入契约：[APM-17 监控接入 API](../APM-17-监控接入API.md)
>
> 本文为简要说明。早期文档中的完整源码已由 `SDK/Go/stardust` 正式实现取代。

适用于 **Go 1.18+**，零第三方依赖。

## 功能特性

- ✅ APM 链路追踪、同名 Span 聚合、样本与错误采样
- ✅ 登录认证、令牌自动续期、401 重登录
- ✅ 心跳保活并上报应用性能（内存、堆、Goroutine、GC 等）
- ✅ 服务端动态采样参数（`Period`、`MaxSamples`、`MaxErrors`、`Excludes` 等）
- ✅ 大报文 GZip（`/Trace/ReportRaw`）
- ✅ 配置中心客户端
- ✅ 自动埋点：默认 HTTP Transport、`WrapHandler`、`WrapClient`、`database/sql` 驱动包装、Redis 通用包装
- ✅ W3C `traceparent` 跨服务透传

## 安装

```bash
go get github.com/NewLifeX/Stardust/SDK/Go/stardust
```

## 快速开始

```go
package main

import (
    "github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
    // 一行接入：创建、登录并启动自动埋点
    tracer := stardust.Init("http://star.example.com:6600", "MyGoApp", "MySecret")
    defer tracer.Stop()

    // 需要自定义名称/版本时改用 builder
    // tracer := stardust.NewTracer(server, appID, secret).SetAppName("订单服务").SetVersion("1.2.3")
    // tracer.Start()

    span := tracer.NewSpan("order.create", "")
    span.Tag = "orderId=123"
    defer span.Finish()

    // 业务代码
}
```

## 配置项

| 方法 | 说明 | 默认值 |
|------|------|--------|
| `SetAppName(name)` | 应用名 | 同 AppId |
| `SetVersion(version)` | 应用版本；服务端据此下发 `Excludes`/`EnableMeter` | `1.0` |
| `SetIP(ip)` | 本机 IP；为空时自动枚举网卡 | 自动获取 |
| `SetEnableMeter(enable)` | 上报是否携带 `Info` 性能信息 | `true` |
| `SetPeriod(seconds)` | 初始上报周期 | `60` |
| `SetHTTPClient(client)` | 自定义 HTTP 客户端 | 10 秒超时 |

`Period`、`MaxSamples`、`MaxErrors`、`Timeout`、`MaxTagLen`、`Excludes`、`EnableMeter` 由服务端响应动态更新。

## 自动埋点

`NewTracer` 默认把出站 HTTP 自动埋点安装到 `http.DefaultTransport`，`Start()` 只负责登录与上报；
`SetAutoInstrument(false)` 可卸载。Web 入口与数据库驱动需一行接入：

```go
// Web 入口：解析 traceparent、创建入口 Span，并在请求上下文传递
http.ListenAndServe(":8080", stardust.WrapHandler(mux))

// database/sql：包装驱动，自动记录 db:{system}:{action}:{tables}
db, err := stardust.OpenDB(&sqlite3.SQLiteDriver{}, "file:app.db", tracer)

// Redis 通用包装
err = tracer.TraceRedis(ctx, "GET", func(ctx context.Context) error {
    return client.Get(ctx, "key").Err()
})
```

go-redis v9 可直接使用可选模块 `SDK/Go/stardust-redis` 的 `Hook`。Span 名称与标签遵循
[APM-17 自动埋点语义约定](../APM-17-监控接入API.md)。

## 心跳性能

心跳 `POST /App/Ping?Token=...` 请求体为 `AppInfo`：

| 字段 | 说明 |
|------|------|
| `Id` / `Name` / `Version` / `IP` | 进程号、应用名、版本、本机 IP |
| `CommandLine` / `UserName` / `MachineName` | 启动命令、用户、机器名 |
| `StartTime` | 进程启动时间（RFC3339） |
| `WorkingSet` / `HeapSize` | `MemStats.Sys` / `HeapAlloc` |
| `Threads` | Goroutine 数量（Go 无统一 OS 线程数） |
| `GCCount` | 距上次采集的 GC 次数增量 |
| `Time` | Unix 毫秒 |
| `ProcessorTime` / `CpuUsage` / `Handles` / `Connections` | 暂为 0（Go 标准库无跨平台采集） |

## 令牌与采样

- 登录响应返回 `Token` 与 `Expire`（秒）。
- 无令牌或 5 分钟内过期时自动重新登录。
- 心跳响应返回新 `Token` 时自动替换。
- HTTP 401 或 `code=401` 时重新登录并重试一次。
- 上报间隔由服务端 `Period` 动态决定，`Stop()` 可立即退出。

## 配置中心

```go
config := stardust.NewConfigClient("http://star.example.com:6600", "MyGoApp", "MySecret")
config.Start()
defer config.Stop()

dbHost := config.Get("database.host")
config.OnChange(func(configs map[string]string) {
    // 配置变更
})
```

## 运行测试

```bash
cd SDK/Go/stardust
go test ./...
go vet ./...
```

覆盖登录、心跳性能、上报、GZip、401 重登录、令牌续期、动态周期、启停，以及 `traceparent`、HTTP 出入站与 `database/sql` 驱动包装。

## 相关资源

- [SDK/Go/README.md](../../SDK/Go/README.md)
- [APM-17 监控接入 API](../APM-17-监控接入API.md)
- [SDK 能力矩阵](../../SDK/README.md)