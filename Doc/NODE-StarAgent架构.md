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
- 事件上报：心跳成功后取走 `Data/NodeEvents.csv` 台账分批上报，成功即删除；上报失败的事件写回台账，等下一次心跳重试

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

### 2.6 升级失败保护

新版文件覆盖到位后先做冒烟测试（拉起新版，3 秒内不退出视为成功）。冒烟失败只有一种处理：**回滚 `.del` 备份、当前进程继续运行**。不再通过重启服务“再试一次”——服务重启会结束当前进程，而新版已被冒烟测试证明跑不起来，结果就是节点彻底失联，且再没有机会执行回滚。

- **提前预检**：`UpgradePreflight` 读取覆盖后的 `StarAgent.runtimeconfig.json`，与本机 `shared/Microsoft.NETCore.App` 已安装版本比对；命中缺失就不再等第二次冒烟，直接按失败处理。比对遵循 .NET 主机的真实前滚规则（`Disable` 要求精确版本；`Major` / `LatestMajor` 允许跨主版本；默认 `Minor` 等策略只允许同主版本内向前，且补丁号不得低于要求；环境变量 `DOTNET_ROLL_FORWARD` / `DOTNET_ROLL_FORWARD_TO_PRERELEASE` 优先于 runtimeconfig），因此「net8 节点上新包要求 net9」判为缺失，而「要求 8.0.5、已装 8.0.33」判为可用。
- **预检只是提前告警**：它不参与“升级失败后做什么”的决策，最终以冒烟测试的实际拉起结果为准；预检漏判时照样回滚，不会去赌重启服务。

### 2.7 自动升级的进程接力（H → U → H2）

升级全程由三个角色接力完成，理解它们的分工就理解了整套机制：

| 角色 | 是什么 | 负责什么 |
|------|--------|----------|
| **H** | 正在当班的老代理（系统服务的主进程） | 下载解压覆盖、把旧文件改名 `.del` 备份、拉起 U 做“试车”、失败时回滚 |
| **U** | H 拉起的升级进程（`-restart -upgrade`） | 睡 3 秒后执行 `systemctl restart StarAgent`，然后自己退出——“交接钥匙的人” |
| **H2** | 系统服务管理器按 `ExecStart` 启动的新代理 | 新版正式当班 |

四种结局，一一对应：

1. **U 根本没起来**（缺 .NET、缺依赖库、文件不全）：H 在 3 秒内就看到进程退出，等 5 秒再试一次；仍失败就还原 `.del`，H 继续当班，历史里留一条“升级失败，已回滚”。
2. **U 起来了**：U 睡 3 秒后叫系统重启服务，系统结束 H、启动 H2，U 退出。这是正常升级。
3. **U 起来了但没交接成功**：H 仍然在岗（H 从不主动退出），下一轮检查更新再来一次。
4. **新版启动后很快崩溃**：交给系统服务的自动重启策略（`Restart=always`）兜底，拉起仍是同一份新版；此时应该发布修复包或回退发布包，而不是让代理自己“赌”。

**铁律：H 永远不主动重启自己所在的服务。**

因为“重启服务”这个动作的含义是“先杀掉 H，再用磁盘上的文件启动”。如果磁盘上已经铺着坏掉的新版，H 被杀之后既回不来、也没人执行回滚——2026-11-09 的 LS-GD 事故正是如此：本地日志 `Fallback: restart service` → `systemd.Restart` → `ProcessExit`，新版因缺 .NET 9 以退出码 150 反复失败，节点失联两天。

**冒烟测试证明什么、不证明什么**：3 秒只能证明“新版的宿主能加载、进程能活过 3 秒”，不能证明长期稳定；它专门拦截“根本起不来”这一类问题（本次事故即属此类）。

**预检（runtimeconfig）的定位**：相当于升级前“看一眼说明书”，提前算出新版要求的 .NET 运行时本机有没有，用于写清失败原因、少等一次 5 秒重试；判断不了就不拦，最终以试车的实际结果为准。

