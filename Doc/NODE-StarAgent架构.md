# StarAgent 架构

> 版本：v1.0 | 日期：2026-07-15
> 对应模块：NODE 节点管理
> 子模块文档：[NODE-9-指令下发](NODE-9-指令下发.md) | [NODE-14-防火墙管理](NODE-14-防火墙管理.md) | [NODE-15-StarAgent远程部署](NODE-15-StarAgent远程部署.md)

---

## 1. 概述

StarAgent 是部署在每台应用服务器上的节点代理程序，以系统服务（Windows Service / Linux systemd）方式运行，负责：

- **进程守护**：管理本地业务应用的启动、停止、守护、自动重启
- **监控采集**：采集 CPU/内存/磁盘/网络等系统指标，上报给 StarServer
- **远程发布**：接收 StarServer 的发布指令，下载应用包并执行部署
- **命令执行**：接收并执行远程命令（启动服务、停止服务、脚本执行等）
- **自身升级**：自动检测 StarAgent 新版本并升级

### 1.1 平台支持

| 平台 | 宿主工程 | 运行方式 | 能力 |
|------|----------|----------|------|
| Windows | `StarAgent` | Windows 服务 | 完整 |
| Linux | `StarAgent` | systemd / SysVinit / Procd / RcInit | 完整 |
| macOS | `StarAgent` | LaunchAgents（`OSXLaunch`） | 完整（防火墙/部分部署能力按平台降级） |
| Android | `StarAgent.Android` | 前台服务 + 开机自启 | 简化：注册/心跳/设备指标/日志/命令子集 |
| iOS | `StarAgent.iOS` | App + BGTask | 极简：前台与后台唤醒时上报，间歇在线 |

移动端共用 `StarAgent.Mobile` 核心，仅承载登录、心跳、命令分发与平台信息填充；不包含进程守护、远程发布与 Web 面板。
命令随心跳响应下发（`PingResponse.Commands`），因此移动端不需要建立长连接。

移动端使用独立 `ProductCode=StarAgent-Mobile`，平台差异由 `OSKind` 表达；能力标签通过 `NodeInfo.Plugins` 上报（`agent:dotnet-mobile`、`cap:exec=false`、`cap:upgrade=false`、`cap:deploy=false`、`os:android`/`os:ios`）。
构建产物统一输出到 `Bin/Agent/mobile/{android,ios}`，打包与冒烟脚本为 `StarAgent.Android/pack-android.ps1`、`StarAgent.Android/run-android.ps1`、`StarAgent.iOS/run-ios.sh`。
详细说明见 [NODE-移动端代理](NODE-移动端代理.md)。

---

## 2. 核心功能

### 2.1 注册与心跳

```
启动 → 读取硬件信息 → 计算节点编码 → Login → 获取令牌 → 心跳循环
```

- 节点编码公式：`Crc({ProductCode}@{UUID}@{DiskID}@{Macs})`
- 心跳周期：60 秒，同时上报 CPU/内存/磁盘/网络等指标
- 在线会话：通过 WebSocket 长连接保持，断线自动重连

### 2.2 进程守护

`ServiceManager` 管理本地应用的完整生命周期：

| 功能 | 说明 |
|------|------|
| 启动进程 | 按部署模式（Standard/Shadow/Hosted/Task）启动 |
| 自动重启 | 进程异常退出时自动拉起 |
| 内存限制 | 内存超限自动重启 |
| 文件监控 | 文件变化自动重启（热更新） |
| 优雅关闭 | SIGTERM → 超时 → SIGKILL |

### 2.3 监控采集

`MachineInfoProvider` 采集系统指标：

- CPU 使用率
- 内存使用率（总/已用/可用）
- 磁盘使用率
- 网络流量
- 进程列表
- 自定义指标

### 2.4 命令执行

通过 WebSocket 双通道接收命令：

