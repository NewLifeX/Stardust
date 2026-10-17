# StarAgent Native 协议规范

> 版本：v1.0（首版）
> 适用：Go / Rust / C++ 原生 Agent
> 参考实现：`Stardust/StarClient.cs`、`Stardust.Server/Controllers/NodeController.cs`

本规范是原生 Agent 与 StarServer 通信的**唯一事实源**。任何实现与本文冲突时，以本文为准；发现本文与 C# 实现不一致时，先修正本文并补一致性测试，再改实现。

---

## 1. 传输与鉴权

### 1.1 基础地址

- 协议：`http://` 或 `https://`，生产环境必须使用 HTTPS。
- 地址：`StarServer` 的站点根地址，默认端口 `6600`，例如 `https://star.example.com:6600`。
- 路径大小写：ASP.NET Core 路由不区分大小写；本文统一使用小写，例如 `/node/login`。

### 1.2 令牌

1. 首次登录成功后，服务端返回 `Token` 与 `Expire`（秒）。
2. 后续 HTTP 请求和 WebSocket 握手必须携带：

```http
Authorization: Bearer <Token>
```

3. 心跳响应可能返回新的 `Token`，收到后立即替换内存中的令牌。
4. `Token` 只保存在内存；落盘只保存 `Code` 与 `Secret`。
5. 收到 HTTP `401` 时：清除令牌、重新登录、重试当前请求一次；仍失败则进入退避重连。

### 1.3 超时与重试

- 建议连接超时 5 秒，请求超时 15 秒；心跳请求超时不超过心跳周期。
- 登录失败、心跳失败、WebSocket 断开使用**指数退避 + 抖动**：1s、2s、4s、8s、16s、30s 封顶。
- 网络恢复、进程唤醒后立即重试一次，不受退避限制。
- 命令回复失败进入重试队列，至少重试 3 次；超过后记录错误并丢弃，避免无限堆积。

---

## 2. JSON 约定

### 2.1 字段命名

- 服务端使用 `System.Text.Json` 默认命名策略，字段为 **PascalCase**，例如 `Code`、`Token`、`ServerTime`。
- 原生 Agent 输出使用 PascalCase；解析时建议**大小写不敏感**，以兼容服务端后续调整。
- 未识别字段必须忽略，不得因新增字段解析失败。
- 可空字段允许缺失或 `null`；数值字段缺失按 `0` 处理。

### 2.2 时间

- `PingInfo.Time`：Unix 毫秒（UTC），`Int64`。
- `EventModel.Time`：Unix 毫秒（UTC），`Int64`。
- `LoginResponse.ServerTime` / `PingResponse.ServerTime`：Unix 毫秒（UTC），`Int64`。
- `NodeInfo.Compile` / `NodeInfo.Time`：服务端使用本地时间转换器，建议格式 `yyyy-MM-dd HH:mm:ss`（UTC 值，不带时区后缀）。原生 Agent 必须发送 `Compile`（构建时间），`Time` 可发送当前 UTC 时间。
- `NodeInfo.Dns` / `PingInfo.Dns`：服务端 `Node.Dns` 字段限制 50 字符，原生 Agent 应只上报前几个 DNS 地址并截断到 50 字符，否则节点校验会失败。
- `CommandModel.StartTime` / `Expire`：同样使用 `yyyy-MM-dd HH:mm:ss` 形式的 UTC 时间；未设置时发送 `0001-01-01 00:00:00` 或省略。

### 2.3 响应包装

- StarServer 可能通过全局过滤器把响应包装为 `{ "code": 0, "data": <载荷>, "msg": "..." }`，也可能返回裸 JSON 对象/裸整数；两类都必须支持。
- 通用解析规则：若响应是对象且包含 `data` 字段，并且 `code` 字段存在，则取 `data`；否则按裸对象/裸值解析。
- `code != 0` 表示业务错误，按失败处理并记录 `msg`。
- HTTP 非 2xx 一律视为失败；记录响应体（截断）并进入重试/退避。

### 2.4 产品编码

每种实现使用独立的 `ProductCode`，用于服务端隔离节点与升级包：