> 历史包袱：为解决旧版“用进程名拼出 `StarAgent.exe.del.exe`”导致 U 永远拉不起来的问题，曾加过“两次失败就把重启交给服务管理器”的兜底。该问题的根因已由“改用入口程序集名”修好，兜底在坏包场景下只会把 H 一起带走，现已删除。

### 2.8 升级下载与完成确认

升级包下载超时按两段控制：响应头 15 秒内未到达视为服务器不可达并快速失败，收到响应头后响应体允许 300 秒（NewLife.Remoting `Upgrade.CreateDownloadOptions` 的默认值）。下载失败最多重试 2 次，每次重建 HttpClient 强制新连接——平台 AB 双机部署时附件同步存在延迟，首次请求可能命中尚未同步完成的节点，换连接重试大概率落到另一台。批量升级由平台把命令执行时间排到未来（每节点递增 3 秒），接口立即返回，节点按各自时间点下载，避免几十个节点同时拉取同一个升级包挤爆文件服务出口带宽。

强制升级的完成确认由代理主动上报：文件覆盖成功且新进程拉起后，把 `Upgrade` 的 success 事件写入本地事件台账（`Data/NodeEvents.csv`），随下一次心跳上报，服务端收到后停止按 10min/30min/2h/6h/24h 退避重复下发。不能只依赖重启后上报的版本号——节点本来就在目标版本时版本号不变，服务端会误判升级未完成而反复重装。

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

> **运行环境**：.NET 版本在 Linux 上依赖 ICU（安装 `libicu`）。无 ICU 的老旧/嵌入式系统可设置 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 降级运行，此时只有英文界面与日志。界面语言优先跟随平台下发的默认语言（`Setting.DefaultLanguage`，随登录/心跳到达），浏览器声明语言优先于它；也可在面板右上角切换并写入 Core 全局配置 `NewLife.Setting.Language` 固定，详见 [多语言本地化](多语言本地化.md)。

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

> 每一项的原理、收益、代价与测量方法，见专题文档：[NODE-StarAgent内存优化](NODE-StarAgent内存优化.md)。本节只留结论。

.NET 代理常驻内存由三部分组成：运行时与程序集文件页（可回收的干净页）、JIT 生成代码、以及原生分配器堆。其中原生堆在 Linux 上长期偏大，原因是 glibc 为每个线程准备独立竞技场（arena），并在竞技场内缓存空闲块不归还；代理常驻二十多个线程，竞技场本身就能占掉一成以上内存。

代理启动时按以下顺序收紧（`Program.TuneRuntime`）：

