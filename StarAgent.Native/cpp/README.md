# StarAgent Native C++

StarAgent 的 C++17 原生实现，与 `spec/protocol.md`、Go/Rust 实现保持协议对齐。支持登录/注销、心跳、令牌续期、`NewServer` 迁移、指数退避重连、HTTP 命令回复、WebSocket 通知、指标采集、内置命令、事件补报与服务安装。

## 状态

- Windows（MSVC 19.51 / Visual Studio 2026，x64）：实际编译、CTest 与一致性测试通过。
- 一致性测试：`conformance --agent staragent.exe --scenario all` → **6 passed / 0 failed**（basic、http-fallback、dedup、expired、reconnect、events）。
- 单元测试：`ctest -C Release` → **2/2**（`staragent_tests` 50/50 checks、`staragent --version`）。
- 真实服务端：在真实 Kestrel StarServer 上完成登录、自动注册、Code/Secret 持久化与在线状态验证（`NativeAgentIntegrationTests`，`ProductCode=StarAgent-Cpp`）。
- Linux/macOS：源码路径完整（`platform_linux.cpp`、`platform_macos.cpp`、`service_posix.cpp`），本机未完成交叉编译验证，建议在对应平台或 CI 上执行 `cmake --build` + `ctest`。

## 目录

```text
cpp/
├── CMakeLists.txt
├── README.md
├── src/
│   ├── main.cpp                CLI：run/once/version/install/uninstall/start/stop/status
│   ├── agent.{hpp,cpp}         登录、心跳、迁移、通知、命令调度、事件补报
│   ├── config.{hpp,cpp}        staragent.json、STARAGENT_* 环境变量、原子持久化
│   ├── protocol.{hpp,cpp}      PascalCase JSON 模型、时间格式、响应解包
│   ├── http_client.{hpp,cpp}   /node/* HTTP 客户端（cpp-httplib）
│   ├── ws_client.{hpp,cpp}     /node/notify WebSocket（IXWebSocket）
│   ├── executor.{hpp,cpp}      命令去重/过期/延迟/并发/超时/白名单
│   ├── metrics.{hpp,cpp}       CPU/内存/磁盘/网络/进程/负载指标
│   ├── events.{hpp,cpp}        有界事件队列与落盘
│   ├── logging.{hpp,cpp}       按天轮转日志（保留 7 天）
│   ├── platform.hpp            平台抽象
│   ├── platform_windows.cpp    PDH / GlobalMemoryStatusEx / GetAdaptersAddresses / SMBIOS
│   ├── platform_linux.cpp      /proc /sys /statvfs / getifaddrs
│   ├── platform_macos.cpp      sysctl / host_statistics / vm_stat / getifaddrs
│   ├── service_windows.cpp     Windows Service（SCM）
│   ├── service_posix.cpp       systemd / OpenWrt procd / launchd
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

OpenWrt 交叉编译时传入目标工具链文件（`-DCMAKE_TOOLCHAIN_FILE=...`）；`service_posix.cpp` 会在目标系统检测 `/etc/rc.common` + `/sbin/procd` 并安装 procd 脚本。

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
  "TlsVerify": true
}
```

关键校验：

- `Server` 必填，scheme 仅 `http`/`https`。
- `MetricsInterval` 5–3600 秒；`CommandTimeout` 1–300 秒。
- `Code`/`Secret` 由登录响应写入，原子落盘并限制为 `0600`（Windows 上为 CRT 只读位 + 数据目录所有者隔离，见“已知限制”）。

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
| Code / Secret / Project / Channel | `STARAGENT_CODE` / `STARAGENT_SECRET` / `STARAGENT_PROJECT` / `STARAGENT_CHANNEL` |

### CLI

```text
staragent run        [--config path] [--server url] [--data dir] [--log-level level] [--allow-exec] [--console]
staragent once       [--config path] [--console]
staragent version
staragent install    [--config path] [--server url] [--data dir] [--user user] [--log-level level] [--dir dir]
staragent uninstall | start | stop | status
```

`run` 为长连接模式；`once` 登录、心跳并处理本次心跳中的命令后退出，适合自检/定时任务。

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
| `node/reboot` / `deploy/*` / `framework/install` | — | 首版回复“不支持命令” |

安全与限制：

- `AllowExec=false` 时 `bash`/`cmd` 一律回复 `Status=3` / `命令执行未启用`。
- 开启后仍受 `AllowedCommands` 白名单（精确匹配或 `前缀*`）约束；参数上限 8KB。
- 输出合并 stdout/stderr，超 64KB 保留头尾并插入截断标记。
- 默认超时 30 秒，可由参数覆盖，硬上限 300 秒；超时终止**整个进程树**（Windows Job Object / POSIX 进程组）。
- 最多 4 个命令并发；shell 命令串行执行。
- 命令按 `Id` 去重 1 小时；`Expire` 过期回复 `Status=3`；`StartTime` 未来时先回复 `Status=1` 再延迟执行。
- 所有回复（过期、延迟、取消、成功、失败）都携带原始 `Id`。

## 安装为系统服务

### Windows（管理员）

```powershell
staragent.exe install --config C:\ProgramData\StarAgent\staragent.json --server https://star.example.com:6600
staragent.exe start
staragent.exe status
staragent.exe stop
staragent.exe uninstall
```

服务名 `StarAgentNative`；服务命令行包含 `run --config <path> --service`。安装/卸载/启动/停止需要管理员权限。

### Linux（systemd，root）

```bash
sudo staragent install --config /etc/staragent/staragent.json --server https://star.example.com:6600
sudo systemctl enable --now staragent   # install 已自动 enable
staragent status
```

写入 `/etc/systemd/system/staragent.service`，`Restart=always`，可选 `--user` 指定运行用户。

### OpenWrt（procd，root）

检测到 `/etc/rc.common` 与 `/sbin/procd` 时写入 `/etc/init.d/staragent`（`USE_PROCD=1`，含 respawn），并执行 `enable`。

### macOS（launchd）

root 写入 `/Library/LaunchDaemons/com.newlifex.staragent.plist`；普通用户写入 `~/Library/LaunchAgents/`。`install` 会 `launchctl load -w`，`uninstall` 会 unload 并删除 plist。

## 能力边界与已知限制

- **不自升级**：收到 `/node/upgrade` 非空版本时只记录日志并上报 `Upgrade` 事件，需人工更新；`cap:upgrade=false`。
- **deploy / framework / reboot 未实现**：按协议回复“不支持命令”。
- **WebSocket 可选**：`-DSTARAGENT_WITH_WS=OFF` 时仍可通过心跳 `Commands` 执行命令；但 `--scenario reconnect/dedup` 等 WS 场景不可用。
- **TLS 依赖 OpenSSL**：未检测到 OpenSSL 的构建只能 `http/ws`；生产必须安装 OpenSSL 并重新构建。
- **Windows 权限**：`0600` 通过 CRT `_chmod` 与数据目录隔离近似实现，无法像 POSIX 一样精确设置 ACL；建议将 `DataDir` 放在受控目录。
- **node/restart 与 SCM**：重启会以原命令行拉起新进程；若父进程由 SCM 启动，子进程的 `--service` 在非 SCM 环境下会回退为控制台模式。生产建议同时配置 Windows 服务恢复策略，或改用服务管理器重启。
- **Linux/macOS 验证**：实现已提供，但当前 Windows 环境未完成对应平台的编译/运行验证；建议在 CI 增加 Ubuntu 与 macOS 构建任务。
- **指标降级**：任何单项采集失败只把该字段填 0，不跳过心跳；`Temperature`、`Battery`、`Tcp*` 等未采集字段为 0。
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
