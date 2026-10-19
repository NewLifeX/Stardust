# Stardust Redis Hook（可选模块）

`go-redis v9` 的星尘埋点钩子。核心包 `stardust` 保持零第三方依赖，需要 go-redis 埋点时再引入本模块。

## 安装

```bash
go get github.com/NewLifeX/Stardust/SDK/Go/stardust-redis
```

## 使用

```go
import (
    "github.com/NewLifeX/Stardust/SDK/Go/stardust"
    stardustredis "github.com/NewLifeX/Stardust/SDK/Go/stardust-redis"
    "github.com/redis/go-redis/v9"
)

tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret")
tracer.Start()
defer tracer.Stop()

rdb := redis.NewClient(&redis.Options{Addr: "127.0.0.1:6379"})
rdb.AddHook(stardustredis.NewHook(tracer))
```

- 普通命令生成 `redis:{COMMAND}` 片段，Tag 为前 4 个参数摘要。
- 管道生成 `redis:PIPELINE` 片段，Tag 为前 4 个命令名称。
- 上下文已有片段时作为其子片段，否则新建链路。
- `NewHook(nil)` 使用 `stardust.DefaultTracer()`。

## 测试

测试使用 fake hook，不需要运行 Redis 服务器：

```bash
go test -count=1 ./...
```
