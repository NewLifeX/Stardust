# NewLife.Stardust - 星尘分布式平台

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/stardust?logo=github)

![GitHub License](https://img.shields.io/github/license/NewLifeX/Stardust?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Stardust?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Stardust?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Stardust?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/Stardust?logo=github)](https://github.com/NewLifeX/Stardust/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ℹ️ 本中文版与 [English README](README.md) 同批维护；若两者内容不一致，以英文版为对外口径。

**星尘（Stardust）** 是轻量级分布式服务平台：节点管理、配置中心、注册中心与服务发现、全链路追踪与 APM、日志中心与远程发布，支持服务自动注册发现、负载均衡、动态伸缩与故障转移。应用服务犹如宇宙中的尘埃，而作为应用服务承载体的计算机节点是为星星，星星与尘埃构成整个浩瀚星空，即为星尘！

![星尘分布式](Doc/星尘分布式.png)

演示系统：[http://star.newlifex.com](http://star.newlifex.com)

**目录**：[快速开始](#快速开始) · [主要功能](#主要功能) · [生态版图](#生态版图) · [文档](#文档) · [参与贡献](#参与贡献) · [团队](#团队) · [许可证](#许可证)

## 快速开始

### 安装

- 源码：clone 本仓库，使用 Visual Studio / Rider / `dotnet build` 直接编译
- NuGet：`NewLife.Stardust`（核心）；`NewLife.Stardust.Extensions`（ASP.NET Core 扩展，传递引用核心包）

```bash
dotnet add package NewLife.Stardust.Extensions
```

### 启动平台

1) 启动 StarServer：

```
cd Stardust.Server
dotnet run
```

2) 启动 StarWeb：

```
cd ../Stardust.Web
dotnet run
```

首次运行自动建库建表（默认 SQLite / 可配置 MySql / SqlServer / PostgreSql / Oracle / 达梦 等；在 `*.config` / `appsettings.*` 或环境变量中设置连接字符串）。

### 部署 StarAgent 到业务节点

```
cd StarAgent
# 直接运行（调试）
dotnet run
# 或发布后安装为系统服务（示例）
dotnet publish -c Release -o publish
# Windows: StarAgent.exe -install
# Linux:   chmod +x StarAgent && ./StarAgent install
```

配置 StarServer 地址后，节点会在平台显示。

#### 其它平台

```
# macOS 自包含包（osx-x64 / osx-arm64）
cd Tools && build-agent-osx.bat

# Android 简化代理（需 Android workload）
dotnet build StarAgent.Android/StarAgent.Android.csproj -c Release
# 产物：Bin/Agent/mobile/android/com.newlife.staragent-Signed.apk
pwsh StarAgent.Android/pack-android.ps1 -Mode Aot
pwsh StarAgent.Android/run-android.ps1 -Smoke

# iOS 极简代理（需 macOS + Xcode）
bash StarAgent.iOS/run-ios.sh
```

> 移动端受系统限制：Android 需要前台服务与电池白名单；iOS 只能间歇在线（前台与 BGTask 唤醒时上报），不支持进程守护与远程发布。详见 [NODE-StarAgent架构](Doc/NODE-StarAgent架构.md)。

### 创建应用 (StarWeb)

- 在“应用管理”中新建应用，生成 AppId / Secret
- 可配置分类、项目(Project)、预期端口、心跳策略等

### 业务应用接入

引用包：

```
dotnet add package NewLife.Stardust.Extensions
```

在 Program / Startup：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 一行接入：连接星尘平台、注入追踪中间件，并启用
// HttpClient / WebApi / 数据库 / Redis 自动埋点。
var star = builder.Services.AddStardust("OrderService");

var app = builder.Build();
// app.UseStardust(); // 可选：需要自定义中间件顺序时再显式调用
app.RegisterService("OrderService"); // 注册到星尘
// app.ConsumeService("UserService"); // 可选：消费其它服务
app.Run();
```

运行后即可在 StarWeb 看到实例在线、性能指标与调用链。

### 多语言SDK支持

除了 .NET SDK，星尘还提供多语言SDK支持：

| SDK | 说明 | 文档 |
| --- | --- | --- |
| JavaScript（浏览器） | UMD 单文件，登录/心跳性能/页面与接口埋点 | [SDK/JavaScript/README.md](SDK/JavaScript/README.md) |
| JavaScript/Node.js | 登录/心跳性能/Express/Koa 埋点 | [SDK/JavaScript/README.md](SDK/JavaScript/README.md) |
| Java | Java 8+，Maven，零运行时依赖 | [SDK/Java/README.md](SDK/Java/README.md) |
| Go | Go 1.18+，APM 与配置中心 | [SDK/Go/README.md](SDK/Go/README.md) |
| Python | Python 3.7+ APM 与配置中心 | [Doc/SDK/stardust-sdk-python.md](Doc/SDK/stardust-sdk-python.md) |
| PHP | PHP 7.4+ APM 与配置中心 | [SDK/PHP/README.md](SDK/PHP/README.md) |
| Classic ASP | VBScript 登录/心跳/埋点 | [SDK/ASP/README.md](SDK/ASP/README.md) |

> 统一能力矩阵、命名与发布计划见 [SDK/README.md](SDK/README.md)。

**JavaScript SDK 快速示例：**

```javascript
// Node.js 环境：一行创建并启动
const { init } = require('./SDK/JavaScript');
const tracer = init({
  server: 'http://star.example.com:6600',
  appId: 'MyApp',
  secret: 'MySecret',
  version: '1.2.3',
});

// 浏览器环境：推荐 loader.js 一行接入（见 SDK/README.md），也可直接调用 init
const webTracer = Stardust.init({
  server: 'http://star.example.com:6600',
  appId: 'MyWebApp',
  tokenProvider: async () => (await fetch('/api/stardust-token')).json(),
  version: '1.2.3',
});
// 自动采集页面加载、路由、JS 错误、fetch/XHR
```

### 模板脚手架

命令行中运行以下命令快速体验NewLife组件：

```
dotnet new install NewLife.Templates
dotnet new nconsole --name test
cd test
dotnet run
```

## 主要功能

<details>
<summary><b>为什么选择星尘</b>（7 项）</summary>

| 优势 | 说明 |
| --- | --- |
| 一体化 | 节点管理 / 配置中心 / 注册中心 / 服务发现 / 全链路追踪 / APM / 日志中心 / 远程发布 / 资源监控 统一模型与界面 |
| 超轻量 | 核心库零第三方重量级依赖，单进程即可运行，自动建库建表 |
| 强兼容 | 同一生态覆盖 .NET Framework 4.5 起至最新 .NET 10，多平台（Windows/Linux/macOS/Android/iOS/ARM/IoT） |
| 多语言 Agent | Go/Rust/C++ 轻量原生 Agent 可直接对接 StarServer，覆盖无 .NET 运行时、嵌入式与 OpenWrt 场景 |
| 高性能 | 基于 NewLife.Core & NewLife.Net 等十余年优化积累，网络吞吐与序列化性能优异 |
| 极易接入 | 几行代码完成接入；ASP.NET Core / Console / Service / IoT 边缘统一方式 |
| 自带运维 | StarAgent 守护进程 + 远程发布 + 指标 & 日志采集，极大降低分布式运维复杂度 |

</details>

<details>
<summary><b>平台核心</b>（13 项）</summary>

| 功能 | 关键特性 |
| --- | --- |
| 星尘代理 | StarAgent部署在每台应用服务器，作为系统服务负责守护本地应用及监控节点性能，支持Windows/Linux/macOS，并提供Android/iOS简化版（注册心跳/指标/日志/命令子集） |
| 节点管理 | 中心化管理多机器上的海量StarAgent，支持机房内服务器节点、远端桌面工控机以及物联网边缘计算节点 |
| 应用管理 | 为每个应用系统分配接入密钥，用于配置中心、应用监控、服务注册、服务发现等 |
| 配置中心 | 各应用从配置中心读取自己的配置信息，有利于应用的多实例部署以及集中管理 |
| 监控中心 | 各应用（服务提供者和服务消费者）向中心上报性能指标等监控数据 |
| 注册中心 | 各微服务应用（服务提供者）向注册中心上报自身服务信息 |
| 服务发现 | 各业务应用（服务消费者）从注册中心拉取指定服务的提供者信息，进行分布式调用（不经过网关） |
| 远程发布 | 在星尘控制台上发布应用包到多台安装有星尘代理的应用服务器或物联网边缘节点，StarAgent负责拉起并守护进程 |
| 日志中心 | 各应用（服务提供者和服务消费者）向中心上报日志数据，统一在星尘Web查看 |
| [多供应商DNS DDNS](Doc/NODE-16-阿里云DNS动态域名解析.md) | StarAgent支持动态域名解析(DDNS)，自动将节点公网IP注册到DNS（支持阿里云/腾讯云(DNSPod)/优刻得(UDNS)/AWS Route53四家供应商），适用于动态IP环境 |
| .NET运行时管理 | StarServer自动同步官方.NET发布信息，StarAgent支持按需安装.NET运行时版本，配合升级命令实现自动升级与冒烟测试回滚 |
| 流水线 | 支持创建流水线及运行/步骤模型，通过 webhook 自动触发构建发布流程，配合仓库 SSH 密钥与用户名密码认证实现自动化部署 |
| 网关v2.0 | StarGateway 重构升级，支持动态路由引擎、负载均衡、健康检查、TLS 终止、WebSocket 代理与静态文件托管（SPA 回退/大文件保护），并集成 APM 追踪 |

</details>

<details>
<summary><b>架构与组件</b>（7 项）</summary>

```
+--------------------+        +-------------------+        +---------------------+
|    应用实例 App    | <----> |  Registry/Config  | <----> |    StarServer 核心   |
| (AddStardust 扩展) |        | (服务/配置/发现)  |        |  (API & 任务/统计)   |
+---------+----------+        +---------+---------+        +----------+----------+
          |                               ^                           |
          v                               |                           v
   StarAgent (节点守护/部署/监控采集)      |                   StarWeb 管理界面
          |                               |                           |
       远程发布 / 性能 & 日志 / 指标上报 ---+--- 数据存储 (XCode ORM, 多数据库)
```

| 组件 | 功能与典型部署 |
| --- | --- |
| StarServer | 服务平台后端（API / 配置 / 注册 / 发布 / 指标聚合 / 统计计算）。1~N，可横向扩展，持久化依赖数据库/Redis(可选) |
| StarWeb | 基于 NewLife.Cube 的管理与可视化界面。1，可与 StarServer 同机或分离 |
| StarAgent | 安装在每台节点主机：进程守护、远程发布、采集监控（CPU/内存/网络/自定义）。N（每机一实例），支持 Windows 服务、Linux systemd、容器或后台进程 |
| Stardust Core Library (SDK) | 客户端 SDK，提供配置拉取、注册发现、调用追踪、性能 & 日志上报。与业务应用同进程，通过 AddStardust/UseStardust 集成 |
| Stardust.Extensions | ASP.NET Core 扩展（AddStardust / UseStardust / RegisterService / ConsumeService）。用于业务 Web/API，暴露中间件与注册/发现便捷方法 |
| 数据层 (Stardust.Data) | 基于 XCode 的实体与统计任务 |
| StarGateway / DeployAgent（可选） | 流量转发 / 构建与分发支撑，视需求部署，可与服务发现协同 |

</details>

<details>
<summary><b>监控中心</b>（4 项）</summary>

| 能力 | 说明 |
| --- | --- |
| 应用监控 | 各应用上报的性能指标，在星尘控制台可视化展示 |
| 调用链 | 业务调用的端到端追踪，例如物联网平台远程控制花园水泵：[传送门](http://star.newlifex.com/trace?id=ac15452e1649035594650011be21ec) |
| 关系图 | 基于上报追踪构建的服务依赖关系图 |
| TraceAnonymous | 调用链页面支持 `TraceAnonymous` 属性，设置后允许未登录用户访问，适用于公开展示或演示场景 |

![应用监控](Doc/应用监控.png)
![调用链](Doc/调用链.png)
![关系图](Doc/关系图.png)

星尘监控介绍：[https://newlifex.com/blood/stardust_monitor](https://newlifex.com/blood/stardust_monitor)

</details>

<details>
<summary><b>配置中心</b>（4 项）</summary>

| 能力 | 关键特性 |
| --- | --- |
| 集中配置 | 在平台“配置中心”中为指定应用添加 Key=Value，客户端自动下发 |
| 隔离与继承 | 支持分环境 / 分项目配置，支持模板继承 |
| 推送更新 | 客户端自动长轮询/推送更新，无需重启应用 |
| 变更事件 | 修改后应用实例收到变更事件，可用于热更新业务参数 |

```csharp
var star = services.AddStardust("OrderService");
var cfg = star.GetConfig();
var dbConn = cfg["Db:ConnStr"]; // 自动从平台下发
```

</details>

<details>
<summary><b>服务注册与发现</b>（4 项）</summary>

| 能力 | 关键特性 |
| --- | --- |
| 注册 | `app.RegisterService("SvcName", address: null, environment, healthUrl)` 向注册中心上报服务实例 |
| 地址解析 | 未显式 address 时自动解析本机可用地址 |
| 健康探针 | healthUrl 提供健康探针 |
| 发现 | `app.ConsumeService("SvcName")` 自动维护提供者列表，支持轮询负载 / 故障剔除；也可通过 `ServiceProviderResolver` 自定义策略 |

```csharp
app.RegisterService("SvcName", address: null, environment, healthUrl);
app.ConsumeService("SvcName");
```

</details>

<details>
<summary><b>全链路追踪与指标</b>（5 项）</summary>

| 能力 | 关键特性 |
| --- | --- |
| 自动 Span | SDK 内置 ITracer，进入点会自动创建 span |
| 手动 Span | 业务层需要时显式创建 span |
| 性能计数 | QPS、耗时分布、错误率 |
| 系统指标 | Agent 采集 CPU/内存/线程/网络 |
| 日志 | 通过 NewLife.Log 上报；在 Web 中统一查询 |

```csharp
using var span = star.Tracer?.NewSpan("biz:calc", new { id });
```

</details>

<details>
<summary><b>远程发布 / 持续交付</b>（5 项）</summary>

| 步骤 | 说明 |
| --- | --- |
| 1. 创建发布应用 | 在“发布中心”填写：Repository / Branch / ProjectPath / PackageFilters / 编译参数 |
| 2. 绑定目标节点 | 绑定目标节点（StarAgent） |
| 3. 触发构建 | 平台拉取代码 → 构建 → 打包过滤 → 分发至节点 → Agent 解压部署 → 守护运行 |
| 4. 灰度发布 | 选择子集节点发布，验证后全量 |
| 5. 回滚 | 历史版本包保留，可一键回滚 |

</details>

<details>
<summary><b>安全与权限</b>（3 项）</summary>

| 能力 | 关键特性 |
| --- | --- |
| 权限体系 | 继承 NewLife.Cube 权限体系（用户 / 角色 / 菜单 / 数据权限） |
| 凭证管理 | AppId + Secret 仅在创建时可见，请妥善保存 |
| 资源隔离 | 支持按项目与分类的资源隔离 |

</details>

<details>
<summary><b>数据与存储</b>（3 项）</summary>

| 能力 | 关键特性 |
| --- | --- |
| ORM | XCode（自动建表/迁移/分表/读写分离） |
| 数据库 | SQLite / MySql / SqlServer / Oracle / PostgreSql / 达梦 等 |
| 数据保留 | 指标、追踪、日志分表存储，长期运行自动归档与清理（后台计划任务） |

</details>

<details>
<summary><b>性能特性</b>（4 项）</summary>

| 特性 | 说明 |
| --- | --- |
| 高吞吐网络栈 | 源自 NewLife.Net |
| 零侵入 APM | 最少埋点即可统计 |
| 多级缓存 | Redis 可选集成（Stardust.Server 使用 Redis 统计加速可选） |
| 低 GC | 内部使用对象池 / 字符串构建池 |

</details>

<details>
<summary><b>与常见方案对比</b>（5 项）</summary>

| 诉求 | 星尘 | 单一注册中心 (如 Consul/Eureka) | 仅配置中心 | 传统脚本发布 |
| ---- | ---- | ------------------------------- | --------- | ------------ |
| 功能覆盖 | 注册/发现/配置/发布/监控/追踪 | 注册 + 健康 | 配置 | 仅分发 |
| 接入复杂度 | 低 | 中 | 中 | 高 |
| 运维统一度 | 高 | 中 | 中 | 低 |
| 语言生态 | 以 .NET 为主 | 多语言 | 多语言 | 任意 |
| 远程发布 | 内置 | 否 | 否 | 需自建 |

*概念性对比。*

</details>

<details>
<summary><b>升级与兼容</b>（3 项）</summary>

| 主题 | 策略 |
| --- | --- |
| 多目标框架 | 多目标框架同时发布，保证老版本应用可逐步迁移 |
| 公共 API | 新增字段保持向后兼容；删除或变更公共 API 先打 `[Obsolete]` |
| 升级顺序 | 建议：StarServer/StarWeb → StarAgent → 业务 SDK |

</details>

<details>
<summary><b>FAQ</b>（5 项）</summary>

| 问题 | 解答 |
| --- | --- |
| 是否必须同时部署 StarServer 与 StarWeb? | 可同机；StarWeb 负责管理界面，StarServer 提供核心 API。小规模可同机部署，也可分离扩展。 |
| Agent 必须安装系统服务吗? | 推荐服务方式以保证宕机自恢复；也可容器模式或前台调试运行。 |
| 支持容器 / K8s 吗? | 支持。Agent 可作为 DaemonSet，业务 Pod 通过 SDK 接入；后续可结合 StarGateway 做入口治理。 |
| 配置热更新需要重启应用吗? | 不需要。SDK 自动监听，提供变更回调。 |
| 如何定制发布流程? | 可扩展构建脚本或在 Repository 中附加自定义打包脚本，再由平台执行。 |

</details>

## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。

## 文档

### 📋 核心文档（三件套）
| 文档 | 说明 |
|------|------|
| [需求文档](Doc/需求文档.md) | 愿景、核心目标、功能需求、暂缓清单（v3.2） |
| [功能清单](Doc/功能清单.md) | 全部功能实现/测试/注释状态追踪（v3.2） |
| [架构设计](Doc/架构设计.md) | 架构文档总索引，链接各组件架构（v3.2） |

### 🏗️ 主要组件架构
| 文档 | 说明 |
|------|------|
| [StarServer 架构](Doc/SVR-StarServer架构.md) | 服务端核心（应用/配置/注册/日志/平台/基础能力） |
| [NODE-StarAgent 架构](Doc/NODE-StarAgent架构.md) | 节点代理守护/监控/发布 |
| [SDK-分布式核心SDK架构](Doc/SDK-分布式核心SDK架构.md) | 客户端 SDK（配置/追踪/注册发现） |
| [APM-监控系统架构](Doc/APM-监控系统架构.md) | APM 监控子系统详细架构 |
| [PUB-远程发布架构](Doc/PUB-远程发布架构.md) | 远程发布子系统详细架构 |
| [GW-网关架构](Doc/GW-网关架构.md) | 网关总架构 |

### 🔧 子模块文档
| 文档 | 说明 |
|------|------|
| [NODE-9-指令下发](Doc/NODE-9-指令下发.md) | NODE-9 命令下发流程（WebSocket 双通道） |
| [StarAgent.Native 协议规范](StarAgent.Native/spec/protocol.md) | Go/Rust/C++ 原生 Agent 协议、Schema 与一致性测试 |
| [APM-3-链路追踪统计](Doc/APM-3-链路追踪统计.md) | APM-3 追踪统计性能瓶颈与优化 |
| [NODE-15-StarAgent远程部署](Doc/NODE-15-StarAgent远程部署.md) | NODE-15 远程部署方案 |
| [NODE-16-阿里云DNS动态域名解析](Doc/NODE-16-阿里云DNS动态域名解析.md) | NODE-16 动态域名解析 |
| [NODE-14-防火墙管理](Doc/NODE-14-防火墙管理.md) | NODE-14 防火墙端口管理 |
| [PUB-21-分布式文件存储](Doc/PUB-21-分布式文件存储.md) | 文件存储设计 |
| [PUB-23-流水线部署流程](Doc/PUB-23-流水线部署流程.md) | 流水线编译→上传→部署流程 |
| [PUB-19-应用包规范](Doc/PUB-19-应用包规范.md) | 应用包格式与规范 |
| [SVR-8-远程命令执行安全](Doc/SVR-8-远程命令执行安全.md) | 安全方案 |
| [PUB-20-部署代理](Doc/PUB-20-部署代理.md) | 部署代理使用说明 |
| [GW-8-网关使用指南](Doc/GW-8-网关使用指南.md) | 网关使用说明 |
| [NODE-17-进程管理](Doc/NODE-17-进程管理.md) | 进程启动与守护 |
| [NODE-移动端代理](Doc/NODE-移动端代理.md) | Android/iOS 简化代理、构建与冒烟 |
| [MCP-MCP架构](Doc/MCP-MCP架构.md) | MCP HTTP 服务架构 |
| [MCP-1-token管理](Doc/MCP-1-token管理.md) | MCP Token 凭证管理 |
| [MCP-2-action扩展](Doc/MCP-2-action扩展.md) | MCP Action 扩展指南 |
| [MCP-3-客户端接入](Doc/MCP-3-客户端接入.md) | MCP 客户端接入指南 |

## 参与贡献

### 构建与测试

```bash
dotnet build 星尘.slnx -c Release
dotnet test 星尘.slnx -c Release --no-build
```

- 支持 VisualStudio（最新正式版）打开解决方案编译，也兼容 `dotnet build` 命令行编译；项目文件摒弃复杂功能以追求简单易用，真正做到开箱即用。
- 公开强命名证书 `newlife.snk`，以支持独自编译替换程序集。
- CI：`.github/workflows/test.yml` 构建解决方案并运行全量测试。

### 发版

推送 `v` 前缀标签触发 `.github/workflows/publish.yml`。工作流构建 `星尘.slnx`，打包 `Stardust`、`Stardust.Extensions` 与 `DeployAgent`，并推送到 NuGet.org 与 GitHub Packages。

### 反馈

- 提交前确保 `dotnet build` 与相关测试通过，遵循仓库的代码风格与提交信息约定。
- 通过 [Issue](https://github.com/NewLifeX/Stardust/issues) 反馈缺陷与建议；欢迎 Star 与 PR。

## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 许可证

本项目基于 [MIT](https://github.com/NewLifeX/Stardust/blob/master/LICENSE) 协议开源。NewLife组件由社区共创20多年，使用MIT开源协议，**任何人可任意修改并再次发行**（无需声明来源）！许多企业基于此构建内部开发框架时，甚至可通过批量替换源码中所有`NewLife`字符串为贵公司名实现私有化定制。

欢迎 Star / Fork / Issue / PR，共建更完善的 .NET 分布式基础设施。
