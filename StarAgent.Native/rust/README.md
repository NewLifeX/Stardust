# StarAgent Native (Rust)

StarAgent 的 Rust 原生实现，遵循 [`../spec/protocol.md`](../spec/protocol.md) 与
[`../spec/schemas/node-agent.schema.json`](../spec/schemas/node-agent.schema.json)，
行为与 [`../go`](../go) 参考实现对齐。

## 已实现能力

- 登录 / 注销 / 心跳 / 令牌续期 / `NewServer` 迁移 / 指数退避重连（1s→30s，带抖动）
- HTTP：`/node/login`、`/node/logout`、`/node/ping`、`/node/postevents`、
  `/node/commandreply`、`/node/upgrade`
- WebSocket：`/node/notify`（`Authorization: Bearer`），文本 `Ping`/`Pong` 保活，
  JSON `CommandModel` 执行后通过 HTTP 回复
- 心跳响应中的 `Commands`（HTTP 降级）执行
- 指标：CPU、内存、磁盘容量/挂载点、网络速率、进程数/进程名、运行时长、系统负载；
  单项采集失败填 0，不影响心跳
- 命令：按 `Id` 去重 1 小时、过期取消、`StartTime` 延迟执行、默认 30s 超时（最大 300s）、
  输出 64KB 头尾截断、进程树终止、最多 4 个并发（shell 串行）
- 内置命令：`bash`（Unix）、`cmd`（Windows）、`node/synctime`、
  `node/setchannel`、`node/restart`；`node/reboot`、`deploy/*`、`framework/install`
  返回“不支持命令”
- 安全默认 `AllowExec=false`；开启后仍受 `AllowedCommands` 白名单、8KB 参数上限、
  超时与 64KB 输出上限约束
- 配置文件 `staragent.json` + `STARAGENT_` 环境变量覆盖；`Code`/`Secret` 原子持久化，
  权限 `0600`；日志按天滚动并保留 7 天
- CLI：`run`、`once`、`version`、`install`、`uninstall`、`start`、`stop`、`status`
- 服务形态：Linux systemd、OpenWrt procd、macOS launchd、Windows Service
  （`windows-service`，服务名 `StarAgentNative`）

## 目录

```
src/
├── main.rs        CLI 入口、服务检测、信号处理
├── app.rs         配置装载、日志初始化、启动参数覆盖
├── config.rs      配置、环境变量、原子持久化（0600）
├── protocol.rs    PascalCase JSON 模型、时间格式、响应包装解析
├── client.rs      HTTP 客户端、令牌、401 重新登录、退避
├── notify.rs      WebSocket /node/notify、Ping/Pong、命令解析
├── metrics.rs     sysinfo 指标采集与失败降级
├── identity.rs    稳定硬件标识、网络与系统信息
├── command/       命令执行、去重、白名单、超时、输出截断、平台实现
├── agent.rs       登录/心跳/迁移/事件/命令主循环
├── events.rs      有界事件队列（落盘 events.json）
├── logging.rs     按天轮转日志
└── service.rs     systemd / procd / launchd / Windows 服务
```

## 构建

需要 Rust 1.82+（本仓库使用 stable 工具链验证）。

```powershell
cd StarAgent.Native\rust
cargo build --release
cargo test
cargo fmt --check
```

如果 crates.io 下载超时，可在**本地**（不要提交）创建 `.cargo/config.toml` 使用 rsproxy
稀疏镜像；CI 与普通用户默认直接使用 crates.io：

```toml
[source.crates-io]
replace-with = "rsproxy-sparse"

[source.rsproxy-sparse]
registry = "sparse+https://rsproxy.cn/index/"
```

产物：`target\release\staragent.exe`（Windows）或 `target/release/staragent`。

构建信息可通过环境变量覆盖版本号：

```powershell
$env:STARAGENT_VERSION = "1.0.2026.1017"
cargo build --release
```

`build.rs` 会把版本与构建时间写入二进制，`NodeInfo.Compile` 使用构建时间。

### 交叉编译

TLS 使用 rustls + ring（不依赖 OpenSSL），纯 Rust 依赖便于交叉编译：