| 实现 | ProductCode | 升级包 `TargetRuntime` |
|------|-------------|------------------------|
| .NET StarAgent | `StarAgent` | `6` / `7` / `8` / `9` / `10` 等 |
| Go 原生 Agent | `StarAgent-Go` | `go` |
| Rust 原生 Agent | `StarAgent-Rust` | `rust` |
| C++ 原生 Agent | `StarAgent-Cpp` | `cpp` |

平台差异通过 `Node.OSKind` / `Node.Architecture` 与 `ProductPackage.OSKind` / `Architecture` 表达，不要把 OS/架构拼进 `ProductCode`。服务端登录会校验节点原有产品编码，已注册节点不得随意更换该值。

---

## 3. 端点

> 以下响应示例均为 `data` 载荷；实际响应可能被 §2.3 的 `{code,data,msg}` 包装，客户端必须先解包。

### 3.1 登录 `POST /node/login`

请求体（`LoginInfo`）：

```json
{
  "Code": "可选，已注册节点的编码",
  "Secret": "可选，已注册节点的密钥",
  "ProductCode": "StarAgent-Go",
  "ClientId": "10.0.0.8@1234",
  "Project": "可选，项目名",
  "Node": {
    "Version": "1.0.2026.1017",
    "Compile": "2026-10-17 08:00:00",
    "OSName": "Ubuntu",
    "OSVersion": "24.04",
    "Architecture": "X64",
    "MachineName": "web-01",
    "UserName": "root",
    "IP": "10.0.0.8",
    "Gateway": "10.0.0.1",
    "Dns": "223.5.5.5",
    "ProcessorCount": 8,
    "Memory": 17179869184,
    "AvailableMemory": 8589934592,
    "FreeMemory": 4294967296,
    "TotalSize": 107374182400,
    "AvailableFreeSpace": 53687091200,
    "DriveSize": 107374182400,
    "DriveInfo": "/[ext4]=50G/100G",
    "Product": "Optional",
    "Vendor": "Optional",
    "Processor": "Optional",
    "UUID": "hardware-uuid",
    "MachineGuid": "machine-guid",
    "SerialNumber": "serial",
    "Board": "board",
    "DiskID": "disk-id",
    "Macs": "AA-BB-CC-DD-EE-FF",
    "InstallPath": "/opt/staragent",
    "Runtime": "go1.24.0",
    "Framework": "go1.24.0",
    "TimeZone": "Asia/Shanghai",
    "Time": "2026-10-17 10:00:00",
    "Plugins": ["agent:go", "cap:exec", "cap:upgrade=false"]
  }
}
```

响应体（`LoginResponse`）：

```json
{
  "Code": "服务端生成的节点编码，仅在首次注册/编码变化时返回",
  "Secret": "服务端生成的节点密钥，仅在首次注册/密钥变化时返回",
  "Name": "节点名称",
  "Token": "JWT",
  "Expire": 7200,
  "Time": 0,
  "ServerTime": 1760000000000
}
```

约定：

- `Code`/`Secret` 为空时表示动态注册；服务端根据 `Node` 硬件信息计算编码并下发。
- 原生 Agent 必须持久化响应中的 `Code`/`Secret`；后续登录携带它们。
- 服务端可能返回新的 `Code`/`Secret`（编码规则变化），收到后覆盖本地值。
- `Expire` 单位为秒。
- 登录失败返回 4xx/5xx；自动注册被禁止时不要反复重试，记录日志并退出或等待人工配置。

### 3.2 注销 `POST /node/logout?reason=...`

- 请求体为空；`reason` 做 URL 编码。
- 响应为 `LoginResponse`（通常只有 `Name`）。
- 注销失败不阻塞进程退出。

### 3.3 心跳 `POST /node/ping`

请求体（`PingInfo`，继承 `PingRequest`）：