| 手段 | 作用 | 说明 |
|------|------|------|
| `MALLOC_ARENA_MAX=1` | 单竞技场 | 竞技场数量只在进程初始化阶段由环境变量决定，运行时 `mallopt` 改不动，因此先 `setenv` 再 `execv` 原地重载自身镜像（PID 不变，systemd 无感知），新镜像立即 `unsetenv` 避免被托管的业务进程继承 |
| `MALLOC_TRIM_THRESHOLD_` / `MALLOC_MMAP_THRESHOLD_` = 64KB，`MALLOC_TOP_PAD_` = 0 | 空闲堆及时归还 | 默认阈值偏大，空闲块长期留在进程里 |
| `DOTNET_ReadyToRun=0` | 不装载预编译镜像 | 放弃直接执行 R2R 代码，改为全量 JIT 一份本地代码，程序集文件页从 43 MB 降到 29 MB（CoreLib 13 MB → 5.4 MB）；代价是启动变慢约 2 倍（armv7 实测 15 秒 → 31 秒）。与 `execv` 重载合并进行，不额外多一次启动 |
| `System.Globalization.Invariant=true`（runtimeconfig，仅非 Windows 包） | 不装载 ICU | ICU 只提供区域性数据（日期格式、排序规则、`CultureInfo`），不参与文案翻译。关掉后 `libicudata`/`libicui18n`/`libicuuc` 与内部缓存共 8 MB 不再落地，日志回落英文、界面不受影响。写在 runtimeconfig 而不是环境变量：有些嵌入式系统根本没装 ICU，靠环境变量注入时进程可能在重载镜像前就先崩了 |
| `System.Runtime.TieredCompilation=false`（runtimeconfig） | 只保留一份全优化代码 | 关闭分层编译后不再同时持有 tier0 与 tier1 两份代码，省下 JIT 代码页 |
| 关闭 HTTP/3（`System.Net.SocketsHttpHandler.Http3Support=false`） | 少装一套协议栈 | 代理只走 HTTP/1.1；不关闭时 .NET 探测 HTTP/3 会连带装载 `System.Net.Quic`（arm64 实测约 0.5~1.4 MB） |
| `System.Runtime.EnableWriteXorExecute=false`（runtimeconfig） | 去掉代码的双重映射 | 默认用 memfd 给 JIT/预编译代码建读写别名，实测该项独占 **26 MB 共享内存**（`Pss_Shmem`，属 tmpfs，内核不能像干净文件页那样回收）；关掉后代码走单映射，RSS 再降 1~2 MB，`Pss_Shmem` 归零 |

重载镜像只在常驻模式（`-s`/`-run`/`-restart`/`-delay`/`-upgrade`/`-repair` 或交互菜单）执行，`install`/`uninstall` 等一次性命令不额外付进程启动开销。

### 8.1 实测数据

局域网真机（2026-11，StarAgent 3.7.2026.1108，框架依赖部署，面板开启）：

| 设备 | 架构 | 总内存 | 优化前 RSS | 优化后 RSS | 降幅 |
|------|------|--------|-----------|-----------|------|
| A6 | armv7 4 核 | 486 MB | 140 MB | 64 MB | -54% |
| A4 | arm64 2 核 | 481 MB | 173 MB | 90 MB | -48% |
| A2 | arm64 4 核 | 949 MB | 150 MB | 87 MB | -42% |
| 本机 PC | x64 16 线程（Windows 服务） | 32 GB | 47 MB | 47 MB | 持平 |

除 RSS 外，关闭 W^X 双映射还回收了 16~27 MB 共享内存（`Pss_Shmem` 归零）。

PC 与三台嵌入式设备的差异，来自优化手段本身全是 Linux/glibc 侧特性：竞技场是 glibc 的线程分配策略，`Pss_Shmem` 双映射在 Windows 上不走 tmpfs，关闭 R2R 省下的是文件页，对总内存 32 GB 的 PC 也谈不上收益。桌面与服务器场景按默认参数运行即可，省内存的开关只为嵌入式设备服务。表中 PC 一列只核对了改造前的 47 MB 基线（Windows 服务形态，25 线程），Linux 专属开关不会注入 Windows 进程，因此没有为它单独发版复测。