| 命令类型 | 说明 |
|----------|------|
| `StartService` | 启动指定部署应用 |
| `StopService` | 停止指定部署应用 |
| `RestartService` | 重启指定部署应用 |
| `InstallService` | 安装系统服务 |
| `UninstallService` | 卸载系统服务 |
| `ExecuteCommand` | 执行 Shell 命令 |
| `Upgrade` | 升级 StarAgent 自身 |

### 2.5 产品编码与升级匹配

StarServer 通过 `Node.ProductCode` 区分产品：标准 .NET Agent 为 `StarAgent`，原生实现为 `StarAgent-Go`、`StarAgent-Rust`、`StarAgent-Cpp`。

自动升级按以下顺序匹配：

1. `ProductRelease.ProductCode` 必须为空（通用版本）或与节点产品编码一致；
2. `ProductPackage` 先按 `OSKind` / `Architecture` 过滤，0 表示通用；
3. 再按运行时匹配：.NET 使用主版本号（如 `8.0.6` → `8`），原生 Agent 使用 `node.Runtime` 前缀（`go` / `rust` / `cpp`）精确匹配 `TargetRuntime`；
4. 未命中时回退 `TargetRuntime="*"` 的万能包。

原生 Agent 没有 .NET 运行时，服务端不会向其推送 `framework/install` 指令。首版原生 Agent 收到非空升级响应时只记录日志并上报 `Upgrade` 事件，不执行自升级。

---

## 3. 通信架构

```
┌─────────────────┐          HTTP/WebSocket         ┌─────────────────┐
│   StarAgent     │ ◄────────────────────────────►   │   StarServer    │
│                 │     Login / Ping / Command       │                 │
│  ┌───────────┐  │                                  │  ┌───────────┐  │
│  │ StarClient │  │                                  │  │NodeCtrl   │  │
│  └───────────┘  │                                  │  └───────────┘  │
│  ┌───────────┐  │                                  │  ┌───────────┐  │
│  │ServiceMgr │  │                                  │  │ DeploySvc │  │
│  └───────────┘  │                                  │  └───────────┘  │
│  ┌───────────┐  │                                  │                 │
│  │MachineInfo│  │                                  │                 │
│  └───────────┘  │                                  │                 │
└─────────────────┘                                  └─────────────────┘
```

---

## 4. 部署模式

| 模式 | 说明 | 进程关系 |
|------|------|----------|
| **Standard** | 直接运行子进程 | StarAgent → 子进程 |
| **Shadow** | 影子模式，新版先启动再切换 | StarAgent → 子进程（旧）→ 子进程（新）|
| **Hosted** | 宿主进程托管 | StarAgent → Host → 插件 |
| **Task** | 一次性任务 | StarAgent → 任务进程（执行后退出） |

> **运行环境**：.NET 版本在 Linux 上依赖 ICU（安装 `libicu`）。无 ICU 的老旧/嵌入式系统可设置 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 降级运行，此时只有英文界面与日志。界面语言默认跟随系统/浏览器语言，可在面板右上角切换并写入 Core 全局配置 `NewLife.Setting.Language`，详见 [多语言本地化](多语言本地化.md)。

---

## 5. 关键配置

通过 `StarAgent/Setting.cs` 管理：

| 配置项 | 说明 |
|--------|------|
| `ServerUrl` | StarServer 地址 |
| `NodeCode` | 节点编码（自动计算） |
| `Secret` | 节点密钥（自动注册后获取） |
| `LocalPort` | 本地 API 端口（供 StarGateway 调用） |
| `HeartbeatPeriod` | 心跳周期（默认 60 秒） |
| `StartTimeout` | 应用启动超时（默认 30 秒） |
| `StopTimeout` | 应用停止超时（默认 10 秒） |
| `MemoryLimit` | 内存限制（MB，0 不限制） |
| `AutoStart` | 开机自启 |

## 6. Web 管理面板

### 6.1 概述

