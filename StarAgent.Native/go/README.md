# StarAgent Native (Go)

StarAgent 原生 Go 实现，遵循 [`../spec/protocol.md`](../spec/protocol.md) 与
[`../spec/schemas/node-agent.schema.json`](../spec/schemas/node-agent.schema.json)。

## 已实现能力

- 登录 / 注销 / 心跳 / 令牌续期 / `NewServer` 迁移 / 指数退避重连（1s→30s，带抖动）
- HTTP：`/node/login`、`/node/logout`、`/node/ping`、`/node/postevents`、
  `/node/commandreply`、`/node/upgrade`
- WebSocket：`/node/notify`（`Authorization: Bearer`），文本 `Ping`/`Pong` 保活，
  JSON `CommandModel` 执行后走 HTTP 回复
- 心跳响应中的 `Commands`（HTTP 降级）执行
- 指标：CPU、内存、磁盘容量/挂载点、网络速率、进程数/进程名、运行时长、系统负载；
  单项采集失败填 0，不影响心跳
- 命令：按 `Id` 去重 1 小时、过期取消、`StartTime` 延迟执行、默认 30s 超时（最大 300s）、
  输出 64KB 头尾截断、进程树终止、最多 4 个并发（shell 串行）
- 内置命令：`bash`（Linux/macOS）、`cmd`（Windows）、`node/synctime`、
  `node/setchannel`、`node/restart`；`node/reboot`、`deploy/*`、`framework/install`
  返回“不支持命令”
- 安全默认 `AllowExec=false`；开启后仍受 `AllowedCommands` 白名单约束
- 配置文件 `staragent.json` + `STARAGENT_` 环境变量覆盖；`Code`/`Secret` 原子持久化，
  权限 `0600`；日志按天滚动并保留 7 天
- CLI：`run`、`once`、`version`、`install`、`uninstall`、`start`、`stop`、`status`
- 服务形态：Linux systemd、OpenWrt procd、Windows 服务（`golang.org/x/sys/windows/svc`）、
  macOS launchd

## 目录

```
cmd/staragent          CLI 入口与服务检测
internal/config        配置、环境变量覆盖、原子持久化
internal/identity      稳定硬件标识、网络与系统信息
internal/protocol      JSON 模型、时间格式、响应包装解析
internal/client        HTTP 客户端、401 重登、退避、WebSocket 通知
internal/metrics       gopsutil 指标采集与失败降级
internal/command       命令模型执行、去重、白名单、超时与截断
internal/agent         登录/心跳/迁移/事件/命令主循环
internal/service       systemd / OpenWrt / launchd / Windows 服务
internal/logging       按天轮转日志
```

## 构建

需要 Go 1.24+（`gopsutil/v4` 要求）。

```bash
cd StarAgent.Native/go
go build ./cmd/staragent
go test ./...
```

可选构建信息：

```bash
go build -ldflags "-X main.Version=1.0.2026.1017 -X 'main.BuildTime=2026-10-17 08:00:00'" ./cmd/staragent
```

## 配置

默认读取当前目录 `staragent.json`（可用 `-config` 或 `STARAGENT_CONFIG` 覆盖）：

```json
{
  "Server": "https://star.example.com:6600",
  "ProductCode": "StarAgent-Go",
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

环境变量（前缀 `STARAGENT_`，字段名转大写）：

| 变量 | 示例 |
|------|------|
| `STARAGENT_SERVER` | `https://star.example.com:6600` |
| `STARAGENT_PRODUCTCODE` | `StarAgent-Go` |
| `STARAGENT_CODE` / `STARAGENT_SECRET` | 已注册节点身份 |
| `STARAGENT_PROJECT` | `demo` |
| `STARAGENT_CHANNEL` | `Release` |
| `STARAGENT_DATADIR` | `/var/lib/staragent` |
| `STARAGENT_LOGLEVEL` | `debug` |
| `STARAGENT_METRICSINTERVAL` | `60` |
| `STARAGENT_ALLOWEXEC` | `false` |
| `STARAGENT_ALLOWEDCOMMANDS` | `node/synctime,bash`（逗号或分号分隔） |
| `STARAGENT_COMMANDTIMEOUT` | `30` |
| `STARAGENT_TLSVERIFY` | `true` |

