# Stardust Go SDK

星尘监控（Stardust）Go SDK，提供 APM 监控和配置中心的接入能力。

## 特性

- ✅ APM 链路追踪
- ✅ 配置中心
- ✅ 心跳性能指标上报
- ✅ 令牌自动续期与 401 重登录
- ✅ 服务端动态采样周期
- ✅ HTTP 入站/出站自动埋点（W3C traceparent 跨语言互通）
- ✅ database/sql 驱动自动埋点
- ✅ Redis 通用包装与可选 go-redis v9 Hook
- ✅ 无第三方依赖，仅使用 Go 标准库
- ✅ 支持 Go 1.18+

## 安装

```bash
go get github.com/NewLifeX/Stardust/SDK/Go/stardust
```

## APM 监控快速开始

```go
package main

import (
    "github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
    // 一行接入：创建、登录并启动自动埋点
    tracer := stardust.Init("http://star.example.com:6600", "MyGoApp", "MySecret")
    defer tracer.Stop()

    // 需要自定义时改用 builder：
    // tracer := stardust.NewTracer(server, appID, secret).SetVersion("1.0.0")
    // tracer.Start()

    // 手动埋点
    span := tracer.NewSpan("业务操作", "")
    span.Tag = "参数信息"
    doSomething()
    span.Finish()
}
```

## 自动埋点

`NewTracer` 创建后会自动完成两件事：设为默认追踪器，并把出站埋点安装到 `http.DefaultTransport`。
`Start()` 只负责登录、心跳与上报，不参与埋点安装。后创建的追踪器会成为新的默认追踪器；
多实例场景请显式使用实例方法，或最后创建主实例。

```go
tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret")
tracer.SetAutoInstrument(false) // 如不需要默认传输自动埋点，可关闭
tracer.Start()
defer tracer.Stop()
```

### 链路上下文（W3C traceparent）

自动埋点遵循 W3C Trace Context：

```text
traceparent: 00-<trace-id>-<span-id>-<flags>
```

- SDK 生成 32 位小写十六进制 TraceId 与 16 位 SpanId。
- 入站解析 `traceparent`，复用 TraceId 并把上游 SpanId 作为入口片段的 ParentId；缺失或非法时新建链路。
- 为兼容 .NET 历史实现，入站允许短于 32 位的 trace-id。
- 出站以当前片段注入 `traceparent`；调用方已显式设置时不覆盖。
- SDK 自身的登录、心跳、上报与配置中心请求不创建片段，也不注入 `traceparent`。

手动读取/传播上下文：

```go
ctx, span := tracer.NewSpanFromContext(ctx, "业务操作")
defer span.Finish()

if current := stardust.SpanFromContext(ctx); current != nil {
    fmt.Println(current.TraceID, current.ID)
}
```

### HTTP 出站

默认传输已自动埋点，普通 `http.Client` 即可；也可显式包装：

```go
client := tracer.WrapClient(&http.Client{Timeout: 5 * time.Second})
resp, err := client.Do(req.WithContext(ctx))
```

- 片段名称：`{METHOD} {host}{path}`，Tag 为完整 URL。
- 客户端 4xx/5xx 或网络异常记为错误片段。

### HTTP 入站（一行接入）

```go
handler := stardust.WrapHandler(mux)
http.ListenAndServe(":8080", handler)

// 或处理函数
http.HandleFunc("/api", stardust.WrapHandlerFunc(api))
```

- 片段名称：`{METHOD} {path}`，Tag 为原始 RequestURI。
- 服务端 5xx 或 panic 记为错误片段，panic 会记录后原样抛出。
- 嵌套包装会通过请求标记去重，同一请求只产生一个入口片段。
- Gin/Echo 等框架请使用其自身中间件 + `NewSpanFromContext`，或把框架的 `http.Handler` 交给 `WrapHandler`。

### 数据库（一行接入）

```go
// 方式一：直接打开（driver.Driver 为真实驱动实例）
db, err := stardust.OpenDB(&sqlite3.SQLiteDriver{}, "file:app.db", tracer)

// 方式二：注册包装后的驱动
sql.Register("mydb", stardust.WrapDriver(&mysql.MySQLDriver{}, tracer))

// 方式三：包装连接器
db := sql.OpenDB(stardust.WrapConnector(connector, tracer))
```

- 片段名称：`db:{system}:{action}:{tables}`，如 `db:sqlite:select:users`；Tag 为 SQL 文本（上报时按 `MaxTagLength` 截断）。
- 表名识别 `from`、`insert into`、`update`、`join`、`drop table`、`truncate table`，并去掉 `_20240101` 等日期分表后缀。
- 覆盖 `driver.Driver/Conn/Stmt/Tx`，以及 `ExecerContext`、`QueryerContext`、`ConnBeginTx`、`ConnPrepareContext`、`Pinger`、`SessionResetter`、`Validator` 等可选接口。
- 底层返回 `driver.ErrSkip` 时按 `database/sql` 约定回退，不产生多余片段。
- 上下文已有片段时作为其子片段，否则新建链路。

### Redis

核心模块提供通用包装，适合任意 Redis 客户端：

```go
err := tracer.TraceRedis(ctx, "GET", func(ctx context.Context) error {
    return rdb.Get(ctx, "user:1").Err()
})

// 带参数摘要
err = tracer.TraceRedisWithTag(ctx, "SET", "user:1", func(ctx context.Context) error {
    return rdb.Set(ctx, "user:1", "alice", 0).Err()
})
```