StarAgent 基于 NewLife.Agent 内置的 `AgentWebPanel` 框架，提供了轻量级 Web 管理面板。该面板内嵌于 StarAgent 进程中，通过 HTTP 访问，无需额外部署。

- **框架**：`NewLife.Agent.WebPanel.AgentWebPanel`
- **实现**：`StarAgent.WebPanel.StarPanel`（继承定制）
- **API**：`StarAgent.WebPanel.StarApi`
- **前端**：嵌入式 `index.html` SPA（单页应用）
- **监听端口**：默认 5500（`staragent.config` 中 `LocalPort`，启动时覆盖 `agent.config` 的 `WebPort`）
- **本地 UDP API**：同一端口承载本机 UDP RPC 与局域网广播搜索（`Api/Info`），固定使用 PascalCase 字段名（如 `LocalIP`、`FileVersion`），不受面板全局 camelCase 策略影响，供码神工具 ApiServer 搜索与本地命令行解析。每次 `Info` 探测都作为 `LocalProbe` 事件写入节点历史；无请求体的广播探测在事件备注与本地日志中记录来源地址

### 6.2 面板功能

面板采用 7 个 Tab 的布局：

| Tab | 类型 | 功能 |
|-----|------|------|
| 📊 **状态** | 增强 | 服务状态、资源监控（CPU/内存/线程/句柄/网络/磁盘IO）、本机详情（磁盘分区/网卡/进程 Top/系统信息） |
| 📦 **子服务** | 新增 | 子服务 CRUD（添加/编辑/删除）、启停重启操作、运行状态实时查看 |
| ⚡ **控制** | 保留 | StarAgent 自身服务启停控制 |
| ⚙ **配置** | 保留 | AgentSetting 配置查看与修改 |
| 🌐 **星尘设置** | 新增 | StarSetting（Server/AppKey/Secret）+ StarAgentSetting（LocalPort/Code/Channel/Delay 等）配置查看与修改 |
| 📋 **日志** | 保留 | 日志文件浏览与实时查看 |
| 🐕 **看门狗** | 保留 | 看门狗服务状态监控 |

### 6.3 架构说明

```
浏览器 ──HTTP──► StarAgent Web 面板 (Port 5580)
                     │
          ┌──────────┴──────────┐
          ▼                     ▼
   HttpServer              EmbeddedResource
   ┌─────────────┐        ┌──────────────┐
   │  /api/*      │        │  /*           │
   │  ApiController│        │  index.html   │
   │  (内置)       │        │  favicon.ico  │
   │              │        └──────────────┘
   │  /api/star/* │
   │  StarAgentApi│
   │  Controller  │
   └──────┬──────┘
          │
          ▼
   ServiceManager / StarSetting / StarAgentSetting
```

- **内置 API**（`/api/*`）：登录鉴权、服务状态、Agent配置、日志、看门狗 — 由 `AgentWebPanel.ApiController` 提供
- **StarAgent API**（`/api/star/*`）：子服务管理、星尘配置、本机信息 — 由 `StarApi` 提供
- **静态文件**：`/` 根路径映射到嵌入式 `index.html`

### 6.4 扩展机制

`AgentWebPanel` 提供 `GetExtensions()` 虚方法，支持通过 `PanelExtension` 注册自定义面板。未来插件可通过重写此方法动态添加 Tab。

### 6.5 鉴权策略

面板所有接口统一要求登录（由 `AgentWebPanel` 注册的鉴权中间件拦截），仅登录页、静态资源、`/api/Login`、`/api/Languages` 等公开路径例外；**本机访问同样需要口令**。登录后签发 Bearer Token，有效期 24 小时。

