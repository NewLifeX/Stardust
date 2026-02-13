# Stardust Go SDK

星尘（Stardust）Go 语言客户端 SDK，提供 APM 监控和配置中心功能。

## 目录结构

```
SDK/Go/
├── stardust/           # SDK 核心包
│   ├── go.mod         # Go 模块定义
│   ├── README.md      # SDK 使用文档
│   ├── tracer.go      # APM 追踪器实现
│   ├── config.go      # 配置中心客户端实现
│   └── stardust_test.go  # 单元测试
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

tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret")
tracer.Start()
defer tracer.Stop()

span := tracer.NewSpan("业务操作", "")
span.Tag = "参数信息"
defer span.Finish()
// 你的业务代码
```

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
- ✅ 心跳保活
- ✅ Gzip 压缩大数据包
- ✅ 自动采样控制
- ✅ 配置变更推送
- ✅ 无第三方依赖
- ✅ 支持 Go 1.18+
- ✅ 完整的单元测试

## 运行测试

```bash
cd stardust
go test -v
```

## 示例程序

查看 `examples/` 目录获取完整示例。

## 文档

- [SDK 详细文档](stardust/README.md)
- [示例程序说明](examples/README.md)
- [完整 API 文档](/Doc/SDK/stardust-sdk-go.md)

## License

MIT License