```bash
rustup target add x86_64-unknown-linux-musl aarch64-unknown-linux-musl
cargo build --release --target x86_64-unknown-linux-musl
cargo build --release --target aarch64-unknown-linux-musl
rustup target add x86_64-apple-darwin aarch64-apple-darwin
cargo build --release --target x86_64-apple-darwin
cargo build --release --target aarch64-apple-darwin
```

`ring` 包含 C/汇编代码，交叉编译需要目标平台的 C 工具链：

- Linux musl：安装 `musl-gcc`（Debian/Ubuntu：`apt install musl-tools`），或使用
  `cargo install cargo-zigbuild` + Zig 后执行
  `cargo zigbuild --release --target x86_64-unknown-linux-musl`。
- macOS：需要 Apple SDK 与 Xcode command line tools，通常只能在 macOS 上构建。
- Windows x64：在 Windows 上使用 MSVC 工具链直接构建。

缺少工具链时 `cargo build` 会明确报错，本项目不会把未生成的产物标记为成功。

## 配置

默认读取当前目录 `staragent.json`（可用 `--config` 或 `STARAGENT_CONFIG` 覆盖）：

```json
{
  "Server": "https://star.example.com:6600",
  "ProductCode": "StarAgent-Rust",
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

约束：`MetricsInterval` 5~3600 秒；`CommandTimeout` 1~300 秒；`Server` 必须是
`http://` 或 `https://`。心跳实际周期以服务端 `PingResponse.Period` 为准（下限 1 秒）。

环境变量（前缀 `STARAGENT_`，同时兼容下划线写法）：

| 变量 | 示例 |
|------|------|
| `STARAGENT_SERVER` / `STARAGENT_SERVER_URL` | `https://star.example.com:6600` |
| `STARAGENT_PRODUCTCODE` / `STARAGENT_PRODUCT_CODE` | `StarAgent-Rust` |
| `STARAGENT_CODE` / `STARAGENT_SECRET` | 已注册节点身份 |
| `STARAGENT_PROJECT` | `demo` |
| `STARAGENT_CHANNEL` | `Release` |
| `STARAGENT_DATADIR` / `STARAGENT_DATA_DIR` | `/var/lib/staragent` |
| `STARAGENT_LOGLEVEL` / `STARAGENT_LOG_LEVEL` | `debug` |
| `STARAGENT_METRICSINTERVAL` / `STARAGENT_METRICS_INTERVAL` | `60` |
| `STARAGENT_ALLOWEXEC` / `STARAGENT_ALLOW_EXEC` | `false` |
| `STARAGENT_ALLOWEDCOMMANDS` / `STARAGENT_ALLOWED_COMMANDS` | `node/*,bash`（逗号或分号分隔） |
| `STARAGENT_COMMANDTIMEOUT` / `STARAGENT_COMMAND_TIMEOUT` | `30` |
| `STARAGENT_TLSVERIFY` / `STARAGENT_TLS_VERIFY` | `true` |

`Code`/`Secret` 只由登录响应写入文件；`Token` 仅保存在内存，不落盘。
`AllowedCommands` 支持精确名称与后缀 `*`（例如 `node/*`）。

## 运行

```bash
./staragent run --config /opt/staragent/staragent.json
./staragent once --config ./staragent.json    # 登录、心跳、处理命令后退出
./staragent version
```

命令执行默认关闭。需要时显式开启，并建议同时配置白名单：

```bash
./staragent run --allow-exec --config /opt/staragent/staragent.json
```

## 安装为服务

### Linux（systemd）

```bash
sudo ./staragent install --server https://star.example.com:6600 \
  --config /opt/staragent/staragent.json --dir /opt/staragent --user staragent
sudo ./staragent status
```

生成 `/etc/systemd/system/staragent.service`，包含 `Restart=always`、
`KillSignal=SIGTERM`，并执行 `systemctl daemon-reload` / `enable`。需要 root。

### OpenWrt（procd）

检测到 `/etc/openwrt_release` 时，`install` 改为生成 `/etc/init.d/staragent`
（`start_service` / `stop_service` / `procd_set_param respawn`）。建议使用 musl 静态构建。