凭据在 `agent.config` 中通过 `WebUserName` / `WebPassword` 配置。`WebPassword` 默认 `admin`，保存为加盐哈希（`IPasswordProvider`，默认 `SaltPasswordProvider`），也可直接填写明文，首次登录成功后自动升级为哈希；需要更强散列时，可在面板构造后替换 `AgentWebPanel.PasswordProvider`。认证方式可扩展：子类重写 `OnAuthenticate` 即可接入域账号或单点登录（当前 StarAgent 未启用）。

配置页中的密钥字段只写不读：`StarApi.GetStarConfig` 对应用密钥与节点密钥一律返回空值，前端以“输入新值以修改”呈现；`UpdateStarConfig` 收到空密钥时保持原值，避免留空保存把密钥清空。

### 6.6 安全默认与远程访问

面板默认仅本机监听（`WebBind` 默认 `127.0.0.1`），局域网内其他机器无法访问；设备上请使用 `http://127.0.0.1:5500` 打开面板。

需要远程管理时，在 `staragent.config` 配置 `WebBind` 为 `*`（所有网卡）或指定 IP，重启 StarAgent 后生效（也可在面板“星尘设置”中修改）：

- 框架默认值：`NewLife.Agent` 的 `Agent.config` 中 `WebBind` 默认 `127.0.0.1`；StarAgent 启动时按 `staragent.config` 的同名配置覆盖，因此面板监听地址/端口不在 Agent 配置页开放编辑
- 非法绑定地址回退本机回环，避免配置笔误把面板误开放到局域网
- 远程访问不内置 TLS：优先用 Nginx/Caddy 反向代理或 SSH 隧道提供 HTTPS，只把本机回环端口交给面板
- 安全拒绝：监听非本机地址且口令仍为空或 `admin` 时，面板**拒绝启动**；先在 `127.0.0.1` 登录改密，或直接修改配置中的 `WebPassword` 后再开放
- 本机访问同样需要登录口令，不再有“本地免鉴权”级别

### 6.7 多实例与地址重用

StarAgent 的 Web 面板在回环地址（默认 `127.0.0.1`）上启用 `SO_REUSEADDR` 地址重用：

- 多个 StarAgent 实例可同时监听同一个 `LocalPort`（默认 5500）
- 先启动的实例接收请求；后启动的实例处于待命状态，不接收新连接
- 先启动的实例退出后，后启动的实例自动接管，适合升级/重启交替
- 仅 Windows 具备上述“多监听待命”语义；Linux 的 `SO_REUSEADDR` 只解决 TIME_WAIT，不支持两个活动监听
- 当 `WebBind` 配置为 `*` 或局域网 IP 时不启用地址重用，避免本地进程抢占对外面板端口
- 旧版本 StarAgent 可能仍以独占方式绑定端口，此时新实例会被系统拒绝（`WSAEACCES`/`WSAEADDRINUSE`）；需要停止旧实例或升级后重启
- 端口被占用时的单行告警由 `NewLife.Agent` 的 `ServiceBase.StartWork` 提供，需使用包含该修改的包版本；旧包仍会打印异常堆栈

---

## 7. 多语言原生 Agent

标准 StarAgent 依赖 .NET 运行时。为覆盖无 .NET 环境、嵌入式/OpenWrt、极小镜像和跨语言运维场景，仓库新增 `StarAgent.Native/`，以 Go / Rust / C++ 实现轻量 Agent，直接对接 StarServer 的 `Node/*` 协议。

### 7.1 目录与文档

| 路径 | 说明 |
|------|------|
| `StarAgent.Native/spec/protocol.md` | 节点侧协议规范（唯一事实源） |
| `StarAgent.Native/spec/schemas/` | JSON Schema |
| `StarAgent.Native/spec/vectors/` | 请求/响应样例 |
| `StarAgent.Native/conformance/` | 假 StarServer + 语言无关一致性测试 |
| `StarAgent.Native/go/` | Go 参考实现 |
| `StarAgent.Native/rust/` | Rust 实现 |
| `StarAgent.Native/cpp/` | C++ 实现（覆盖 Linux/Windows/macOS/OpenWrt） |