> ⚠️ **换版测试与运维注意**：带 SmartIPC 固件的设备（A2/A4/A6 均属此类）由 `newlifed` 星尘自愈巡检守护代理，默认每 15 分钟一轮（启动后约 2 分钟首巡）。救援副本是 `/usr/share/newlife/rescue/staragent-<ver>.tar.gz` + 同名 `.md5`（黄金代理，7 个核心文件；缺包/缺 md5/校验失败一律不用），归属固件/newlifed，**StarAgent 不写该目录**。
>
> **新版 newlifed（2026-11-08 起，含 e0fab1a/79b62f2 状态机修正与后续细化）的救援条件**：`ActiveState=failed` 或一个巡检周期内 `NRestarts` 增长 ≥3（重启循环）才恢复；`active`/`reloading`/`activating`/`deactivating` 视为正常；`inactive` 且曾成功运行过给 30 分钟人工维护宽限，首启从未启动过才直接恢复；`masked` 不介入。覆盖前检查 `MainPID`/`pgrep` 存活进程；`failed` 或宽限到期的 `inactive` 先 `reset-failed` + `start` 等 60 秒，仍失败才还原。恢复前会把被覆盖的核心文件备份到 `/root/agent.broken-<时间戳>`（只保留最近 2 份，含 `manifest.txt`；`Config/` 与现场插件不动），恢复动作限流 30 分钟；运行时连续两轮巡检异常才重装（同时校验 `Microsoft.NETCore.App` 与 `Microsoft.AspNetCore.App` 10.x）。结果追加到 `/root/agent/Data/NodeEvents.csv` 事件台账，由 StarAgent 作为普通节点事件上报（代理不感知事件来源，也不感知救援机制；台账锁为 `NodeEvents.lock`）。现场维护可用 `newlifed guard-hold [分钟]` 暂停自动恢复（`guard-release` 解除），`rescue-now` 强制忽略宽限/限流。A6 在旧版 newlifed 上曾因停止运行十几秒被救援、内存退回 116 MB；新版已按上述状态机修正（设备上需确认 `newlifed` 二进制已更新到该版本）。
>
> 换版脚本用 `systemctl restart` 收尾时，服务单元是 `KillMode=process` + `SIGINT`，老进程若正在忙（如升级替换中的文件写操作）可能没在超时内退出，留下一个父子关系已断的旧代理进程继续上报。换版后请核对 `systemctl show StarAgent -p MainPID --value` 与 `ps -eo pid,etimes,args | grep StarAgent.dll` 是否只有一个进程；多出来的按 PID 杀掉（实测 A6 曾在换版后同时跑着新旧两个代理，内存统计也因此偏高）。

空闲 CPU 约单核 0.5%（4 核板，逐线程采样），常驻工作只有 60 秒心跳、10 分钟升级检查与 10 秒级服务巡检。连续 15 分钟采样 RSS 在 63~66 MB 间随 GC 周期波动，**无持续增长**。

### 8.2 剩余构成与优化边界

优化后 armv7 设备（A6，63~65 MB）的构成，其中约 40 MB 是可被内核回收的干净文件页（内存紧张时自动让位），真正独占的脏页只有 24 MB：

| 组成 | 约占用 | 能否再降 |
|------|--------|----------|
| 运行时本体（CoreLib 5.4 + coreclr 3.8 + clrjit 1.6 + JIT 代码） | 22 MB | 否，任何 .NET 进程的地板；关闭 R2R 后 CoreLib 从 13.2 降到 5.4，多出来的 JIT 代码已计入 |
| 星尘客户端栈（Http/TLS/Json/Remoting/追踪） | 9 MB | 否，心跳与命令通道必需 |
| BCL 其余（Xml/Cryptography/libstdc++/libc） | 8 MB | 仅能改配置格式等大改换取 |
| 自研程序集（NewLife.* + Stardust + StarAgent） | 4 MB | 否 |
| 匿名页（托管堆 + 原生堆 + 线程栈 + JIT 代码） | 24 MB | 已接近下限（GC 参数实测无收益） |

ICU 已默认关闭（`libicudata`/`libicui18n`/`libicuuc` 均不再装载），这一项在 8.1 的降幅里占最后 8 MB。

至此 .NET 代理已接近地板，再要显著下降只有两条路：

- **换原生代理**：`StarAgent.Native` 的 C++ 版实测 7 MB，差一个数量级，但缺进程守护/远程发布/Web 面板/插件/自升级。
- **NativeAOT**：理论可到 20~30 MB，但需各平台交叉 AOT 工具链并适配反射密集的配置与序列化代码，属大型改造。

已试过但**未采用**的选项：