go-redis v9 可使用独立模块 `SDK/Go/stardust-redis`（核心包仍保持零第三方依赖）：

```go
import stardustredis "github.com/NewLifeX/Stardust/SDK/Go/stardust-redis"

rdb := redis.NewClient(&redis.Options{Addr: "127.0.0.1:6379"})
rdb.AddHook(stardustredis.NewHook(tracer))
```

- 片段名称：`redis:{COMMAND}`，如 `redis:GET`；Tag 为 key/参数摘要。
- 管道记为 `redis:PIPELINE`，Tag 为前几个命令名称。

### 自动埋点限制

- 只覆盖 Go 标准库 `net/http` 与 `database/sql`；gRPC、消息队列等需手动埋点。
- 标准库没有路由模板信息，入站片段名称使用实际路径；需要模板名称时请结合框架中间件自行创建片段。
- 数据库表名基于简单 SQL 关键字解析，复杂 SQL、动态表名或存储过程可能无法识别。
- 数据库类型从驱动类型名推断（如 sqlite/mysql/postgres/sqlserver/oracle），自定义驱动可能显示为类型名前缀。
- 核心模块不内置 Redis 客户端依赖；go-redis 需额外引入 `stardust-redis` 模块。
- 自动埋点自身不改变业务返回值；上游 `traceparent` 非法时自动降级为新链路。

## APM 配置项

所有设置方法均返回 `*Tracer`，支持链式调用：

| 方法 | 说明 | 默认值 |
|------|------|--------|
| `SetAppName(name)` | 应用名称 | 与 AppId 相同 |
| `SetVersion(version)` | 应用版本号。建议显式设置（SDK 默认 `1.0`）；服务端只有收到 Version 才下发 `Excludes`/`EnableMeter` | `1.0` |
| `SetIP(ip)` | 本机 IP。为空时自动枚举网卡（跳过回环地址，必要时 UDP 探测） | 自动获取 |
| `SetEnableMeter(enable)` | 是否在监控上报中携带进程性能信息（心跳始终携带） | `true` |
| `SetPeriod(seconds)` | 初始采样周期（秒），服务端响应会覆盖 | `60` |
| `SetHTTPClient(client)` | 自定义 HTTP 客户端 | 10 秒超时 |
| `SetAutoInstrument(enable)` | 开/关默认 HTTP 传输自动埋点 | `true`（`NewTracer` 自动安装） |

> 登录请求会携带 `Version` 与 `IP`；上报响应中的 `Period`、`MaxSamples`、`MaxErrors`、`Timeout`、`MaxTagLength`、`Excludes`、`EnableMeter` 会即时应用到后续采样。

## 心跳性能指标

启用后，心跳请求（`POST /App/Ping`）的请求体为 `AppInfo`，包含：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | int | 进程 ID |
| `Name` / `Version` / `IP` | string | 应用名、版本、本机 IP |
| `CommandLine` | string | 启动命令行 |
| `UserName` / `MachineName` | string | 当前用户与机器名 |
| `StartTime` | string | 进程启动时间（RFC3339） |
| `ProcessorTime` / `CpuUsage` | int64 / float64 | 处理器时间与 CPU 占比（Go SDK 暂为 0） |
| `WorkingSet` / `HeapSize` | int64 | 进程内存（`MemStats.Sys`）与堆分配（`HeapAlloc`） |
| `Threads` | int | Goroutine 数量 |
| `GCCount` | int | 距上次采集的 GC 次数增量 |
| `Time` | int64 | 当前 Unix 毫秒时间戳 |

其余线程池、句柄、连接数字段保留协议兼容，Go SDK 暂填 0。

## 令牌与采样周期

- 登录响应返回 `Token` 与 `Expire`（秒）；令牌缺失或距离过期不足 5 分钟时自动重新登录。
- 心跳响应返回新 `Token` 时自动更新本地令牌。
- 服务端返回 HTTP 401 或 `code=401` 时，自动重新登录并重试一次。
- 上报间隔由服务端下发的 `Period` 动态决定，修改后下一次等待即生效；`Stop()` 可立即退出等待。

## 配置中心快速开始

```go
package main

import (
    "fmt"
    "github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
    config := stardust.NewConfigClient("http://star.example.com:6600", "MyGoApp", "MySecret")
    config.Start()
    defer config.Stop()

    // 获取配置
    value := config.Get("database.host")
    fmt.Println("Database Host:", value)

    // 监听配置变更
    config.OnChange(func(configs map[string]string) {
        fmt.Println("配置已更新:", configs)
    })

    select {}
}
```

## 完整文档

详细文档请参考：[/Doc/SDK/stardust-sdk-go.md](../../../Doc/SDK/stardust-sdk-go.md)

## 框架集成

### Gin 框架

```go
import (
    "github.com/gin-gonic/gin"
    "github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

var tracer = stardust.NewTracer("http://star.example.com:6600", "MyGinApp", "secret")

func StardustMiddleware() gin.HandlerFunc {
    return func(c *gin.Context) {
        name := c.Request.Method + " " + c.Request.URL.Path
        span := tracer.NewSpan(name, "")
        span.Tag = c.Request.Method + " " + c.Request.RequestURI
        defer span.Finish()
        c.Next()
    }
}
```

## License

MIT License
