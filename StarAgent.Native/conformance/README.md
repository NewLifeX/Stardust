# StarAgent 一致性测试夹具（conformance）

本目录是**语言无关**的黑盒一致性测试：用 Go 实现一个假 StarServer（HTTP + WebSocket）和场景 runner，启动任意语言的 Agent 可执行文件，验证登录、心跳、命令、回复、重连、事件补报等协议行为。

协议唯一事实源见 [`../spec/protocol.md`](../spec/protocol.md)，JSON 契约见 [`../spec/schemas/node-agent.schema.json`](../spec/schemas/node-agent.schema.json)。

## 目录结构

```
conformance/
├── cmd/conformance/          # CLI runner（含 mock-agent 子命令）
├── internal/fakeserver/      # 假 StarServer + /control/* 测试控制接口
├── internal/mockagent/       # 内置最小 mock Agent（仅用于夹具自测）
├── internal/protocol/        # 与 spec 对齐的 PascalCase JSON 模型
├── internal/scenario/        # 场景定义、断言、进程与配置注入
└── templates/agent.json      # 配置模板示例
```

## 快速开始

```powershell
cd StarAgent.Native\conformance

# 1. 单元测试 + 用 mock Agent 跑全部场景
go test ./...
go run ./cmd/conformance --mock-agent --scenario all

# 2. 构建
go build -o conformance ./cmd/conformance

# 3. 测试真实 Agent（示例）
.\conformance.exe --agent C:\path\to\staragent.exe --config .\templates\agent.json --scenario all
```

Windows 下生成 `conformance.exe`；Linux/macOS 为 `conformance`。

## Agent 接入约定

### 启动方式

runner 默认执行：

```text
<agent> run --config <配置文件>
```

也可以用环境变量提供配置；示例：

```powershell
.\conformance.exe --agent .\staragent --agent-args "run --config {config}" --scenario basic
```

`--agent-args` 支持占位符：`{config}`、`{server}`、`{data}`、`{workdir}`。参数按空白拆分，不支持带空格的引号参数；路径含空格时建议直接用默认参数。

runner 会在 Agent 进程环境中继承当前环境，并额外设置：

| 变量 | 值 |
|------|----|
| `STARAGENT_SERVER` / `STARAGENT_SERVER_URL` | 假服务器地址，如 `http://127.0.0.1:54321` |
| `STARAGENT_DATA_DIR` / `STARAGENT_DATADIR` | 本次场景的数据目录 |
| `STARAGENT_CONFIG` / `STARAGENT_CONFIG_FILE` | 生成的配置文件路径 |
| `STARAGENT_PRODUCTCODE` / `STARAGENT_PRODUCT_CODE` | `StarAgent-Conformance` |
| `STARAGENT_WORKDIR` | 场景工作目录 |

用户自行设置的环境变量不会被清除。若 Agent 支持 `STARAGENT_*` 覆盖，可用 `STARAGENT_METRICS_INTERVAL` 等调整行为（以 Agent 自身的优先级为准）。

### 配置文件注入

- `--config <template>` 指定模板；不指定时 runner 生成默认 `staragent.json`。
- JSON 模板中可使用字符串占位符：`{{SERVER}}`、`{{SERVER_URL}}`、`{{WS_URL}}`、`{{DATA_DIR}}`、`{{CONFIG_DIR}}`、`{{WORK_DIR}}`。
- runner 会强制把 `Server` 改为假服务器地址、把 `DataDir` 改到场景目录；其他字段原样保留。
- 非 JSON 模板会做纯文本占位符替换；此时 `Server`/`DataDir` 必须由占位符或环境变量提供。
- **`MetricsInterval` 默认写入 5 秒**（Go Agent 的合法范围是 5~3600）。模板显式给出时以模板为准，也可用 `--metrics-interval` 调整默认值。
- 假服务器心跳响应固定 `Period=1`（秒），与 `MetricsInterval` 无关；Agent 应使用响应中的 `Period` 控制心跳周期。
- 模板中的 `Code`/`Secret` 必须为空。首次登录由假服务器动态下发，Agent 必须持久化到 `DataDir` 或配置文件；`basic` 场景会重启 Agent 验证二次登录。

### 协议行为要求

| 行为 | 场景 | 要求 |
|------|------|------|
| 首次登录 | basic | `Code`/`Secret` 为空；返回后持久化服务端下发的值 |
| 二次登录 | basic | 携带已持久化的 `Code`/`Secret`；登录成功 |
| 心跳 | basic / reconnect | `POST /node/ping` 携带 Bearer Token；`PingInfo.Time` 为 Unix 毫秒 |
| WebSocket | basic / dedup / expired / reconnect | `WS /node/notify` 携带 Bearer Token；收到文本 `Ping` 回 `Pong` |
| 命令执行 | basic | `node/synctime` 回复 `Status=2` 且 `Data` 非空 |
| 去重 | dedup | 相同 `Id` 只回复一次 |
| 过期 | expired | `Expire` 早于当前时间时回复 `Status=3` |
| 断线重连 | reconnect | 服务端强制断开后重新连接 WS 并恢复心跳/命令 |
| HTTP 降级 | http-fallback | WS 不可用时，执行心跳响应 `Commands` 中的命令 |
| 事件补报 | events | 上报失败后重试；**至少产生一个事件**（如 `Startup`） |

`events` 场景依赖 Agent 在启动后上报事件（mock Agent 上报 `Startup`）。若被测 Agent 没有启动事件，请先补充事件上报或为该场景提供可触发的事件。

## 场景