```json
{
  "Time": 1760000000000,
  "IP": "10.0.0.8",
  "Gateway": "10.0.0.1",
  "Dns": "223.5.5.5",
  "Macs": "AA-BB-CC-DD-EE-FF",
  "DriveInfo": "/[ext4]=50G/100G",
  "Framework": "go1.24.0",
  "Processes": "nginx,dotnet",
  "ProcessCount": 120,
  "Memory": 17179869184,
  "AvailableMemory": 8589934592,
  "FreeMemory": 4294967296,
  "TotalSize": 107374182400,
  "AvailableFreeSpace": 53687091200,
  "CpuRate": 12.5,
  "Temperature": 0,
  "Battery": 0,
  "Signal": 0,
  "UplinkSpeed": 1024,
  "DownlinkSpeed": 2048,
  "Uptime": 86400,
  "Delay": 25,
  "TcpConnections": 42,
  "TcpTimeWait": 3,
  "TcpCloseWait": 1,
  "IntranetScore": 0.98,
  "InternetScore": 0.95,
  "SystemLoad": 0.8,
  "DiskIOPS": 120,
  "DiskActiveTime": 5.2,
  "GatewayLatency": 1,
  "GatewayLossRate": 0,
  "DnsLatency": 8,
  "DnsLossRate": 0,
  "ServerLatency": 25,
  "ServerLossRate": 0
}
```

响应体（`PingResponse`）：

```json
{
  "Time": 0,
  "ServerTime": 1760000000000,
  "Period": 60,
  "Token": "可选，即将过期时下发新令牌",
  "NewServer": "可选，服务器迁移地址",
  "Commands": [
    {
      "Id": 123,
      "Command": "node/synctime",
      "Argument": null,
      "StartTime": "0001-01-01 00:00:00",
      "Expire": "0001-01-01 00:00:00",
      "TraceId": null,
      "Status": 0
    }
  ]
}
```

约定：

- `Period` 单位为秒；`0` 表示使用默认 60 秒。
- `NewServer` 非空时：注销旧服务器、切换地址、重新登录；切换失败保留旧地址并重试。
- `Commands` 可能为空或缺失；存在时按第 5 节处理。
- 指标缺失时填 `0`；不要因为采集失败而跳过心跳。

### 3.4 升级检查 `POST /node/upgrade?channel=Release`

- `channel` 可选，默认 `Release`。
- 响应为 `UpgradeInfo` 或空：

```json
{
  "Version": "1.1.2026.1101",
  "Source": "https://.../staragent-go-linux-amd64.zip",
  "FileHash": "md5",
  "FileSize": 12345678,
  "Preinstall": "可选，更新前命令",
  "Executor": "可选，更新后启动命令",
  "Force": false,
  "Description": "可选"
}
```

首版原生 Agent **不执行自升级**：收到非空响应时记录日志并上报事件 `Upgrade`（内容为可用版本），由运维手动更新。服务端必须按 `ProductCode` 过滤，避免把 .NET 包发给原生 Agent。

### 3.5 事件上报 `POST /node/postevents`

请求体为数组：

```json
[
  {
    "Time": 1760000000000,
    "Type": "info",
    "Name": "Startup",
    "Remark": "agent started"
  }
]
```

响应为写入条数（整数）。`Type` 使用 `info` / `alert` / `error`。事件先入内存队列，按批上报；失败保留并退避重试，队列上限建议 1000 条。

### 3.6 命令回复 `POST /node/commandreply`

```json
{
  "Id": 123,
  "Status": 2,
  "Data": "命令输出或结果说明"
}
```

`Status` 取值：

| 值 | 名称 | 含义 |
|---:|------|------|
| 0 | 就绪 | 已收到，尚未执行 |
| 1 | 处理中 | 延迟执行已安排 |
| 2 | 已完成 | 执行成功 |
| 3 | 取消 | 已过期或拒绝执行 |
| 4 | 错误 | 执行失败 |

约定：

- 回复使用登录令牌，服务端会校验命令归属。
- `Data` 建议截断到 64KB；命令输出过大时保留头尾。
- 回复失败重试 3 次；仍失败记录本地日志。

### 3.7 WebSocket 通知 `WS /node/notify`

握手：

```http
GET /node/notify HTTP/1.1
Upgrade: websocket
Connection: Upgrade
Authorization: Bearer <Token>
```

