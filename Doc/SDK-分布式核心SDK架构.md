# Stardust SDK 架构

> 版本：v1.0 | 日期：2026-07-15
> 对应模块：SDK 分布式核心 SDK
> 相关文档：[NODE-16-阿里云DNS动态域名解析](NODE-16-阿里云DNS动态域名解析.md)

---

## 1. 概述

Stardust SDK 包含两个 NuGet 包：

| 包名 | 说明 | 目标框架 |
|------|------|----------|
| `NewLife.Stardust` | 核心库（无 ASP.NET Core 依赖） | net45+ / netstandard2.0+ |
| `NewLife.Stardust.Extensions` | ASP.NET Core 扩展 | netcoreapp3.1+ ~ net10.0 |

SDK 提供：配置拉取、服务注册与发现、调用追踪（APM）、性能监控、日志上报、事件总线等能力。

---

## 2. 核心组件

### 2.1 客户端类

```
┌──────────────────────────────────────────────────┐
│                    StarFactory                    │
│  统一入口：StarSetting + StarClient + StarTracer │
└──────────┬───────────────────────────────────────┘
           │
    ┌──────┴──────┐
    ▼             ▼
┌──────────┐ ┌──────────┐
│StarClient│ │ AppClient│
│ 连接/心跳 │ │ 应用客户端│
│ 令牌刷新  │ │ 配置/注册 │
└──────────┘ └──────────┘
    │             │
    ▼             ▼
┌──────────────────────────────────────────────────┐
│              功能模块                              │
│ StarHttpConfigProvider  (配置拉取)                │
│ IRegistry               (服务注册发现)            │
│ StarTracer              (调用追踪)                │
│ TraceService            (追踪上报)                │
│ StarEventBus            (事件总线)                │
│ AliyunDnsClient         (DDNS)                   │
└──────────────────────────────────────────────────┘
```

### 2.2 组件详情

| 组件 | 命名空间 | 说明 |
|------|----------|------|
| `StarFactory` | `Stardust` | 统一工厂，读取配置、创建 Client 和 Tracer |
| `StarClient` | `Stardust` | 核心客户端，管理连接/心跳/令牌刷新 |
| `AppClient` | `Stardust` | 业务应用客户端，封装配置拉取/注册发现 |
| `StarSetting` | `Stardust` | 配置管理（Server/AppKey/Secret） |
| `StarHttpConfigProvider` | `Stardust.Configs` | HTTP 配置提供者 |
| `IRegistry` | `Stardust.Registry` | 服务注册与发现接口 |
| `StarTracer` | `Stardust.Monitors` | APM 追踪器 |
| `StarEventBus` | `Stardust.Services` | 事件总线 |

---

## 3. 接入方式

### 3.1 ASP.NET Core 应用

```csharp
// 1. 注册服务
var star = builder.Services.AddStardust("MyApp");

// 2. 使用中间件
app.UseStardust();

// 3. 注册服务
app.RegisterService("MyService");

// 4. 消费服务
var services = app.ConsumeService("OtherService");
```

### 3.2 控制台/Service 应用

```csharp
var star = new StarFactory {
    Server = "http://star.newlifex.com:6600",
    AppKey = "MyApp",
    Secret = "xxx"
};

// 创建客户端
var client = star.Create();
await client.Login();
```

---

## 4. 配置中心客户端

```
应用启动 → StarHttpConfigProvider.GetConfig()
         → 请求 StarServer /config/getall
         → 缓存配置到本地
         → 定时轮询变更
```

- 支持版本号增量更新
- 配置变更回调事件
- 本地缓存兜底

---

## 5. 追踪 (APM) 客户端

### 5.1 自动埋点

`StarTracer` 通过 .NET 的 `DiagnosticListener` 自动采集以下框架的调用：

| 监听器 | 目标框架 |
|--------|----------|
| `HttpDiagnosticListener` | `HttpClient` / `HttpWebRequest` |
| `SqlClientDiagnosticListener` | ADO.NET SQL Server |
| `RedisDiagnosticListener` | NewLife.Redis / StackExchange.Redis |
| `AspNetCoreDiagnosticListener` | ASP.NET Core 请求 |
| `EfCoreDiagnosticListener` | Entity Framework Core |
| `GrpcDiagnosticListener` | gRPC |
| `MongoDbDiagnosticListener` | MongoDB Driver |
| `SocketEventListener` | Socket 网络通信 |

### 5.2 数据流

```
应用调用 → DiagnosticListener 捕获 → StarTracer 本地统计
         → 每 60 秒批量上报 → TraceController.Report()
         → 服务端聚合计算
```

---

## 6. ASP.NET Core 扩展

`Stardust.Extensions` 提供的中间件：

| 方法 | 说明 |
|------|------|
| `AddStardust()` | 注入 StarClient、StarTracer，注册配置服务 |
| `UseStardust()` | 启用追踪中间件，自动创建 Span |
| `RegisterService()` | 注册当前应用为服务提供者 |
| `ConsumeService()` | 消费指定服务，返回可用地址列表 |