### 7.2 首版能力与裁剪

当前状态：Go / Rust / C++ 三种实现均通过统一一致性测试（各 6/6），且均通过真实 StarServer 登录、自动注册与在线状态验证。

首版原生 Agent 覆盖：

- 登录/注销/心跳/令牌续期/服务器迁移/指数退避重连
- WebSocket 命令通道 + HTTP 心跳命令降级
- 事件上报、命令回复
- CPU/内存/磁盘/网络/进程/运行时长/负载等基础指标
- `bash`/`cmd`/`node/synctime`/`node/setchannel`/`node/restart`/`node/bootstrap`
- 只提供前台进程（`run`/`once`/`bootstrap`/`version`），**不注册系统服务**；systemd / procd / launchd / 计划任务由部署侧提供模板（`StarAgent.Native/packaging/install/`）
- 引导安装：登录后调 `POST /node/bootstrap` 取包，安装 .NET 运行时与完整版 StarAgent（CLI `bootstrap` 或 `Bootstrap=true` 时由 `node/bootstrap` 命令触发）
- 系统/硬件信息（Windows 已对齐）：OS 名称版本与种类、全部 IPv4、网关+MAC、DNS、时区、DPI、分辨率、C 运行时、CPU/内存/磁盘、产品名/制造商/处理器/GPU、UUID/MachineGuid/序列号/主板/磁盘序列号/物理网卡 MAC；Linux/macOS 先覆盖基础字段，产品名/GPU 等按平台能力逐步补齐

原生 Agent 的 `Runtime` / `Framework` 上报自身语言运行时，不枚举本机 .NET Framework 集合；这是与标准 .NET Agent 的有意差异。

首版**不包含**：系统服务安装卸载与启停、进程守护、远程发布（`deploy/*`）、自身升级、Web 管理面板、插件、防火墙、DNS、截屏。`deploy/*`、`framework/install`、`node/reboot` 统一回复“不支持”。

### 7.3 安全默认

- `AllowExec=false`：`bash`/`cmd` 一律拒绝。
- `Bootstrap=false`：`node/bootstrap` 回复取消（引导安装未启用），避免平台一次性推错节点就改动系统安装。
- 显式开启后仍受 `AllowedCommands` 白名单、命令超时（默认 30s、上限 300s）、输出截断（64KB）约束。
- 生产环境必须校验 TLS 证书；日志不输出 Secret/Token。
- 遵循 [SVR-8-远程命令执行安全](SVR-8-远程命令执行安全.md)。

### 7.4 与移动端的关系

Android/iOS 的 .NET 移动代理（`StarAgent.Mobile` + `StarAgent.Android` + `StarAgent.iOS`）继续作为移动端首选实现：Android 使用前台服务，iOS 受系统限制为间歇在线。原生 Go/Rust/C++ Agent 首版聚焦服务器、桌面与嵌入式 Linux，不替代移动端 .NET 代理。

---

## 8. 资源占用与内存优化

.NET 代理常驻内存由三部分组成：运行时与程序集文件页（可回收的干净页）、JIT 生成代码、以及原生分配器堆。其中原生堆在 Linux 上长期偏大，原因是 glibc 为每个线程准备独立竞技场（arena），并在竞技场内缓存空闲块不归还；代理常驻二十多个线程，竞技场本身就能占掉一成以上内存。

代理启动时按以下顺序收紧（`Program.TuneNativeAllocator`）：