帧格式：

- 文本帧 `Ping`（4 字节）：收到后回复文本帧 `Pong`。
- 文本帧 `Pong`（4 字节）：仅更新最后收到 Pong 的时间。
- 其它文本帧：JSON `CommandModel`（见第 5 节），执行后通过 HTTP `Node/CommandReply` 回复。
- 二进制帧：首版忽略并记录日志。
- 关闭帧：正常关闭；客户端应重连。

客户端保活：

- 每个心跳周期发送 `Ping`。
- 连续 3 个周期未收到 `Pong` 判定死连接，主动关闭并重连。
- 服务端也可能发送 `Ping`，客户端必须回复 `Pong`。

---

## 4. 指标最小集合

| 字段 | 单位 | 说明 |
|------|------|------|
| `CpuRate` | % | 0~100 |
| `Memory` | 字节 | 总内存 |
| `AvailableMemory` | 字节 | 可用内存 |
| `FreeMemory` | 字节 | 空闲内存（Linux 可为缓存前空闲值） |
| `TotalSize` | 字节 | 安装目录所在磁盘总大小 |
| `AvailableFreeSpace` | 字节 | 安装目录所在磁盘可用空间 |
| `DriveInfo` | 字符串 | `挂载点[文件系统]=可用/总量`，逗号分隔 |
| `UplinkSpeed` / `DownlinkSpeed` | 字节/秒 | 采样周期内的平均速率 |
| `ProcessCount` | 个 | 进程总数 |
| `Processes` | 字符串 | 进程名去重列表，逗号分隔；建议过滤系统进程 |
| `Uptime` | 秒 | 系统运行时长 |
| `SystemLoad` | 浮点 | Linux Load1 / Windows 处理器队列长度 |
| `GatewayLatency` / `DnsLatency` / `ServerLatency` | 毫秒 | 无采集能力时填 0 |
| `GatewayLossRate` / `DnsLossRate` / `ServerLossRate` | 0~1 | 丢包率 |

平台采集失败时填 0，不要省略整个心跳。

---

## 5. 命令模型与执行

`CommandModel`：

```json
{
  "Id": 123,
  "Command": "bash",
  "Argument": "{\"cmd\":\"uptime\",\"timeout\":30000}",
  "StartTime": "0001-01-01 00:00:00",
  "Expire": "0001-01-01 00:00:00",
  "TraceId": null,
  "Status": 0
}
```

执行规则：

1. **去重**：按 `Id` 缓存至少 1 小时；重复命令直接忽略（不重复回复）。
2. **过期**：`Expire` 有效且早于当前 UTC 时间 → 回复 `Status=取消`。
3. **延迟**：`StartTime` 晚于当前 UTC 时间 → 回复 `Status=处理中`，后台等待到点执行；长延迟分段等待，避免整数溢出。
4. **并发**：建议最多 4 个命令并发；`bash`/`cmd` 默认串行或限制并发，避免资源耗尽。
5. **超时**：默认 30 秒，可由参数覆盖，最大 300 秒；超时后终止进程树并回复 `Status=错误`。
6. **输出**：合并 stdout/stderr，截断到 64KB；保留退出码。
7. **未知命令**：回复 `Status=错误`，`Data="不支持命令: <name>"`。

首版内置命令：

| 命令 | 平台 | 说明 |
|------|------|------|
| `bash` | Linux/macOS | `Argument` 为字符串，或 `{"cmd":"...","timeout":30000}` |
| `cmd` | Windows | 同上 |
| `node/synctime` | 全平台 | 可选参数为时间字符串；无参数时使用服务端时间 |
| `node/setchannel` | 全平台 | 修改更新通道并持久化 |
| `node/restart` | 全平台 | 延迟 1 秒后重启自身进程/服务 |
| `node/reboot` | 全平台 | 首版不实现，回复不支持 |
| `deploy/*` | — | 首版不实现，回复不支持 |
| `framework/install` | — | 首版不实现，回复不支持 |

安全默认：

