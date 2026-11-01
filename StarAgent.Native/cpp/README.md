# StarAgent Native C++

StarAgent 的 C++17 原生实现，与 `spec/protocol.md`、Go/Rust 实现保持协议对齐。支持登录/注销、心跳、令牌续期、`NewServer` 迁移、指数退避重连、HTTP 命令回复、WebSocket 通知、指标采集、内置命令、事件补报。原生 Agent 只提供前台进程，**不注册系统服务**，进程守护由部署侧负责。

## 状态

- Windows（MSVC 19.51 / Visual Studio 2026，x64）：实际编译、CTest 与一致性测试通过。
- Linux ARM64 真机（Ubuntu 26.04，A2 `HTNICE GT685X` / A4-WiFi `Nuvoton MA35D1`）：依赖目标机 `g++`/`cmake`，用 `-DFETCHCONTENT_SOURCE_DIR_IXWEBSOCKET` 指向离线源码即可构建；登录、心跳、指标上报与一致性测试 6/6 通过。
- 一致性测试：`conformance --agent staragent.exe --scenario all` → **6 passed / 0 failed**（basic、http-fallback、dedup、expired、reconnect、events）。
- 单元测试：`ctest -C Release` → **2/2**（`staragent_tests` 70/70 checks、`staragent --version`）。
- 真实服务端：在真实 Kestrel StarServer 上完成登录、自动注册、Code/Secret 持久化与在线状态验证（`NativeAgentIntegrationTests`，`ProductCode=StarAgent-Cpp`）。
- 小内存设备构建：`cmake --build build --target staragent --parallel 1`；`--parallel 2` 在 512MB~1GB 的板子上会被 OOM Killer 终止。

## 目录

```text
cpp/
├── CMakeLists.txt
├── README.md
├── src/
│   ├── main.cpp                CLI：run/once/version
│   ├── agent.{hpp,cpp}         登录、心跳、迁移、通知、命令调度、事件补报
│   ├── config.{hpp,cpp}        staragent.json、STARAGENT_* 环境变量、原子持久化
│   ├── protocol.{hpp,cpp}      PascalCase JSON 模型、时间格式、响应解包
│   ├── http_client.{hpp,cpp}   /node/* HTTP 客户端（cpp-httplib）
│   ├── ws_client.{hpp,cpp}     /node/notify WebSocket（IXWebSocket）
│   ├── executor.{hpp,cpp}      命令去重/过期/延迟/并发/超时/白名单
│   ├── metrics.{hpp,cpp}       CPU/内存/磁盘/网络/进程/TCP 连接/温度/磁盘 IO/负载指标
│   ├── events.{hpp,cpp}        有界事件队列与落盘
│   ├── logging.{hpp,cpp}       按天轮转日志（保留 7 天）
│   ├── platform.hpp            平台抽象
│   ├── platform_windows.cpp    PDH / GlobalMemoryStatusEx / GetAdaptersAddresses / SMBIOS
│   ├── platform_linux.cpp      /proc /sys /statvfs / getifaddrs
│   ├── platform_macos.cpp      sysctl / host_statistics / vm_stat / getifaddrs
│   └── output_buffer.hpp       64KB 头尾保留缓冲
├── tests/tests.cpp             协议、配置、事件队列、命令执行器单元测试
└── third_party/
    ├── httplib.h               cpp-httplib v0.61.0（MIT）
    ├── json.hpp                nlohmann/json v3.12.0（MIT）
    └── licenses/               两份依赖许可证原文
```

## 构建

### 依赖

- CMake ≥ 3.20，C++17 编译器（MSVC 2022/2026、GCC 11+、AppleClang 13+）。
- 首次 configure 需要网络：`FetchContent` 从 GitHub 拉取 **IXWebSocket v12.1.0**（BSD-3-Clause）。
- 可选：OpenSSL 开发包（启用 HTTPS/WSS，见下文）。
- 已内置单头文件依赖：cpp-httplib、nlohmann/json，无需额外下载。

### Windows（已验证）

```powershell
# Visual Studio 生成器
& 'C:\vs2026\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' `
  -S StarAgent.Native\cpp -B StarAgent.Native\cpp\build `
  -G 'Visual Studio 18 2026' -A x64

& 'C:\vs2026\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' `
  --build StarAgent.Native\cpp\build --config Release --parallel