| 手段 | 作用 | 说明 |
|------|------|------|
| `MALLOC_ARENA_MAX=1` | 单竞技场 | 竞技场数量只在进程初始化阶段由环境变量决定，运行时 `mallopt` 改不动，因此先 `setenv` 再 `execv` 原地重载自身镜像（PID 不变，systemd 无感知），新镜像立即 `unsetenv` 避免被托管的业务进程继承 |
| `MALLOC_TRIM_THRESHOLD_` / `MALLOC_MMAP_THRESHOLD_` = 64KB，`MALLOC_TOP_PAD_` = 0 | 空闲堆及时归还 | 默认阈值偏大，空闲块长期留在进程里 |
| `System.Runtime.TieredCompilation=false`（runtimeconfig） | 只保留一份全优化代码 | 关闭分层编译后不再同时持有 tier0 与 tier1 两份代码，省下 JIT 代码页 |
| 关闭 HTTP/3（`System.Net.SocketsHttpHandler.Http3Support=false`） | 少装一套协议栈 | 代理只走 HTTP/1.1；不关闭时 .NET 探测 HTTP/3 会连带装载 `System.Net.Quic`（arm64 实测约 0.5~1.4 MB） |
| `System.Runtime.EnableWriteXorExecute=false`（runtimeconfig） | 去掉代码的双重映射 | 默认用 memfd 给 JIT/预编译代码建读写别名，实测该项独占 **26 MB 共享内存**（`Pss_Shmem`，属 tmpfs，内核不能像干净文件页那样回收）；关掉后代码走单映射，RSS 再降 1~2 MB，`Pss_Shmem` 归零 |

重载镜像只在常驻模式（`-s`/`-run`/`-restart`/`-delay`/`-upgrade`/`-repair` 或交互菜单）执行，`install`/`uninstall` 等一次性命令不额外付进程启动开销。

### 8.1 实测数据

局域网真机（2026-11，StarAgent 3.7.2026.1107，框架依赖部署，面板开启）：

| 设备 | 硬件 | 总内存 | 优化前 RSS | 优化后 RSS | 降幅 |
|------|------|--------|-----------|-----------|------|
| A6 | armv7 4 核 | 486 MB | 140 MB | 83 MB | -41% |
| A4 | arm64 2 核 | 481 MB | 173 MB | 105 MB | -39% |
| A2 | arm64 4 核 | 949 MB | 150 MB | 102 MB | -32% |

除 RSS 外，关闭 W^X 双映射还回收了 16~27 MB 共享内存（`Pss_Shmem` 归零）。

> ⚠️ **换版测试与运维注意**：带 SmartIPC 固件的设备（A2/A4/A6 均属此类）由 `newlifed` 看门狗守护代理，代理停止运行十几秒就会用固件内置的救援副本（`/usr/share/newlife/rescue/staragent`）覆盖部署目录后重启。换版时必须**同时刷新该救援副本**，否则设备会在无人察觉时回滚到固件随附的老版本（内存优化全部失效，实测 A6 就曾这样悄悄退回 116 MB）。救援事件由 `MyStarClient.CheckRescueFlag` 上报，平台节点事件里能看到“代理恢复成功……已还原 N 个核心文件”。

空闲 CPU 约单核 0.5%（4 核板，逐线程采样），常驻工作只有 60 秒心跳、10 分钟升级检查与 10 秒级服务巡检。连续 15 分钟采样 RSS 在 83~85 MB 间随 GC 周期波动，**无持续增长**。

### 8.2 剩余构成与优化边界

优化后 armv7 设备（A6，83 MB）的构成，其中约 59 MB 是可被内核回收的干净文件页（内存紧张时自动让位），真正独占的脏页只有 25 MB：

| 组成 | 约占用 | 能否再降 |
|------|--------|----------|
| 运行时本体（CoreLib 13.2 + coreclr 3.9 + clrjit 1.6 + JIT 代码 8.1） | 27 MB | 否，任何 .NET 进程的地板 |
| 星尘客户端栈（Http/TLS/Json/Remoting/追踪） | 11 MB | 否，心跳与命令通道必需 |
| ICU（`libicudata`/`icui18n`/`icuuc` + 内部缓存） | 12.6 MB | 仅能通过关闭 ICU 换取，见下 |
| BCL 其余（Xml/Expressions/Cryptography/Regex/Linq/压缩） | 9 MB | 仅能改配置格式等大改换取 |
| 自研程序集（NewLife.* + Stardust + StarAgent） | 5 MB | 否 |
| 托管堆 + 原生堆 + 线程栈 | 14 MB | 已接近下限（GC 参数实测无收益） |