- `AllowExec=false`：`bash`/`cmd` 一律回复 `Status=取消`，`Data="命令执行未启用"`。
- 开启后仍受 `AllowedCommands` 白名单约束；白名单为空表示允许内置命令。
- 禁止把未经过滤的 `Argument` 拼接到日志或 shell 外层命令；按平台原样执行，但限制长度（建议 8KB）。

---

## 6. 节点身份与能力

### 6.1 身份字段

服务端根据以下字段计算/匹配节点编码：

- `UUID`：硬件 UUID（Linux DMI product_uuid / Windows Win32_ComputerSystemProduct.UUID / macOS IOPlatformUUID）
- `MachineGuid`：系统安装标识（Linux `/etc/machine-id` / Windows `MachineGuid` 注册表）
- `MACs`：物理网卡 MAC，大写、`-` 分隔、逗号分隔多个，排序去重
- `DiskID`：系统盘序列号
- `SerialNumber`：设备序列号
- `Board`：主板标识

要求：

- 同一台机器多次启动必须得到稳定值；取不到时留空，不要用随机数。
- 网卡变化时 `MACs` 会变化，服务端会按匹配度复用旧节点；Agent 不要自行生成编码。
- Android/iOS 使用应用/设备允许的稳定标识，取不到时使用安装时生成的本地 UUID 并持久化。

### 6.2 能力声明

首版通过 `NodeInfo.Plugins` 上报能力标签：

- `agent:go` / `agent:rust` / `agent:cpp`
- `cap:exec`（是否支持命令执行）
- `cap:upgrade=false`（首版固定 false）
- `cap:deploy=false`
- `os:linux` / `os:windows` / `os:macos` / `os:android` / `os:ios`

服务端可将这些标签用于展示或后续策略；首版不依赖它们做关键判断。

---

## 7. 配置与持久化

建议配置文件名 `staragent.json`：

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

约定：

- `Code`/`Secret` 由登录响应写入，文件权限 `0600`。
- 配置支持环境变量覆盖（建议前缀 `STARAGENT_`）。
- 日志文件放 `DataDir/logs`，按天/大小轮转，默认保留 7 天。
- 事件队列可落盘到 `DataDir/events.json`，启动时恢复。

---

## 8. 平台服务形态

| 平台 | 形态 | 说明 |
|------|------|------|
| Linux | systemd | `Restart=always`，`KillSignal=SIGTERM`，服务用户可配置 |
| OpenWrt | procd | `/etc/init.d/staragent`，`start_service`/`stop_service` |
| Windows | Windows Service | 服务名 `StarAgentNative`，支持 install/uninstall/start/stop |
| macOS | launchd | `~/Library/LaunchAgents` 或 `/Library/LaunchDaemons` |
| Android | Foreground Service | 常驻通知；受电池优化/厂商策略影响，不保证 100% 在线 |
| iOS | 宿主 App 内 SDK | 不常驻、不执行任意 Shell、不做进程守护 |

---

## 9. 安全要求

1. 生产环境必须校验 TLS 证书；`TlsVerify=false` 仅用于测试。
2. 不在日志中输出 `Secret`、`Token`、命令完整参数中的敏感信息。
3. 命令执行默认关闭；开启后使用白名单、超时、输出上限和审计日志。
4. 升级包必须校验哈希；首版不自动升级，但手动安装脚本也应校验。
5. 服务进程使用最小权限账户运行；Android/iOS 遵守平台权限模型。
6. 任何来自服务端的路径/文件名都不得直接拼接为本地路径执行，需做白名单与规范化校验。

---

## 10. 验收清单

- [ ] 首次登录自动注册并持久化 Code/Secret
- [ ] 二次登录复用身份
- [ ] 心跳维持在线，指标字段可被服务端解析
- [ ] WebSocket 命令可执行并 HTTP 回复
- [ ] 心跳响应中的 Commands 可执行（HTTP 降级）
- [ ] 重复/过期/延迟命令处理正确
- [ ] 服务端断连/重启后自动重连
- [ ] 事件失败重试
- [ ] 未知命令返回不支持
- [ ] 命令执行默认关闭，开启后受白名单/超时/输出限制