- `PublishReadyToRun`：程序集体积涨 2~3 倍、升级包变大，实测内存基本持平（JIT 代码页减少的量被文件页抵消）。
- 关闭诊断端口（`DOTNET_EnableDiagnostics=0`）：内存几乎无变化，只少两个线程，不值得牺牲可诊断性。
- GC 参数（`GCHeapHardLimit` 16/32/48 MB、`GCgen0size` 1 MB、`gcConcurrent=0`）：托管堆本来就小，实测无收益甚至略升。
- 定时 `malloc_trim(0)`：启动 90 秒后调用一次，页面虽有归还但 WorkingSet 只从 83180K 变成 83300K（噪声级），原生堆碎片没有可回收量，不必加定时任务。

### 8.3 启动时间与包体积的取舍

这套优化只拿启动时间换常驻内存，**不动发布包体积**：`DOTNET_ReadyToRun=0` 与关闭 ICU 都是运行时行为，升级包里还是同一批程序集；ICU 本就是系统共享库，从不随包分发。本次改动后 `StarAgent.dll` 从 1.38 MB 涨到 1.40 MB（多出的 19 KB 是语言判定与开关代码）。

启动时间是真实代价。systemd `Started` 到面板 `Http Ready` 的实测：A6（armv7 4 核）**28 秒**、A4（arm64 2 核）**33 秒**、A2（arm64 4 核）**14 秒**；改造前 armv7 只需 15 秒左右。慢的部分是一次性 JIT 全量编译，编译完成后运行的是优化代码，**稳态执行不会变慢**（分层编译下反而要等 tier1 重新编译）。

对边缘设备这笔交易划得来：常驻内存按天算，启动只在上电或升级时发生一次，而看门狗的重启等待上限是十几秒到几十秒量级，仍在可接受范围。若某个部署场景更看重秒级上线，把 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` 之外的两项关掉即可（ICU 只影响 8 MB，与启动时间无关，可以单独保留）。

### 8.4 与原生 Agent 的差距

同一台 A4 上实测 C++ 原生代理（`StarAgent.Native`）常驻 **7 MB**、6 个线程，与 .NET 版差一个数量级。差距来自运行时自身——托管运行时即使只做心跳也要装载 CoreLib 与 JIT。

因此选型建议：**极小内存设备（<256 MB）或必须极致省内存的嵌入式场景**优先用原生 Agent；需要进程守护、远程发布、Web 面板、插件、自升级等完整能力时用 .NET 版。

### 8.5 上游可优化项（跨仓库，未实施）

用 `AppDomain.AssemblyLoad` 挂钩逐项确认了剩余程序集的装载来源，以下三项需要改别的仓库才能拿下，合计约 7 MB：

| 程序集 | 占用 | 装载来源 | 可行的改法 |
|--------|------|----------|-----------|
| `System.Linq.Expressions` | 2.1 MB | `NewLife.Reflection.DefaultReflect.RegisterAndCreate`，首次访问 `Config<T>.Current` 就触发 | NewLife.Core 的对象工厂改为 `Activator.CreateInstance` 或缓存委托，不用表达式树 |
| `System.Reflection.Metadata` + `System.Collections.Immutable` | 1.7 MB | 异常堆栈解析源码行号（`StackTraceSymbols`），首次带文件信息的 `StackTrace` 触发 | NewLife.Core 记录异常时不再请求文件行号信息 |
| `System.Private.Xml` | 3.1 MB | `XmlConfigProvider` 读取 `*.config` | 配置文件改用 JSON（`FileConfigProvider` 按扩展名选解析器），需迁移全部既有部署 |

---

> 详细子模块文档见：[NODE-StarAgent内存优化](NODE-StarAgent内存优化.md)、[NODE-9-指令下发](NODE-9-指令下发.md)、[NODE-15-StarAgent远程部署](NODE-15-StarAgent远程部署.md)、[NODE-14-防火墙管理](NODE-14-防火墙管理.md)
