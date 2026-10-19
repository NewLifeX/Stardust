# Stardust Go SDK

星尘监控（Stardust）Go SDK，提供 APM 监控和配置中心的接入能力。

## 特性

- ✅ APM 链路追踪
- ✅ 配置中心
- ✅ 心跳性能指标上报
- ✅ 令牌自动续期与 401 重登录
- ✅ 服务端动态采样周期
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
    tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret").
        SetAppName("MyGoApp").
        SetVersion("1.0.0")
    tracer.Start()
    defer tracer.Stop()

    // 手动埋点
    span := tracer.NewSpan("业务操作", "")
    span.Tag = "参数信息"
    doSomething()
    span.Finish()
}
```

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
