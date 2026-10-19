<!-- generated from README.md by sync-readme-package.ps1; do not edit -->

# NewLife.Stardust - Stardust Distributed Platform

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/stardust?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/Stardust?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Stardust?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Stardust?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Stardust?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](https://github.com/NewLifeX/Stardust/blob/master/Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/Stardust?logo=github)](https://github.com/NewLifeX/Stardust/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](https://github.com/NewLifeX/Stardust/blob/master/README.zh-CN.md) | [繁體中文](https://github.com/NewLifeX/Stardust/blob/master/README.zh-TW.md) | [Español](https://github.com/NewLifeX/Stardust/blob/master/README.es.md) | [Deutsch](https://github.com/NewLifeX/Stardust/blob/master/README.de.md) | [日本語](https://github.com/NewLifeX/Stardust/blob/master/README.ja.md) | [Français](https://github.com/NewLifeX/Stardust/blob/master/README.fr.md) | [Português](https://github.com/NewLifeX/Stardust/blob/master/README.pt.md) | [Русский](https://github.com/NewLifeX/Stardust/blob/master/README.ru.md) | [한국어](https://github.com/NewLifeX/Stardust/blob/master/README.ko.md) | [العربية](https://github.com/NewLifeX/Stardust/blob/master/README.ar.md)
<!-- lang:end -->

**Stardust** is a lightweight distributed service platform: node management, configuration center, registry and service discovery, distributed tracing and APM, log center and remote release, with automatic service registration and discovery, load balancing, dynamic scaling and failover. Application services are like dust in the universe, and the computer nodes that host them are the stars; stars and dust together form the vast starry sky, which is Stardust.

![Stardust](https://github.com/NewLifeX/Stardust/blob/master/Doc/星尘分布式.png)

Demo system: [http://star.newlifex.com](http://star.newlifex.com)

**Contents**: [Quick Start](#quick-start) · [Main Features](#main-features) · [Ecosystem](#ecosystem) · [Documentation](#documentation) · [Contributing](#contributing) · [Team](#team) · [License](#license)

## Quick Start

### Install

- Source: clone this repository and build directly with Visual Studio / Rider / `dotnet build`
- NuGet: `NewLife.Stardust` (core); `NewLife.Stardust.Extensions` (ASP.NET Core extensions, brings the core package transitively)

```bash
dotnet add package NewLife.Stardust.Extensions
```

### Start the platform

1) Start StarServer:

```
cd Stardust.Server
dotnet run
```

2) Start StarWeb:

```
cd ../Stardust.Web
dotnet run
```

On first run, the database and tables are created automatically (SQLite by default; MySql / SqlServer / PostgreSql / Oracle / DaMeng etc. are configurable; set the connection string in `*.config` / `appsettings.*` or environment variables).

### Deploy StarAgent to business nodes

```
cd StarAgent
# Run directly (debug)
dotnet run
# Or publish and install as a system service (example)
dotnet publish -c Release -o publish
# Windows: StarAgent.exe -install
# Linux:   chmod +x StarAgent && ./StarAgent install
```

After configuring the StarServer address, the node will appear on the platform.

#### Other platforms

```
# macOS self-contained packages (osx-x64 / osx-arm64)
cd Tools && build-agent-osx.bat

# Android simplified agent (requires the Android workload)
dotnet build StarAgent.Android/StarAgent.Android.csproj -c Release
# Output: Bin/Agent/mobile/android/com.newlife.staragent-Signed.apk
pwsh StarAgent.Android/pack-android.ps1 -Mode Aot
pwsh StarAgent.Android/run-android.ps1 -Smoke

# iOS minimal agent (requires macOS + Xcode)
bash StarAgent.iOS/run-ios.sh
```

> Mobile platforms are subject to system limitations: Android requires a foreground service and a battery whitelist; iOS can only be online intermittently (reporting in the foreground and when awakened by BGTask) and does not support process supervision or remote release. See [NODE-StarAgent Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-StarAgent架构.md) for details.

### Create an application (StarWeb)

- Create a new application in "Application Management" to generate an AppId / Secret
- You can configure the category, project, expected port, heartbeat policy, etc.

### Integrate a business application

Reference the package:

```
dotnet add package NewLife.Stardust.Extensions
```

In Program / Startup:

```csharp
var builder = WebApplication.CreateBuilder(args);

// One line: connect to Stardust, inject the tracing middleware, and enable
// HttpClient / WebApi / database / Redis auto-instrumentation.
var star = builder.Services.AddStardust("OrderService");

var app = builder.Build();
// app.UseStardust(); // Optional: only when you need a custom middleware order
app.RegisterService("OrderService"); // Register with Stardust
// app.ConsumeService("UserService"); // Optional: consume other services
app.Run();
```

After it starts, you can see the instance online, performance metrics, and call chains in StarWeb.

### Multi-language SDK Support

In addition to the .NET SDK, Stardust also provides multi-language SDK support:

| SDK | Description | Documentation |
| --- | --- | --- |
| JavaScript (browser) | UMD single file; login/heartbeat performance/page and API instrumentation | [SDK/JavaScript/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/JavaScript/README.md) |
| JavaScript/Node.js | Login/heartbeat performance/Express/Koa instrumentation | [SDK/JavaScript/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/JavaScript/README.md) |
| Java | Java 8+, Maven, zero runtime dependencies | [SDK/Java/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/Java/README.md) |
| Go | Go 1.18+, APM and configuration center | [SDK/Go/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/Go/README.md) |
| Python | Python 3.7+ APM and configuration center | [Doc/SDK/stardust-sdk-python.md](https://github.com/NewLifeX/Stardust/blob/master/Doc/SDK/stardust-sdk-python.md) |
| PHP | PHP 7.4+ APM and configuration center | [SDK/PHP/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/PHP/README.md) |
| Classic ASP | VBScript login/heartbeat/instrumentation | [SDK/ASP/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/ASP/README.md) |

> See [SDK/README.md](https://github.com/NewLifeX/Stardust/blob/master/SDK/README.md) for the unified capability matrix, naming, and release plan.

**JavaScript SDK quick example:**

```javascript
// Node.js: one-line create and start
const { init } = require('./SDK/JavaScript');
const tracer = init({
  server: 'http://star.example.com:6600',
  appId: 'MyApp',
  secret: 'MySecret',
  version: '1.2.3',
});

// Browser: prefer the one-line loader.js (see SDK/README.md), or call init directly
const webTracer = Stardust.init({
  server: 'http://star.example.com:6600',
  appId: 'MyWebApp',
  tokenProvider: async () => (await fetch('/api/stardust-token')).json(),
  version: '1.2.3',
});
// Automatically collects page loads, routes, JS errors, fetch/XHR
```

### Template scaffolding

Run the following commands in a command line to quickly try the NewLife components:

```
dotnet new install NewLife.Templates
dotnet new nconsole --name test
cd test
dotnet run
```

## Main Features

<details>
<summary><b>Why Stardust</b> (7)</summary>

| Advantage | What it gives you |
| --- | --- |
| All-in-one | Node management / configuration center / registry / service discovery / distributed tracing / APM / log center / remote release / resource monitoring, with a unified model and UI |
| Ultra-lightweight | The core library has no heavy third-party dependencies and runs in a single process, with automatic database and table creation |
| Strong compatibility | One ecosystem covers .NET Framework 4.5 through the latest .NET 10, across multiple platforms (Windows/Linux/macOS/Android/iOS/ARM/IoT) |
| Multi-language agents | Lightweight Go/Rust/C++ native agents can connect directly to StarServer, covering scenarios without a .NET runtime, embedded systems, and OpenWrt |
| High performance | Built on more than a decade of optimization in NewLife.Core & NewLife.Net, with excellent network throughput and serialization performance |
| Easy integration | Integrate with just a few lines of code; the same approach for ASP.NET Core / Console / Service / IoT edge |
| Built-in operations | StarAgent daemon + remote release + metric & log collection, greatly reducing the complexity of distributed operations |

</details>

<details>
<summary><b>Platform Core</b> (13)</summary>

| Feature | Key Characteristics |
| --- | --- |
| Stardust Agent | StarAgent is deployed on every application server and, as a system service, supervises local applications and monitors node performance. It supports Windows/Linux/macOS and also provides simplified Android/iOS versions (registration heartbeat/metric/log/command subset) |
| Node management | Centrally manage massive numbers of StarAgents across machines, covering server nodes in data centers, remote desktop industrial PCs, and IoT edge computing nodes |
| Application management | Assign an access key to each application system for the configuration center, application monitoring, service registration, service discovery, etc. |
| Configuration center | Each application reads its own configuration from the configuration center, which facilitates multi-instance deployment and centralized management |
| Monitoring center | Each application (service provider and service consumer) reports performance metrics and other monitoring data to the center |
| Registry | Each microservice application (service provider) reports its own service information to the registry |
| Service discovery | Each business application (service consumer) pulls provider information for the specified service from the registry and makes distributed calls (without going through a gateway) |
| Remote release | Publish application packages from the Stardust console to multiple application servers or IoT edge nodes with StarAgent installed; StarAgent starts and supervises the processes |
| Log center | Each application (service provider and service consumer) reports log data to the center, which is viewed centrally in Stardust Web |
| [Multi-provider DNS DDNS](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-16-阿里云DNS动态域名解析.md) | StarAgent supports dynamic DNS (DDNS), automatically registering the node's public IP with DNS (Alibaba Cloud/Tencent Cloud (DNSPod)/UCloud (UDNS)/AWS Route53), suitable for dynamic IP environments |
| .NET runtime management | StarServer automatically synchronizes official .NET release information, and StarAgent can install .NET runtime versions on demand, working with upgrade commands to enable automatic upgrades and smoke-test rollback |
| Pipeline | Supports creating pipelines and run/step models, automatically triggering the build and release process via webhooks, and enabling automated deployment with repository SSH keys and username/password authentication |
| Gateway v2.0 | StarGateway has been refactored and upgraded, supporting a dynamic routing engine, load balancing, health checks, TLS termination, WebSocket proxying, and static file hosting (SPA fallback/large-file protection), with integrated APM tracing |

</details>

<details>
<summary><b>Architecture &amp; Components</b> (7)</summary>

```
+--------------------+        +-------------------+        +---------------------+
|   App Instance     | <----> |  Registry/Config  | <----> |   StarServer Core   |
| (AddStardust ext)  |        | (service/config/  |        | (API & task/stats)  |
+---------+----------+        |    discovery)     |        +----------+----------+
          |                   +---------+---------+                   |
          v                             ^                             v
   StarAgent (node supervision/deploy/monitoring)             StarWeb admin UI
          |                             |                             |
       Remote release / performance & logs / metrics ---+--- Data storage (XCode ORM, multi-database)
```

| Component | Function and typical deployment |
| --- | --- |
| StarServer | Service platform backend (API / configuration / registry / release / metric aggregation / statistics computation). 1~N, horizontally scalable; persistence depends on a database/Redis (optional) |
| StarWeb | Management and visualization UI based on NewLife.Cube. 1, on the same machine as StarServer or separated |
| StarAgent | Installed on every node host: process supervision, remote release, and monitoring collection (CPU/memory/network/custom). N (one instance per machine); Windows service, Linux systemd, container, or background process |
| Stardust Core Library (SDK) | Client SDK providing configuration pull, registry discovery, call tracing, and performance & log reporting. Runs in the same process as the business application; integrated via AddStardust/UseStardust |
| Stardust.Extensions | ASP.NET Core extensions (AddStardust / UseStardust / RegisterService / ConsumeService). Used by business Web/API projects; exposes middleware and convenience methods for registration/discovery |
| Data layer (Stardust.Data) | XCode-based entities and statistics tasks |
| StarGateway / DeployAgent (optional) | Traffic forwarding / build and distribution support; deployed as required and can work together with service discovery |

</details>

<details>
<summary><b>Monitoring Center</b> (4)</summary>

| Capability | Description |
| --- | --- |
| Application monitoring | Performance metrics reported by every application, visualized in the Stardust console |
| Call chain | End-to-end trace of a business call, for example an IoT platform remotely controlling a garden water pump: [portal](http://star.newlifex.com/trace?id=ac15452e1649035594650011be21ec) |
| Relationship graph | Service dependency graph built from reported traces |
| TraceAnonymous | The call chain page supports the `TraceAnonymous` property. When set, unauthenticated users are allowed to access it, which is suitable for public display or demonstration scenarios |

![Application monitoring](https://github.com/NewLifeX/Stardust/blob/master/Doc/应用监控.png)
![Call chain](https://github.com/NewLifeX/Stardust/blob/master/Doc/调用链.png)
![Relationship graph](https://github.com/NewLifeX/Stardust/blob/master/Doc/关系图.png)

Stardust monitoring introduction: [https://newlifex.com/blood/stardust_monitor](https://newlifex.com/blood/stardust_monitor)

</details>

<details>
<summary><b>Configuration Center</b> (4)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Centralized configuration | Add Key=Value entries for the specified application in the platform's "Configuration Center"; the client delivers them automatically |
| Isolation and inheritance | Per-environment / per-project configuration with template inheritance |
| Push updates | The client automatically long-polls/pushes updates; no restart is required |
| Change events | After a change, application instances receive a change event that can be used to hot-update business parameters |

```csharp
var star = services.AddStardust("OrderService");
var cfg = star.GetConfig();
var dbConn = cfg["Db:ConnStr"]; // Delivered automatically from the platform
```

</details>

<details>
<summary><b>Service Registration and Discovery</b> (4)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Registration | `app.RegisterService("SvcName", address: null, environment, healthUrl)` reports the service instance to the registry |
| Address resolution | When address is not specified explicitly, an available local address is resolved automatically |
| Health probe | healthUrl provides a health probe for the registered service |
| Discovery | `app.ConsumeService("SvcName")` automatically maintains the provider list, supporting round-robin load balancing / failure eviction; `ServiceProviderResolver` allows custom strategies |

```csharp
app.RegisterService("SvcName", address: null, environment, healthUrl);
app.ConsumeService("SvcName");
```

</details>

<details>
<summary><b>Distributed Tracing &amp; Metrics</b> (5)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Automatic spans | The SDK has a built-in ITracer; entry points automatically create spans |
| Manual spans | Create spans explicitly wherever business-level tracing is needed |
| Performance counters | QPS, latency distribution and error rate |
| System metrics | Agent collects CPU/memory/threads/network |
| Logs | Reported through NewLife.Log and queried centrally in the Web |

```csharp
using var span = star.Tracer?.NewSpan("biz:calc", new { id });
```

</details>

<details>
<summary><b>Remote Release / Continuous Delivery</b> (5)</summary>

| Step | Description |
| --- | --- |
| 1. Create a release application | In the "Release Center" fill in: Repository / Branch / ProjectPath / PackageFilters / build arguments |
| 2. Bind target nodes | Bind the target nodes (StarAgent) |
| 3. Trigger the build | The platform pulls code → builds → filters packages → distributes to nodes → Agent unzips and deploys → supervises execution |
| 4. Canary release | Release to a subset of nodes first, then to all after verification |
| 5. Rollback | Historical version packages are retained, and one-click rollback is available |

</details>

<details>
<summary><b>Security &amp; Permissions</b> (3)</summary>

| Capability | Key Characteristics |
| --- | --- |
| Permission model | Inherits the NewLife.Cube permission system (user / role / menu / data permissions) |
| Credentials | AppId + Secret are only visible at creation time; please keep them safe |
| Isolation | Resource isolation by project and category is supported |

</details>

<details>
<summary><b>Data &amp; Storage</b> (3)</summary>

| Capability | Key Characteristics |
| --- | --- |
| ORM | XCode (automatic table creation/migration/table sharding/read-write splitting) |
| Databases | SQLite / MySql / SqlServer / Oracle / PostgreSql / DaMeng, etc. |
| Retention | Metrics, traces, and logs are stored in sharded tables, with automatic archiving and cleanup for long-running systems (background scheduled tasks) |

</details>

<details>
<summary><b>Performance</b> (4)</summary>

| Characteristic | Description |
| --- | --- |
| High-throughput network stack | From NewLife.Net |
| Non-intrusive APM | Statistics with minimal instrumentation |
| Multi-level caching | Optional Redis integration (Redis-based statistics acceleration is optional in Stardust.Server) |
| Low GC | Internal use of object pools / string builder pools |

</details>

<details>
<summary><b>Comparison with Common Solutions</b> (5)</summary>

| Concern | Stardust | Single registry (e.g. Consul/Eureka) | Configuration center only | Traditional script deployment |
| ---- | ---- | ------------------------------- | --------- | ------------ |
| Feature coverage | Registry/discovery/configuration/release/monitoring/tracing | Registry + health | Configuration | Distribution only |
| Integration complexity | Low | Medium | Medium | High |
| Operations consistency | High | Medium | Medium | Low |
| Language ecosystem | Primarily .NET | Multi-language | Multi-language | Any |
| Remote release | Built-in | No | No | Build your own |

*Conceptual comparison.*

</details>

<details>
<summary><b>Upgrade &amp; Compatibility</b> (3)</summary>

| Topic | Policy |
| --- | --- |
| Multi-targeting | Multiple target frameworks are published simultaneously so that older applications can migrate gradually |
| Public API | New fields remain backward compatible; public APIs are marked `[Obsolete]` before removal or change |
| Upgrade order | Recommended: StarServer/StarWeb → StarAgent → business SDK |

</details>

<details>
<summary><b>FAQ</b> (5)</summary>

| Question | Answer |
| --- | --- |
| Must StarServer and StarWeb be deployed together? | They can run on the same machine; StarWeb provides the management UI and StarServer provides the core APIs. For small-scale deployments they can share a machine, or they can be separated for scaling. |
| Must the Agent be installed as a system service? | The service mode is recommended so that it recovers automatically after a crash; container mode or foreground debugging is also possible. |
| Are containers / K8s supported? | Yes. The Agent can run as a DaemonSet, and business Pods integrate through the SDK; StarGateway can later be combined for ingress governance. |
| Does configuration hot reload require restarting the application? | No. The SDK listens automatically and provides change callbacks. |
| How do I customize the release process? | You can extend the build script or attach a custom packaging script in the Repository, which is then executed by the platform. |

</details>

## Ecosystem

NewLife is a layered ecosystem of independently versioned open-source projects. Stardust sits in the **Web, AI & Distributed** layer and works with NewLife.Redis (high-performance cache & queue), NewLife.Remoting (RPC / HTTP hybrid communication), NewLife.XCode (big-data ORM) and NewLife.Agent (daemon service capability, already built into StarAgent).

| Layer | Projects |
| --- | --- |
| Foundation | Core · XCode · Net · Remoting · Agent · Templates |
| Data & Messaging | Redis · RocketMQ · MQTT · NovaDb · MySql |
| Web, AI & Distributed | Cube · Stardust · AntJob · NewLife.AI |
| Industrial & IoT | IoT · Modbus · Siemens · Omron · Melsec · OPC · BACnet · ZeroIoT |
| Vehicle Telematics | JT808 · JT809 · JT1078 |
| Domain Components | Office · Audio · Video · Map · IP |
| Tools & Products | CrazyCoder · GitCandy · LuckyClover |

Full ecosystem → [NewLifeX/X#ecosystem](https://github.com/NewLifeX/X#ecosystem)

<details>
<summary><b>NewLife Project Matrix</b></summary>

All projects support net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5 by default; older versions (2024.0801) support net4.0/net2.0

|                               Project                               | Year  | Description                                                                                        |
| :--------------------------------------------------------------: | :---: | ------------------------------------------------------------------------------------------- |
|                             Foundation Components                             |       | Supports other middleware and product projects                                                                  |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | Core library: logging, configuration, caching, networking, serialization, APM performance tracing                                         |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | Big-data middleware, single table at the tens-of-billions scale, MySql/SQLite/SqlServer/Oracle/PostgreSql/DaMeng, automatic table sharding, read-write splitting |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | Networking library, tens of millions of TPS per machine (22.66 million tps), millions of connections per machine (4 million long-lived TCP connections)                     |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | Protocol communication library providing a C/S application communication framework, supporting Http/RPC communication frameworks, high throughput, low overhead and easy access for IoT devices        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | Cube rapid development platform, integrating user permissions, SSO login, OAuth server, etc., verified by a project with 10 billion rows per table               |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | Service management component that installs applications as operating system daemons: Windows services, Linux Systemd                   |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero scaffold; project templates NewLife.Templates based on the NewLife component ecosystem: Web, WebApi, Service        |
|                             Middleware                              |       | Connects to well-known middleware platforms                                                                          |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis client, microsecond latency, millions of throughput, rich message queues, verified by projects with tens of billions of data                   |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | Pure managed RocketMQ client, supporting Apache RocketMQ and Alibaba Cloud Message Queue, verified by billion-scale projects                     |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | IoT messaging protocol, MqttClient/MqttServer; the client supports Alibaba Cloud IoT                               |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT standard library defining various communication protocol standards in the IoT domain                                             |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII implemented on the IoT standard library, supporting the ZeroIoT platform and IoTEdge gateway            |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | Siemens PLC protocol implemented on the IoT standard library, supporting the IoT platform and IoTEdge                                      |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | Map component library wrapping Baidu Maps, Amap, Tencent Maps, and Tianditu                                        |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | Audio codec library: PCM/ADPCMA/G711A/G722U/WAV/AAC                                                |
|                             Product Platforms                             |       | Product-platform level, ready to use after build and deployment, customizable                                                      |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | Stardust, distributed service platform: node management, APM monitoring center, configuration center, registry, release center                   |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | AntJob distributed big-data computing platform (real-time/offline), ant-moving sharding philosophy, verified by trillion-scale projects         |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | Enterprise ERP: product management, customer management, sales management, supplier management                                           |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | CrazyCoder developer tools: networking, serial port, encryption/decryption, regular expressions, Modbus, MQTT                      |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | Simple file storage supporting centralized file storage in distributed systems.                                                |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | Product-grade reverse proxy: NAT proxy, Http proxy                                                           |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http stress testing tool                                                                            |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git source code management system                                                                           |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | Embedded operating system, fully independent, supporting the ARM Cortex-M chip architecture                                      |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | Embedded industrial computer, IoT edge gateway, high-performance .NET8 host, used in industry, agriculture, transportation, and healthcare             |
|                         FIoT IoT Platform                          | 2020  | Overall IoT solution for buildings, environmental protection, and agriculture, integrating software, hardware, and big-data analytics, verified by a 100,000-point-per-machine project      |
|                        UWB High-Precision Indoor Positioning                         | 2020  | Centimeter-level (10~20 cm) high-precision indoor positioning, integrated hardware and software, interworking with other systems, verified by large exhibition hall projects             |

</details>

## Documentation

### 📋 Core Documents (Trilogy)
| Document | Description |
|------|------|
| [Requirements Document](https://github.com/NewLifeX/Stardust/blob/master/Doc/需求文档.md) | Vision, core goals, functional requirements, deferred list (v3.2) |
| [Feature List](https://github.com/NewLifeX/Stardust/blob/master/Doc/功能清单.md) | Implementation/test/comment status tracking for all features (v3.2) |
| [Architecture Design](https://github.com/NewLifeX/Stardust/blob/master/Doc/架构设计.md) | Architecture document index linking the architecture of each component (v3.2) |

### 🏗️ Main Component Architecture
| Document | Description |
|------|------|
| [StarServer Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/SVR-StarServer架构.md) | Server core (application/config/registry/logging/platform/foundation) |
| [NODE-StarAgent Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-StarAgent架构.md) | Node agent supervision/monitoring/release |
| [SDK-Distributed Core SDK Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/SDK-分布式核心SDK架构.md) | Client SDK (configuration/tracing/registry discovery) |
| [APM-Monitoring System Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/APM-监控系统架构.md) | Detailed architecture of the APM monitoring subsystem |
| [PUB-Remote Release Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/PUB-远程发布架构.md) | Detailed architecture of the remote release subsystem |
| [GW-Gateway Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/GW-网关架构.md) | Overall gateway architecture |

### 🔧 Submodule Documents
| Document | Description |
|------|------|
| [NODE-9-Command Dispatch](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-9-指令下发.md) | NODE-9 command dispatch flow (WebSocket dual channel) |
| [StarAgent.Native Protocol Specification](https://github.com/NewLifeX/Stardust/blob/master/StarAgent.Native/spec/protocol.md) | Go/Rust/C++ native agent protocol, schema, and conformance tests |
| [APM-3-Trace Statistics](https://github.com/NewLifeX/Stardust/blob/master/Doc/APM-3-链路追踪统计.md) | APM-3 trace statistics performance bottlenecks and optimization |
| [NODE-15-StarAgent Remote Deployment](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-15-StarAgent远程部署.md) | NODE-15 remote deployment solution |
| [NODE-16-Alibaba Cloud DNS Dynamic DNS](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-16-阿里云DNS动态域名解析.md) | NODE-16 dynamic DNS resolution |
| [NODE-14-Firewall Management](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-14-防火墙管理.md) | NODE-14 firewall port management |
| [PUB-21-Distributed File Storage](https://github.com/NewLifeX/Stardust/blob/master/Doc/PUB-21-分布式文件存储.md) | File storage design |
| [PUB-23-Pipeline Deployment Flow](https://github.com/NewLifeX/Stardust/blob/master/Doc/PUB-23-流水线部署流程.md) | Pipeline build → upload → deployment flow |
| [PUB-19-Application Package Specification](https://github.com/NewLifeX/Stardust/blob/master/Doc/PUB-19-应用包规范.md) | Application package format and specification |
| [SVR-8-Remote Command Execution Security](https://github.com/NewLifeX/Stardust/blob/master/Doc/SVR-8-远程命令执行安全.md) | Security solution |
| [PUB-20-Deployment Agent](https://github.com/NewLifeX/Stardust/blob/master/Doc/PUB-20-部署代理.md) | Deployment agent usage guide |
| [GW-8-Gateway User Guide](https://github.com/NewLifeX/Stardust/blob/master/Doc/GW-8-网关使用指南.md) | Gateway usage guide |
| [NODE-17-Process Management](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-17-进程管理.md) | Process startup and supervision |
| [NODE-Mobile Agent](https://github.com/NewLifeX/Stardust/blob/master/Doc/NODE-移动端代理.md) | Android/iOS simplified agent, build and smoke tests |
| [MCP-MCP Architecture](https://github.com/NewLifeX/Stardust/blob/master/Doc/MCP-MCP架构.md) | MCP HTTP service architecture |
| [MCP-1-Token Management](https://github.com/NewLifeX/Stardust/blob/master/Doc/MCP-1-token管理.md) | MCP token credential management |
| [MCP-2-Action Extension](https://github.com/NewLifeX/Stardust/blob/master/Doc/MCP-2-action扩展.md) | MCP action extension guide |
| [MCP-3-Client Integration](https://github.com/NewLifeX/Stardust/blob/master/Doc/MCP-3-客户端接入.md) | MCP client integration guide |

## Contributing

### Build and test

```bash
dotnet build 星尘.slnx -c Release
dotnet test 星尘.slnx -c Release --no-build
```

- The solution opens in Visual Studio (latest stable) and also builds from the command line with `dotnet build`; project files avoid complex features to stay out-of-the-box.
- The strong-name certificate `newlife.snk` is published so you can build and replace assemblies independently.
- CI: `.github/workflows/test.yml` builds the solution and runs the full test suite.

### Release

Push a `v`-prefixed tag to trigger `.github/workflows/publish.yml`. The workflow builds `星尘.slnx`, packs `Stardust`, `Stardust.Extensions` and `DeployAgent`, and pushes the packages to NuGet.org and GitHub Packages.

### Feedback

- Before submitting, make sure `dotnet build` and the relevant tests pass; follow the repository conventions for code style and commit messages.
- Report bugs and suggestions via [Issue](https://github.com/NewLifeX/Stardust/issues); Stars and PRs are welcome.

## Team

![NewLife](https://newlifex.com/logo.png)

NewLife was founded in 2002 and is an IoT industry solution provider for the new era, dedicated to software and hardware application consulting, system architecture planning, and development services.  
The 80+ open-source projects led by the team are widely used across industries, with cumulative NuGet downloads exceeding 4 million.  
The team's big-data middleware NewLife.XCode, AntJob scheduling and computing platform, Stardust distributed platform, cache and queue component NewLife.Redis, and IoT platform FIoT have all been successfully applied in power, higher education, the Internet, telecommunications, transportation, logistics, industrial control, healthcare, and museums, providing customers with advanced, reliable, secure, high-quality, and easily extensible products and system integration services.  

We will continue to improve our services to become a long-term trusted partner of our customers, and through continuous innovation and development, become an excellent IoT service provider.  

`The NewLife team started in 2002; some open-source projects have a history of more than 20 years, and the source repositories retain all modification records since 2010`  
Website: https://newlifex.com  
Open source: https://github.com/newlifex  
QQ groups: 1600800/1600838  
WeChat official account:  
![NewLife](https://newlifex.com/stone.jpg)

## License

This project is licensed under [MIT](https://github.com/NewLifeX/Stardust/blob/master/LICENSE). NewLife components have been co-created by the community for more than 20 years and are released under the MIT open-source license: **anyone can modify and redistribute them freely** (no attribution required). Many enterprises build internal development frameworks on top of them; private customization can even be achieved by batch-replacing all `NewLife` strings in the source code with the company name.

Welcome to Star / Fork / Issue / PR to build a better .NET distributed infrastructure together.