| 名称 | 验证内容 |
|------|----------|
| `basic` | 动态注册、Token、心跳、WS 连接、应用层 Ping/Pong、WS 命令与回复、崩溃重启后复用 `Code`/`Secret` |
| `http-fallback` | 禁用 WS 后，命令经 `PingResponse.Commands` 下发并回复 |
| `dedup` | 同一命令 `Id` 投递两次，只执行/回复一次（1.5 秒观察窗口） |
| `expired` | `Expire` 已过期的命令回复 `Status=3` |
| `reconnect` | `/control/kick` 断开 WS 后 Agent 自动重连，连接数与心跳恢复，命令仍可送达 |
| `events` | 前两次 `/node/postevents` 返回 500，Agent 退避重试直至成功 |

`--scenario all` 运行全部；也支持逗号分隔，如 `--scenario basic,events`。`--list` 可列出场景。

## CLI

```text
conformance [flags]

--agent <path>            被测 Agent 可执行文件
--agent-args <args>       自定义启动参数（默认 "run --config <file>"）
--config <path>           配置模板（可选）
--mock-agent              使用内置 mock Agent 代替外部进程
--scenario <name>         场景名、逗号列表或 all（默认 all）
--timeout <duration>      总超时（默认 5m）
--scenario-timeout <d>    单场景超时（默认 90s）
--metrics-interval <n>    生成配置中的 MetricsInterval（默认 5，模板值优先）
--workdir <path>          产物根目录（默认系统临时目录）
--keep                    成功后也保留场景目录
--verbose                 实时输出 Agent 日志
--list                    列出场景并退出
```

退出码：`0` 全部通过；`1` 有场景失败；`2` 参数/环境错误。

失败时会输出失败原因、服务器状态摘要、最近请求、Agent 日志尾部与产物目录。失败场景的目录（含 `agent.log`、`staragent.json`、`data/`）默认保留以便排查；成功场景默认清理。

## 控制接口（仅测试）

假服务器额外暴露 `/control/*`，供 runner 驱动场景，也可用于自定义调试：

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/control/state` | 完整状态快照（登录、心跳、回复、事件、WS、请求记录等） |
| POST | `/control/command?via=ws\|http\|auto` | 入队命令；`ws` 立即下发，`http` 放入下一次心跳响应 |
| POST | `/control/ping` | 设置 `Period`/`NewServer`/`SyncTime`/`RotateToken`，并可附加 HTTP 命令 |
| POST | `/control/kick` | 强制关闭所有 WS 连接 |
| POST | `/control/events-fail` | 让接下来 N 次事件上报失败：`{"Count":2,"Status":500}` |
| POST | `/control/ws` | 禁用/启用 WS：`{"Disabled":true}` |
| POST | `/control/upgrade` | 设置 `/node/upgrade` 返回值；`{"Clear":true}` 清空 |

## 构建与测试

```powershell
go test ./...
go build ./cmd/conformance
go vet ./...
```

- 仅依赖标准库与 `github.com/coder/websocket`（服务端 `Accept` 与客户端 `Dial`）。
- 若 `proxy.golang.org` 超时，可改用：
  ```powershell
  $env:GOPROXY = 'https://goproxy.cn,direct'
  go mod download
  ```
- 测试包含 `internal/scenario` 的全场景集成测试：启动假服务器 + 内置 mock Agent，覆盖全部 6 个场景。

## 接入新语言（最短路径）

1. 按 [`../spec/protocol.md`](../spec/protocol.md) 实现 Agent，入口约定为 `<agent> run --config <file>`；至少读取 `Server`/`DataDir`，并建议支持 `STARAGENT_*` 环境变量兜底。
2. 直接复用本目录模板：`--config templates/agent.json`。runner 会把 `Server`、`DataDir` 重写到本次场景，`Code`/`Secret` 保持为空，`MetricsInterval` 默认 5。
3. 先跑 `basic` 建立最小闭环，再跑 `all`：
   ```powershell
   .\conformance.exe --agent .\staragent --config .\templates\agent.json --scenario basic
   .\conformance.exe --agent .\staragent --config .\templates\agent.json --scenario all
   ```
4. 失败时按输出定位：`reason` → `state` → `requests` → `replies`/`event attempts` → `<workdir>\<scenario>\agent.log`。修完重跑，目标输出 `6 passed, 0 failed`。
5. 新语言不需要链接任何库，只要可执行文件能启动并遵守协议；CI 中可直接 `go test ./...` + `go run ./cmd/conformance --agent <binary> --scenario all`。
6. 需要新增断言时，在 `internal/scenario/scenarios.go` 添加一个 `Definition` 即可复用假服务器、配置注入和进程管理。

## 新增语言实现检查清单

1. 实现 `run --config <file>`（或等价的环境变量入口），读取 `Server`/`DataDir`。
2. 首次登录发送空 `Code`/`Secret`，持久化响应值；重启后携带。
3. 用登录返回的 `Token` 作为后续 HTTP/WS 的 Bearer Token。
4. 按心跳响应 `Period` 发送 `POST /node/ping`，并处理 `Commands` 降级命令。
5. 连接 `WS /node/notify`，回复应用层 `Ping`，执行 JSON 命令并通过 HTTP 回复。
6. 实现 `node/synctime`、命令去重、过期判断。
7. 启动后上报至少一个事件，并实现失败重试。
8. 运行 `conformance --agent <binary> --config templates/agent.json --scenario all`。

## 当前限制

- 假服务器只监听 `127.0.0.1` 明文 HTTP，不测试 TLS；生产 TLS 行为不在本夹具范围。
- 已提供 `/node/upgrade`、`/node/logout`、`Token` 轮换、`NewServer` 的控制能力，但默认场景未断言这些行为。
- 不使用真实 StarServer/数据库；鉴权、命令归属等仅做最小校验。