至此 .NET 代理已接近地板，再要显著下降只有三条路：

- **关闭 ICU**：设 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 实测降到 72 MB（-12.6 MB），代价是界面与日志只剩英文，只适合不需要中文界面的极简设备。
- **换原生代理**：`StarAgent.Native` 的 C++ 版实测 7 MB，差一个数量级，但缺进程守护/远程发布/Web 面板/插件/自升级。
- **NativeAOT**：理论可到 20~30 MB，但需各平台交叉 AOT 工具链并适配反射密集的配置与序列化代码，属大型改造。

已试过但**未采用**的选项：

- `PublishReadyToRun`：程序集体积涨 2~3 倍、升级包变大，实测内存基本持平（JIT 代码页减少的量被文件页抵消）。
- 关闭诊断端口（`DOTNET_EnableDiagnostics=0`）：内存几乎无变化，只少两个线程，不值得牺牲可诊断性。
- GC 参数（`GCHeapHardLimit` 16/32/48 MB、`GCgen0size` 1 MB、`gcConcurrent=0`）：托管堆本来就小，实测无收益甚至略升。
- 定时 `malloc_trim(0)`：启动 90 秒后调用一次，页面虽有归还但 WorkingSet 只从 83180K 变成 83300K（噪声级），原生堆碎片没有可回收量，不必加定时任务。
- `DOTNET_ReadyToRun=0`（关预编译镜像）：常驻内存确实更省（armv7 83→74 MB、arm64 109→103 MB），但 JIT 全量编译让启动时间从 15 秒涨到 **31 秒**，边缘设备上看重上线速度，不划算。

### 8.3 与原生 Agent 的差距

同一台 A4 上实测 C++ 原生代理（`StarAgent.Native`）常驻 **7 MB**、6 个线程，与 .NET 版差一个数量级。差距来自运行时自身——托管运行时即使只做心跳也要装载 CoreLib、JIT 与 ICU。

因此选型建议：**极小内存设备（<256 MB）或必须极致省内存的嵌入式场景**优先用原生 Agent；需要进程守护、远程发布、Web 面板、插件、自升级等完整能力时用 .NET 版。

### 8.4 上游可优化项（跨仓库，未实施）

用 `AppDomain.AssemblyLoad` 挂钩逐项确认了剩余程序集的装载来源，以下三项需要改别的仓库才能拿下，合计约 7 MB：

| 程序集 | 占用 | 装载来源 | 可行的改法 |
|--------|------|----------|-----------|
| `System.Linq.Expressions` | 2.1 MB | `NewLife.Reflection.DefaultReflect.RegisterAndCreate`，首次访问 `Config<T>.Current` 就触发 | NewLife.Core 的对象工厂改为 `Activator.CreateInstance` 或缓存委托，不用表达式树 |
| `System.Reflection.Metadata` + `System.Collections.Immutable` | 1.7 MB | 异常堆栈解析源码行号（`StackTraceSymbols`），首次带文件信息的 `StackTrace` 触发 | NewLife.Core 记录异常时不再请求文件行号信息 |
| `System.Private.Xml` | 3.1 MB | `XmlConfigProvider` 读取 `*.config` | 配置文件改用 JSON（`FileConfigProvider` 按扩展名选解析器），需迁移全部既有部署 |

---

> 详细子模块文档见：[NODE-9-指令下发](NODE-9-指令下发.md)、[NODE-15-StarAgent远程部署](NODE-15-StarAgent远程部署.md)、[NODE-14-防火墙管理](NODE-14-防火墙管理.md)