`Code`/`Secret` 只由登录响应写入文件；`Token` 仅保存在内存，不落盘。
`AllowedCommands` 支持精确名称与后缀 `*`（例如 `node/*`）。

## 运行

```bash
./staragent run -config /opt/staragent/staragent.json
./staragent once -config ./staragent.json     # 单次登录、心跳、处理命令后退出
./staragent version
```

命令执行默认关闭。需要时显式开启，并建议同时配置白名单：

```bash
./staragent run -allow-exec
```

## 安装为服务

### Linux（systemd）

```bash
sudo ./staragent install -server https://star.example.com:6600 \
  -config /opt/staragent/staragent.json -dir /opt/staragent -user staragent
sudo ./staragent status
```

生成 `/etc/systemd/system/staragent.service`，包含 `Restart=always`、
`KillSignal=SIGTERM`。需要 root。

### OpenWrt（procd）

同一 `install` 命令在检测到 `/etc/openwrt_release` 时改为生成
`/etc/init.d/staragent`（`start_service` / `stop_service` / `respawn`）。
建议使用 `CGO_ENABLED=0` 构建：

```bash
CGO_ENABLED=0 GOOS=linux GOARCH=arm GOARM=7 go build -trimpath -o staragent-arm ./cmd/staragent
```

### macOS（launchd）

`sudo ./staragent install -server ...` 写入 `/Library/LaunchDaemons/com.newlifex.staragent.plist`；
非 root 安装写入 `~/Library/LaunchAgents/com.newlifex.staragent.plist`。

### Windows

以管理员身份执行：

```powershell
.\staragent.exe install -server https://star.example.com:6600 `
  -config C:\ProgramData\StarAgent\bin\staragent.json
.\staragent.exe start
.\staragent.exe status
.\staragent.exe uninstall
```

服务名 `StarAgentNative`。`run` 由 SCM 启动时会自动进入服务模式。

## 交叉编译

```bash
export CGO_ENABLED=0
GOOS=linux   GOARCH=amd64 go build -trimpath -o staragent-linux-amd64 ./cmd/staragent
GOOS=linux   GOARCH=arm64 go build -trimpath -o staragent-linux-arm64 ./cmd/staragent
GOOS=linux   GOARCH=arm GOARM=7 go build -trimpath -o staragent-linux-arm ./cmd/staragent
GOOS=windows GOARCH=amd64 go build -trimpath -o staragent-windows-amd64.exe ./cmd/staragent
GOOS=windows GOARCH=arm64 go build -trimpath -o staragent-windows-arm64.exe ./cmd/staragent
GOOS=darwin  GOARCH=amd64 go build -trimpath -o staragent-darwin-amd64 ./cmd/staragent
GOOS=darwin  GOARCH=arm64 go build -trimpath -o staragent-darwin-arm64 ./cmd/staragent
```

本项目不依赖 CGO，OpenWrt 直接使用 `CGO_ENABLED=0` 构建即可。

## 安全说明

- 生产环境保持 `TlsVerify=true`；`false` 仅用于测试。
- 日志不输出 `Secret`、`Token` 与命令完整参数；命令审计只记录名称、Id、状态、耗时与参数字节数。
- shell 命令默认关闭；开启后仍受白名单、8KB 参数上限、超时与 64KB 输出上限约束。
- `node/restart` 使用当前可执行文件与原始参数重新拉起进程后退出，不硬编码进程名。
- 配置与事件队列使用临时文件 + rename 原子写入；配置文件权限 `0600`（Windows 依赖 ACL）。

## 已知限制

- 首版不自动升级：`/node/upgrade` 有可用版本时仅记录日志并上报 `Upgrade` 事件。
- `node/reboot`、`deploy/*`、`framework/install` 明确返回不支持。
- `node/synctime` 需要 root/管理员权限，失败会返回错误与 stderr 摘要。
- 在 systemd/launchd 下使用 `node/restart` 时，监督进程可能同时拉起实例；
  生产环境建议改用 `systemctl restart staragent` 等外部编排。
- Windows 服务模式下 `node/restart` 拉起的是普通进程而非 SCM 服务实例，需要外部服务管理器兜底。
- Android/iOS 不在本仓库范围内。