### macOS（launchd）

root 安装写入 `/Library/LaunchDaemons/com.newlifex.staragent.plist`；
普通用户写入 `~/Library/LaunchAgents/com.newlifex.staragent.plist`。
`start` / `stop` 使用 `launchctl load -w` / `unload -w`。

### Windows

以管理员身份执行：

```powershell
.\staragent.exe install --server https://star.example.com:6600 `
  --config C:\ProgramData\StarAgent\bin\staragent.json
.\staragent.exe start
.\staragent.exe status
.\staragent.exe uninstall
```

服务名 `StarAgentNative`，由 SCM 启动 `run` 时自动进入服务模式
（`windows-service` 的 `service_dispatcher`）。

## HTTPS / WSS

- HTTP 请求与 WebSocket 握手统一使用 rustls + ring（不依赖 OpenSSL），便于静态/交叉构建。
- `Server` 为 `https://` 时，`/node/notify` 自动使用 `wss://`；`Authorization: Bearer` 头同时用于 HTTP 与 WS 握手。
- `TlsVerify=true`（默认）使用 webpki 内置根证书校验服务端证书；`false` 关闭校验，仅用于测试环境。
- 当前版本使用内置根证书，不读取系统证书库，也不支持自定义 CA 文件；使用企业私有 CA 时需自行构建或改用受信任证书（见“已知限制”）。

## 一致性测试

```powershell
cd StarAgent.Native\conformance
go build -o conformance.exe ./cmd/conformance
.\conformance.exe --agent ..\rust\target\release\staragent.exe `
  --config .\templates\agent.json --scenario all --workdir ..\rust\target\conformance
```

目标：6 passed / 0 failed。runner 默认执行 `run --config <file>`，并通过环境变量
`STARAGENT_SERVER`、`STARAGENT_DATA_DIR`、`STARAGENT_PRODUCTCODE` 注入配置。

本实现在 Windows（release 构建）上使用 `--scenario all` 实测结果为
**6 passed / 0 failed**：`basic`、`http-fallback`、`dedup`、`expired`、`reconnect`、`events`。

## 安全说明

- 生产环境保持 `TlsVerify=true`；`false` 仅用于测试。
- 日志不输出 `Secret`、`Token` 与命令完整参数；审计日志只记录命令名、Id、
  状态、耗时与参数字节数。
- shell 命令默认关闭；开启后仍受白名单、8KB 参数上限、超时与 64KB 输出上限约束。
- `node/restart` 使用当前可执行文件与原始参数重新拉起进程后退出。
- 配置、事件队列使用临时文件 + rename 原子写入；Unix 下配置权限 `0600`（Windows 依赖 ACL）。

## 已知限制

- 首版不自动升级：`/node/upgrade` 有可用版本时仅记录日志并上报 `Upgrade` 事件。
- `node/reboot`、`deploy/*`、`framework/install` 明确返回不支持。
- `node/synctime` 需要 root/管理员权限，失败会返回错误与 stderr 摘要。
- 在 systemd/launchd 下使用 `node/restart` 时，监督进程可能同时拉起实例；
  生产环境建议改用 `systemctl restart staragent` 等外部编排。
- Windows 服务模式下 `node/restart` 拉起的是普通进程而非 SCM 服务实例，需要外部服务管理器兜底。
- Android/iOS 不在本目录范围内。
- 指标中的 `Temperature`、`Battery`、`TcpConnections`、`GatewayLatency` 等字段
  当前固定为 0（协议允许缺失填 0）。
- TLS 使用 webpki 内置根证书，不读取系统证书库，暂不支持自定义 CA 文件；企业私有 CA
  场景需换用受信任证书或自行扩展 `Client` 的 TLS 配置。
- Linux/macOS 交叉构建需要目标平台的 C 工具链（`ring` 含 C/汇编代码）：
  `x86_64-unknown-linux-musl` 需要 `musl-gcc` 或 `cargo-zigbuild` + Zig；
  macOS 目标需要 Apple SDK。缺少工具链时构建会失败并给出明确错误。