# 测试
& 'C:\vs2026\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe' `
  --test-dir StarAgent.Native\cpp\build -C Release --output-on-failure
```

也可以使用 Ninja（需先 `call vcvars64.bat`）：

```bat
call C:\vs2026\VC\Auxiliary\Build\vcvars64.bat
cmake -S StarAgent.Native\cpp -B StarAgent.Native\cpp\build -G Ninja
cmake --build StarAgent.Native\cpp\build
```

产物：`StarAgent.Native/cpp/build/Release/staragent.exe`。

### Linux / OpenWrt / macOS

```bash
cmake -S StarAgent.Native/cpp -B StarAgent.Native/cpp/build -DCMAKE_BUILD_TYPE=Release
cmake --build StarAgent.Native/cpp/build --parallel
ctest --test-dir StarAgent.Native/cpp/build --output-on-failure
```

OpenWrt 交叉编译时传入目标工具链文件（`-DCMAKE_TOOLCHAIN_FILE=...`）；目标系统检测 `/etc/rc.common` + `/sbin/procd` 的 procd 脚本模板见 [`../packaging/install/staragent.init`](../packaging/install/staragent.init)。

### 离线 / 不启用 WebSocket

```bash
# 已有 IXWebSocket 源码时避免联网
cmake -S ... -DFETCHCONTENT_SOURCE_DIR_IXWEBSOCKET=/path/to/IXWebSocket

# 完全不启用 WS：命令改由心跳响应 Commands 下发（HTTP 降级）
cmake -S ... -DSTARAGENT_WITH_WS=OFF
```

## 依赖与许可证

