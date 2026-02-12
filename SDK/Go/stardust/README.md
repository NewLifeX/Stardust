# Stardust Go SDK

星尘监控（Stardust）Go SDK，提供 APM 监控和配置中心的接入能力。

## 特性

- ✅ APM 链路追踪
- ✅ 配置中心
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
    tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret")
    tracer.Start()
    defer tracer.Stop()

    // 手动埋点
    span := tracer.NewSpan("业务操作", "")
    span.Tag = "参数信息"
    doSomething()
    span.Finish()
}
```

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
