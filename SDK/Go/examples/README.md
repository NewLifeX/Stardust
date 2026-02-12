# Stardust Go SDK 示例程序

本目录包含了 Stardust Go SDK 的使用示例。

## 前置要求

1. 安装 Go 1.18 或更高版本
2. 运行 Stardust 服务器（默认端口 6600）

## 示例列表

### 1. APM 基础示例 (apm_basic.go)

演示如何使用 APM 追踪器进行链路追踪。

```bash
go run apm_basic.go
```

功能：
- 创建和启动追踪器
- 手动埋点
- 使用 defer 自动结束 span
- 自动上报数据到服务器

### 2. 配置中心基础示例 (config_basic.go)

演示如何使用配置中心客户端。

```bash
go run config_basic.go
```

功能：
- 连接配置中心
- 获取单个配置项
- 获取所有配置
- 监听配置变更

### 3. 综合示例 (combined.go)

演示如何同时使用 APM 和配置中心。

```bash
go run combined.go
```

功能：
- 同时启动 APM 和配置中心
- 配置变更时记录到 APM
- 根据配置执行业务逻辑
- 业务操作的链路追踪

## 配置说明

在运行示例前，需要在 Stardust 服务器中创建应用：

1. 应用标识：`MyGoApp`
2. 密钥：`MySecret`

或者修改示例代码中的连接参数：

```go
tracer := stardust.NewTracer("http://your-server:6600", "YourAppId", "YourSecret")
config := stardust.NewConfigClient("http://your-server:6600", "YourAppId", "YourSecret")
```

## 在自己的项目中使用

1. 安装依赖：

```bash
go get github.com/NewLifeX/Stardust/SDK/Go/stardust
```

2. 导入并使用：

```go
import "github.com/NewLifeX/Stardust/SDK/Go/stardust"

func main() {
    tracer := stardust.NewTracer("http://localhost:6600", "MyApp", "secret")
    tracer.Start()
    defer tracer.Stop()
    
    // 你的业务代码
}
```

## 更多文档

详细文档请参考：[/Doc/SDK/stardust-sdk-go.md](../../../Doc/SDK/stardust-sdk-go.md)
