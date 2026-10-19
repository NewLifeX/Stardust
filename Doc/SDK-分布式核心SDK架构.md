# Stardust SDK 架构

> 版本：v1.1 | 日期：2026-10-17
> 对应模块：SDK 分布式核心 SDK
> 相关文档：[NODE-16-阿里云DNS动态域名解析](NODE-16-阿里云DNS动态域名解析.md)

---

## 1. 概述

Stardust SDK 包含两个 NuGet 包：

| 包名 | 说明 | 目标框架 |
|------|------|----------|
| `NewLife.Stardust` | 核心库（无 ASP.NET Core 依赖） | net45+ / netstandard2.0+ |
| `NewLife.Stardust.Extensions` | ASP.NET Core 扩展（传递引用核心库） | netcoreapp3.1+ ~ net10.0 |

> ASP.NET Core 应用只需引用 `NewLife.Stardust.Extensions`，然后 `builder.Services.AddStardust("AppId")` 一行接入。

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
// 一行接入：连接星尘、自动注入追踪中间件，并启用
// HttpClient / WebApi / 数据库 / Redis 自动埋点。
var star = builder.Services.AddStardust("MyApp");

// 可选：需要自定义中间件顺序时再显式调用；重复调用是幂等的。
// app.UseStardust();

// 注册服务
app.RegisterService("MyService");

// 消费服务
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
| `AddStardust()` | 一行接入：注入 StarClient、StarTracer、配置服务，并自动注册追踪中间件与启动服务 |
| `UseStardust()` | 显式启用追踪中间件，自动创建 Span；`AddStardust` 已自动调用，通常无需重复 |
| `RegisterService()` | 注册当前应用为服务提供者 |
| `ConsumeService()` | 消费指定服务，返回可用地址列表 |

---

## 7. 多语言 SDK

非 .NET 应用通过 HTTP 接入星尘，不依赖 .NET 运行时。统一契约见 [APM-17 监控接入 API](APM-17-监控接入API.md)，能力矩阵与发布计划见 [SDK/README.md](../SDK/README.md)。

### 7.1 分层

```
业务应用
   │
   ├─ 手动埋点：Span/Tag/Error
   ├─ 自动埋点：Web 入口（traceparent 解析）、出站 HTTP（traceparent 注入）、
   │            DB/Redis 驱动包装、浏览器 fetch/XHR、页面加载、路由、JS 错误
   │
   ▼
语言 SDK（StardustTracer）
   ├─ 本地聚合：同名 Span → Builder（Total/Errors/Cost/样本）
   ├─ 心跳：30s，上报 AppInfo（CPU/内存/堆/线程/GC/句柄）
   ├─ 上报：60s（可被服务端 Period 调整），>1KB GZip
   └─ 令牌：登录获取，到期前重登录，401 重试
   │
   ▼
StarServer 6600：/App/Login、/App/Ping、/Trace/Report
```

### 7.2 语言矩阵

| 语言 | 实现方式 | Web 入口自动 | 出站 HTTP 自动 | DB/Redis 自动 |
|------|----------|--------------|----------------|---------------|
| JavaScript（浏览器） | UMD 单文件，零依赖 | 页面加载、SPA 路由、JS/资源错误 | fetch/XHR（注入 `traceparent`） | - |
| JavaScript/Node.js | CommonJS，零外部依赖 | `http.createServer`、Express/Koa | `http`/`https`/`fetch` | `pg`/`mysql2`、`redis`/`ioredis` |
| Java 8+ | Maven，零运行时依赖 | ServletContainerInitializer + Filter | `StardustHttp` 一行包装 | `StardustJdbc` 一行包装、Redis 通用包装 |
| Go | Go Module，零外部依赖 | `WrapHandler` 一行接入 | 默认 Transport 自动、`WrapClient` | `WrapDriver` 一行接入、Redis 通用包装 |
| Python | 现有实现 | WSGI/ASGI、Flask/Django/Starlette | `requests`/`urllib` | DB-API、`redis` |
| PHP/ASP | 现有实现 | 文档示例 | 文档示例 | 文档示例 |

> 跨语言链路统一使用 W3C `traceparent`：Web 入口解析上游上下文，出站 HTTP 注入当前 Span 上下文，详见 [APM-17 监控接入 API](APM-17-监控接入API.md)。

### 7.3 鉴权模式

| 模式 | 适用场景 | 说明 |
|------|----------|------|
| `AppId + Secret` | 服务端/内网应用 | 简单直接；Secret 不得提交到公开仓库 |
| `tokenProvider` | 浏览器/公开前端 | 后端用 Secret 登录后签发短期 Token，前端只持有 Token |
| 匿名上报 | 多级级联/极简 SDK | 服务端需显式开启 `AnonymousReport`，默认关闭 |

### 7.4 发布与命名

| 生态 | 坐标策略 |
|------|----------|
| NuGet | `NewLife.Stardust`（已发布） |
| npm | `newlife` 已被占用，统一使用 `@newlifex/stardust` |
| Maven Central | `com.newlife:stardust`（优先验证 newlife.com）；无法验证时用 `io.github.newlifex:stardust` |
| Go | 模块路径即身份：`github.com/NewLifeX/Stardust/SDK/Go/stardust` |
| crates.io | `stardust` 已被占用，使用 `newlifex-stardust` |
| PyPI | `stardust`/`stardust-sdk` 已被占用，使用 `newlifex-stardust` |
| Packagist | `newlifex/stardust` |

> 命名原则：优先 `NewLife`，被占用时使用 `NewLifeX`；同一生态内保持前缀一致，避免同一 SDK 多套坐标。
