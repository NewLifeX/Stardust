# Stardust Go SDK

星尘（Stardust）Go 语言客户端 SDK，提供 APM 监控和配置中心功能。

## 目录结构

```
SDK/Go/
├── stardust/           # SDK 核心包
│   ├── go.mod         # Go 模块定义
│   ├── README.md      # SDK 使用文档
│   ├── tracer.go      # APM 追踪器实现
│   ├── tracer_appinfo.go # 心跳与性能指标采集
│   ├── config.go      # 配置中心客户端实现
│   ├── stardust_test.go  # 单元测试
│   └── integration_test.go # 使用 httptest 的集成测试
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

tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret").
    SetAppName("MyGoApp").
    SetVersion("1.0.0") // 建议显式设置版本号，服务端据此下发采样参数
tracer.Start()
defer tracer.Stop()

span := tracer.NewSpan("业务操作", "")
span.Tag = "参数信息"
defer span.Finish()
// 你的业务代码
```

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
- ✅ 配置中心客户端实现
- ✅ 自动登录和 Token 刷新
- ✅ 令牌到期前自动续期、401 自动重登录并重试
- ✅ 心跳保活并上报进程性能指标
- ✅ 版本号协商（Version）与服务端动态采样周期
- ✅ Gzip 压缩大数据包
- ✅ 自动采样控制
- ✅ 自动枚举网卡获取本机 IP
- ✅ 配置变更推送
- ✅ 无第三方依赖
- ✅ 支持 Go 1.18+
- ✅ 完整的单元测试与 httptest 集成测试

## 运行测试

```bash
cd stardust
go test ./...
go test -race ./...   # 需要启用 CGO
```

## 示例程序

查看 `examples/` 目录获取完整示例。

## 文档

- [SDK 详细文档](stardust/README.md)
- [示例程序说明](examples/README.md)
- [完整 API 文档](../../Doc/SDK/stardust-sdk-go.md)

## License

MIT License
