# Stardust Go SDK

星尘（Stardust）Go 语言客户端 SDK，提供 APM 监控和配置中心功能。

## 目录结构

```
SDK/Go/
├── stardust/           # SDK 核心包（零第三方依赖）
│   ├── go.mod         # Go 模块定义
│   ├── README.md      # SDK 使用文档
│   ├── tracer.go      # APM 追踪器实现
│   ├── tracer_appinfo.go # 心跳与性能指标采集
│   ├── config.go      # 配置中心客户端实现
│   ├── tracecontext.go # W3C traceparent 解析与生成
│   ├── context.go     # 上下文片段与子片段辅助
│   ├── http.go        # HTTP 入站/出站自动埋点
│   ├── db.go          # database/sql 驱动自动埋点
│   ├── redis.go       # Redis 通用埋点包装
│   ├── stardust_test.go  # 单元测试
│   ├── tracecontext_test.go # 链路上下文测试
│   ├── http_instrument_test.go # HTTP 自动埋点测试
│   ├── db_test.go     # 数据库自动埋点测试（假驱动）
│   └── integration_test.go # 使用 httptest 的集成测试
├── stardust-redis/    # 可选模块：go-redis v9 埋点 Hook
│   ├── go.mod
│   ├── hook.go
│   └── hook_test.go
└── examples/          # 示例程序
    ├── README.md      # 示例说明
    ├── apm_basic.go   # APM 基础示例
    ├── config_basic.go # 配置中心基础示例
    └── combined.go    # 综合示例
```

## 快速开始

### 安装

```bash
go get github.com/NewLifeX/Stardust/SDK/Go/stardust
```

### APM 监控

```go
import "github.com/NewLifeX/Stardust/SDK/Go/stardust"

// 一行接入：创建、登录并启动自动埋点
tracer := stardust.Init("http://localhost:6600", "MyGoApp", "MySecret")
defer tracer.Stop()

// 需要自定义版本号等参数时改用 builder
// tracer := stardust.NewTracer(server, appID, secret).SetVersion("1.0.0")
// tracer.Start()

span := tracer.NewSpan("业务操作", "")
span.Tag = "参数信息"
defer span.Finish()
// 你的业务代码
```

### 自动埋点（一行接入）

`NewTracer` 自动安装默认 HTTP 传输埋点，`Start()` 只负责登录、心跳与上报：

```go
tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret")
tracer.Start()
defer tracer.Stop()

// HTTP 服务端：入口 Span + 上游 traceparent 解析
http.ListenAndServe(":8080", stardust.WrapHandler(mux))

// HTTP 客户端：默认传输已埋点，普通 http.Client 即可；也可显式包装
client := tracer.WrapClient(&http.Client{Timeout: 5 * time.Second})

// 数据库：driver 为真实驱动实例
db, err := stardust.OpenDB(&sqlite3.SQLiteDriver{}, "file:app.db", tracer)

// Redis：通用包装
err = tracer.TraceRedis(ctx, "GET", func(ctx context.Context) error {
    return rdb.Get(ctx, "user:1").Err()
})
```

关闭默认传输自动埋点：

```go
tracer.SetAutoInstrument(false)
```

go-redis v9 可选用独立模块（核心包仍零第三方依赖）：

```go
import stardustredis "github.com/NewLifeX/Stardust/SDK/Go/stardust-redis"

rdb.AddHook(stardustredis.NewHook(tracer))
```

> 自动埋点遵循 W3C traceparent：SDK 生成 32 位 TraceId、16 位 SpanId；入站兼容 .NET 短 TraceId；SDK 自身登录/心跳/上报请求不埋点。
> 仅覆盖标准库 `net/http` 与 `database/sql`；Gin/Echo/gRPC 需使用框架中间件 + `NewSpanFromContext`。完整说明见 [stardust/README.md](stardust/README.md#自动埋点)。

### APM 配置项

| 设置方法 | 说明 | 默认值 |
|----------|------|--------|
| `SetAppName(name)` | 应用名称 | 与 AppId 相同 |
| `SetVersion(version)` | 应用版本号。**服务端只有收到 Version 才下发 Excludes/EnableMeter** | `1.0` |
| `SetIP(ip)` | 本机 IP。为空时自动枚举网卡（跳过回环地址，必要时 UDP 探测） | 自动获取 |
| `SetEnableMeter(enable)` | 是否在监控上报中携带进程性能信息（心跳始终携带） | `true` |
| `SetPeriod(seconds)` | 初始采样周期（秒），服务端响应会覆盖 | `60` |
| `SetHTTPClient(client)` | 自定义 HTTP 客户端（超时、代理等） | 10 秒超时 |

采样参数 `Period`、`MaxSamples`、`MaxErrors`、`Timeout`、`MaxTagLen`、`Excludes`、`EnableMeter` 均由服务端上报响应动态下发并即时生效，无需重启。

### 心跳与令牌

- 登录请求携带 `Version` 与 `IP`，响应返回 `Token` 与 `Expire`（秒）。
- 令牌缺失或距离过期不足 5 分钟时自动重新登录。
- 心跳（`/App/Ping`）上报进程性能指标：`WorkingSet`、`HeapSize`、`Threads`、`GCCount`、`Time`、`StartTime` 等；响应携带新 `Token` 时自动更新。
- 服务端返回 HTTP 401 或 `code=401` 时，SDK 自动重新登录并重试一次。
- 采样周期由服务端下发的 `Period` 动态调整上报间隔。

### 配置中心

```go
import "github.com/NewLifeX/Stardust/SDK/Go/stardust"

config := stardust.NewConfigClient("http://localhost:6600", "MyGoApp", "MySecret")
config.Start()
defer config.Stop()

value := config.Get("database.host")
config.OnChange(func(configs map[string]string) {
    // 配置变更处理
})
```

## 特性

- ✅ 完整的 APM 链路追踪支持
- ✅ HTTP 入站/出站自动埋点（W3C traceparent，跨语言互通）
- ✅ database/sql 驱动自动埋点（一行包装）
- ✅ Redis 通用埋点包装 + 可选 go-redis v9 Hook
- ✅ 配置中心客户端实现
- ✅ 自动登录和 Token 刷新
- ✅ 令牌到期前自动续期、401 自动重登录并重试
- ✅ 心跳保活并上报进程性能指标
- ✅ 版本号协商（Version）与服务端动态采样周期
- ✅ Gzip 压缩大数据包
- ✅ 自动采样控制
- ✅ 自动枚举网卡获取本机 IP
- ✅ 配置变更推送
- ✅ 核心包无第三方依赖
- ✅ 支持 Go 1.18+
- ✅ 完整的单元测试与 httptest 集成测试

## 运行测试

```bash
cd stardust
go test -count=1 ./...
go vet ./...

# 可选 go-redis Hook 模块
cd ../stardust-redis
go test -count=1 ./...
```

## 示例程序

查看 `examples/` 目录获取完整示例。

## 文档

- [SDK 详细文档](stardust/README.md)
- [示例程序说明](examples/README.md)
- [完整 API 文档](../../Doc/SDK/stardust-sdk-go.md)

## License

MIT License