| 依赖 | 版本 | 许可证 | 引入方式 |
|------|------|--------|----------|
| [cpp-httplib](https://github.com/yhirose/cpp-httplib) | v0.61.0 | MIT | `third_party/httplib.h`，许可证 `third_party/licenses/cpp-httplib.LICENSE` |
| [nlohmann/json](https://github.com/nlohmann/json) | v3.12.0 | MIT | `third_party/json.hpp`，许可证 `third_party/licenses/nlohmann-json.LICENSE` |
| [IXWebSocket](https://github.com/machinezone/IXWebSocket) | v12.1.0 | BSD-3-Clause | CMake `FetchContent`，源码与许可证位于构建目录 `_deps/ixwebsocket-src` |
| OpenSSL（可选） | 系统版本 | Apache-2.0 | 系统包管理器 / 工具链提供 |

## HTTPS / WSS 与 OpenSSL

CMake 使用 `find_package(OpenSSL QUIET)`：

- **找到 OpenSSL**：自动设置 `STARAGENT_WITH_TLS=ON`，同时为 cpp-httplib 定义 `CPPHTTPLIB_OPENSSL_SUPPORT`，并为 IXWebSocket 设置 `USE_TLS/USE_OPEN_SSL`，支持 `https://` 与 `wss://`。
- **未找到 OpenSSL**：保持 `STARAGENT_WITH_TLS=OFF`，configure 输出明确警告；此构建**仅支持 `http://` / `ws://`**。
- 生产环境必须使用 HTTPS/WSS。请安装 OpenSSL 开发包（如 `libssl-dev`、`openssl-devel`、vcpkg `openssl`）后重新 configure；也可显式指定 `-DSTARAGENT_WITH_TLS=ON`（缺少 OpenSSL 时会直接报错）或 `-DSTARAGENT_WITH_TLS=OFF`。
- `TlsVerify=false` 仅用于测试；默认 `true`，会校验证书。

本机（Windows）未安装 OpenSSL，因此已验证的构建为 **http/ws**；一致性测试的假服务器也使用 http。

## 配置

默认读取 `staragent.json`（可用 `--config` 或 `STARAGENT_CONFIG` / `STARAGENT_CONFIG_FILE` 覆盖）：

```json
{
  "Server": "https://star.example.com:6600",
  "ProductCode": "StarAgent-Cpp",
  "Code": "",
  "Secret": "",
  "Project": "",
  "Channel": "Release",
  "DataDir": "/var/lib/staragent",
  "LogLevel": "info",
  "MetricsInterval": 60,
  "AllowExec": false,
  "AllowedCommands": [],
  "CommandTimeout": 30,
  "TlsVerify": true,
  "LocalPort": 5500,
  "AutoUpgrade": true
}
```

关键校验：

- `Server` 必填，scheme 仅 `http`/`https`。
- `MetricsInterval` 5–3600 秒；`CommandTimeout` 1–300 秒。
- `Code`/`Secret` 由登录响应写入，原子落盘并限制为 `0600`（Windows 上为 CRT 只读位 + 数据目录所有者隔离，见“已知限制”）。
- `UpgradeVersion` 由升级流程成功后自动写入（无需手工配置），用于记录已应用的版本，避免服务端仍推送同一版本时反复替换重启。

### 环境变量

`STARAGENT_` 前缀覆盖配置；同时兼容下划线别名（例如一致性 runner 注入的变量）：

| 配置 | 变量 |
|------|------|
| Server | `STARAGENT_SERVER`、`STARAGENT_SERVER_URL` |
| ProductCode | `STARAGENT_PRODUCTCODE`、`STARAGENT_PRODUCT_CODE` |
| DataDir | `STARAGENT_DATADIR`、`STARAGENT_DATA_DIR` |
| LogLevel | `STARAGENT_LOGLEVEL`、`STARAGENT_LOG_LEVEL` |
| MetricsInterval | `STARAGENT_METRICSINTERVAL`、`STARAGENT_METRICS_INTERVAL` |
| AllowExec | `STARAGENT_ALLOWEXEC`、`STARAGENT_ALLOW_EXEC` |
| AllowedCommands | `STARAGENT_ALLOWEDCOMMANDS`、`STARAGENT_ALLOWED_COMMANDS` |
| CommandTimeout | `STARAGENT_COMMANDTIMEOUT`、`STARAGENT_COMMAND_TIMEOUT` |
| TlsVerify | `STARAGENT_TLSVERIFY`、`STARAGENT_TLS_VERIFY` |
| LocalPort | `STARAGENT_LOCALPORT`、`STARAGENT_LOCAL_PORT` |
| AutoUpgrade | `STARAGENT_AUTOUPGRADE`、`STARAGENT_AUTO_UPGRADE` |
| Code / Secret / Project / Channel | `STARAGENT_CODE` / `STARAGENT_SECRET` / `STARAGENT_PROJECT` / `STARAGENT_CHANNEL` |

### CLI

```text
staragent run        [--config path] [--server url] [--data dir] [--log-level level] [--allow-exec] [--console]
staragent once       [--config path] [--console]
staragent bootstrap  [--config path] [--console]
staragent version
```

`run` 为前台常驻长连接模式；`once` 登录、心跳并处理本次心跳中的命令后退出，适合自检/定时任务；
`bootstrap` 登录后向服务端取包，安装 .NET 运行时与完整版 StarAgent（需 root/管理员）。

## 持久化与日志

- `DataDir/events.json`：有界事件队列（1000 条），失败重试期间保留，重启后恢复。
- `DataDir/logs/staragent-YYYY-MM-DD.log`：按天轮转，保留 7 天；`--console` 同时写 stderr。
- 日志不会输出 `Secret`/`Token`；HTTP 错误体会脱敏后再记录。

## 命令与安全默认

| 命令 | 平台 | 说明 |
|------|------|------|
| `bash` | Linux/macOS | 字符串参数或 `{"cmd":"...","timeout":30000}`（毫秒） |
| `cmd` | Windows | 同上，通过 `cmd.exe /d /s /c` 执行 |
| `node/synctime` | 全平台 | 可选时间字符串；无参数使用服务端时间 |
| `node/setchannel` | 全平台 | 修改并持久化更新通道 |
| `node/restart` | 全平台 | 1 秒后重启自身进程 |
| `node/reboot` / `node/shutdown` | 全平台 | 1.5 秒后重启/关机（Linux/macOS 调 `reboot`/`poweroff`，Windows 用 `shutdown.exe` 延迟 30 秒） |
| `node/upgrade` | 全平台 | 立即检查升级，有新版时走自动升级流程 |
| `deploy/*` / `framework/install` | — | 回复“不支持命令”（原生 Agent 不做远程发布与 .NET 运行时安装） |

安全与限制：

- `AllowExec=false` 时 `bash`/`cmd` 一律回复 `Status=3` / `命令执行未启用`。
- 开启后仍受 `AllowedCommands` 白名单（精确匹配或 `前缀*`）约束；参数上限 8KB。
- 输出合并 stdout/stderr，超 64KB 保留头尾并插入截断标记。
- 默认超时 30 秒，可由参数覆盖，硬上限 300 秒；超时终止**整个进程树**（Windows Job Object / POSIX 进程组）。
- 最多 4 个命令并发；shell 命令串行执行。
- 命令按 `Id` 去重 1 小时；`Expire` 过期回复 `Status=3`；`StartTime` 未来时先回复 `Status=1` 再延迟执行。
- 所有回复（过期、延迟、取消、成功、失败）都携带原始 `Id`。

## 本地接口与局域网发现

`LocalPort`（默认 5500，`0` 关闭）上监听 UDP，线格式与 .NET StarAgent 完全一致：
**SRMP 帧**（1 Flag + 1 sequence + 2 length + payload）+ **JsonEncoder 载荷**（`[7位长度][action][4字节长度][JSON]`）。

- `Info`：返回 `AgentInfo`（`ProcessId`/`ProcessName`/`Version`/`FileName`/`Arguments`/`IP`/`Server`/`Code`/`Services`），供本机应用探测与**局域网广播发现**（`LocalStarClient.Scan()` 向 `255.255.255.255:5500` 广播 `Info` 帧）使用。
- `Ping`：返回 `{"ServerTime": <毫秒时间戳>}`。
- 其他 action 回复错误帧（code=404）。

验证（Windows PowerShell，发一帧 `Info`）：

```powershell
$a=[Text.Encoding]::UTF8.GetBytes('Info'); $p=[byte[]](@(4)+$a)
$f=[byte[]](@(0x01,0x01,[byte]($p.Length -band 0xFF),[byte](($p.Length -shr 8) -band 0xFF)))+$p
$u=New-Object Net.Sockets.UdpClient; $u.Client.ReceiveTimeout=3000
$u.Send($f,$f.Length,(New-Object Net.IPEndPoint([Net.IPAddress]::Loopback,5500))) | Out-Null
[Text.Encoding]::UTF8.GetString($u.Receive([ref](New-Object Net.IPEndPoint([Net.IPAddress]::Any,0))))
```

> 同一台机器上已有 .NET StarAgent 占用 5500 时，原生 Agent 会记录“bind failed”并跳过本地接口（不影响心跳与采集）。

## 以守护进程方式运行

原生 Agent 只提供前台进程（`staragent run`），不注册系统服务；重启策略、开机自启、日志重定向由部署侧托管。模板见 [`../packaging/install/`](../packaging/install/)。

### Linux（systemd）

```ini
[Unit]
Description=StarAgent Native
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/opt/staragent/staragent run --config /etc/staragent/staragent.json
Restart=always
RestartSec=5
KillSignal=SIGTERM

[Install]
WantedBy=multi-user.target
```

写入 `/etc/systemd/system/staragent.service` 后 `systemctl daemon-reload && systemctl enable --now staragent`。

### OpenWrt（procd）

检测到 `/etc/rc.common` 与 `/sbin/procd` 的环境使用 procd 脚本（`USE_PROCD=1`，`procd_set_param respawn`）。

### Windows（任务计划程序）

原生 Agent 不注册为 Windows 服务，使用任务计划程序在开机时以 SYSTEM 身份启动：

```powershell
schtasks /Create /TN StarAgentNative /TR "C:\Program Files\StarAgent\staragent.exe run --config C:\ProgramData\StarAgent\staragent.json" /SC ONSTART /RU SYSTEM /RL HIGHEST /F
```

### macOS（launchd）

写入 `/Library/LaunchDaemons/com.newlifex.staragent.plist`（root，`KeepAlive=true`）后 `launchctl load -w`。

## 能力边界与已知限制

- **自动升级**：`/node/upgrade` 返回更高版本时，下载 `Source` 到 `DataDir/Update/`，按 `FileHash`（MD5）校验后把当前可执行文件改名为 `*.old`、写入新文件并重启；`AutoUpgrade=false` 时只记录事件。**仅支持单文件原生包**（裸二进制）；`.zip` 包回复“需人工升级”。下载或校验失败不再每次心跳重试，而是退避 10 分钟后再试；成功应用后把版本写入 `UpgradeVersion`，同一版本不会重复替换。
- **deploy / framework / reboot 未实现**：按协议回复“不支持命令”。
- **WebSocket 可选**：`-DSTARAGENT_WITH_WS=OFF` 时仍可通过心跳 `Commands` 执行命令；但 `--scenario reconnect/dedup` 等 WS 场景不可用。
- **TLS 依赖 OpenSSL**：未检测到 OpenSSL 的构建只能 `http/ws`；生产必须安装 OpenSSL 并重新构建。
- **Windows 权限**：`0600` 通过 CRT `_chmod` 与数据目录隔离近似实现，无法像 POSIX 一样精确设置 ACL；建议将 `DataDir` 放在受控目录。
- **控制台中文输出**：Windows 控制台按本地代码页（简体中文为 936/GBK）解码收到的字节，程序内部字符串是 UTF-8，直写会显示乱码。启动时把 `stdout`/`stderr` 换成专用缓冲区：输出到控制台时转宽字符走 `WriteConsoleW`（不受代码页影响，也不需要用户 `chcp 65001`），重定向到文件或管道时保持 UTF-8 字节。
- **node/restart**：以原命令行拉起新进程（Windows 使用 `DETACHED_PROCESS`，POSIX 使用 `setsid`），随后当前进程退出；守护进程由部署侧（systemd/procd/计划任务/launchd）重新接管。
- **Linux/macOS 验证**：Linux ARM64 已在 A2/A4 真机验证（构建、登录、心跳、指标、命令往返、conformance），CI `native-agents.yml` 的 `cpp-linux` 任务在 ubuntu 上做构建+单测+一致性测试；macOS 未做真机验证，建议补充 CI 构建任务。
- **指标降级**：任何单项采集失败只把该字段填 0，不跳过心跳；无电池/无传感器的设备 `Battery`、`Temperature`、`Signal` 为 0。
- **平台采集差异**：
  - 温度：Linux 读 `/sys/class/thermal`/`hwmon`；Windows 读 PDH 计数器 `Thermal Zone Information(*)\Temperature`（开尔文换算摄氏度，无热区为 0）；macOS 为 0。
  - 电量：Windows 用 `GetSystemPowerStatus`，Linux 读 `/sys/class/power_supply/BAT*/capacity`；macOS 为 0。
  - 无线信号：仅 Linux（`/proc/net/wireless`，兼容 70/100 两种满值）；Windows/macOS 为 0。
  - 磁盘 IO：Linux 用 `/proc/diskstats`，Windows 用 PDH `PhysicalDisk(_Total)`；macOS 为 0。
  - `MaxOpenFiles`：仅 Linux（`getrlimit`）。
- **网络质量**：后台线程每 48 秒对网关、DNS、服务端各做 5 次 ICMP 探测（Windows 用 `IcmpSendEcho`，Linux/macOS 调 `ping`），心跳上报 `GatewayLatency`/`DnsLatency`/`ServerLatency`、丢包率与 `IntranetScore`/`InternetScore`（评分算法与 .NET `PingMonitor` 一致：`成功率 × exp(-0.001×(延迟-1))`）；本地回环 DNS 改用 `223.5.5.5`。
- **Linux 板级标识**：`Vendor`/`Product`/`SerialNumber`/`Board` 优先取 `/etc/os-release` 中设备端写入的字段（与 .NET `MachineInfo` 约定一致，如 `Board=A2-2026-1G`），缺失时回落 DMI 与设备树；`UUID` 优先 `/etc/uuid`。
- **无 Android/iOS 实现**；移动端见仓库其他原生目录。
- **状态文件**：`Code`/`Secret` 写入 `staragent.json`；`events.json` 与日志位于 `DataDir`，不会被提交到仓库。

## 一致性测试

```powershell
cd StarAgent.Native\conformance
go build -o conformance.exe ./cmd/conformance
.\conformance.exe --agent ..\cpp\build\Release\staragent.exe --scenario all
```

本次结果：

```text
[PASS] basic (2.6s)
[PASS] http-fallback (1.7s)
[PASS] dedup (2.1s)
[PASS] expired (600ms)
[PASS] reconnect (2.9s)
[PASS] events (3.6s)
6 passed, 0 failed in 13.3s
```
