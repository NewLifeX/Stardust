# StarAgent.Native

StarAgent 的非 .NET 原生实现集合，目标是以轻量二进制接入 StarServer，完成节点注册、心跳、指标采集、事件上报与远程命令执行。

| 实现 | 状态 | 目标平台 |
|------|------|----------|
| Go | 参考实现，已通过 6 项一致性测试 | Linux x64/ARM、Windows、macOS、OpenWrt |
| Rust | 已通过 6 项一致性测试 | Linux musl、Windows、macOS |
| C++ | 已通过 6 项一致性测试与真实服务端登录/上线验证；Windows 本机构建验证 | Linux、Windows、macOS、OpenWrt |

> C 版暂缓：C++ 已覆盖 C 的目标平台，且内存安全与生态库更合适。仅在出现无 C++ 工具链的特定嵌入式目标时再评估。

## 已验证

- Go：`go test ./...`、`go build ./cmd/staragent`、`CGO_ENABLED=0` 交叉编译（linux/amd64、linux/arm64、linux/arm、windows/amd64、windows/arm64、darwin/amd64、darwin/arm64）
- Rust：`cargo test`、`cargo build` 通过；Windows 本机二进制可运行
- C++：Windows 本机 `cmake` 构建通过，`staragent_tests` 50/50 项检查通过
- 一致性测试：Go / Rust / C++ 三种实现均对假 StarServer 运行 `--scenario all`，各自 `6 passed, 0 failed`
- 真实服务端：Go / Rust / C++ Agent 均在真实 Kestrel StarServer 上完成登录、自动注册、Code/Secret 持久化与在线状态验证
- 公网 StarServer：三种实现均以 `once` 模式连接 `http://star.newlifex.com:6600`，完成登录与心跳
- 服务端：`ProductRelease` 按 `ProductCode` 过滤、`ProductPackage` 按 `OSKind/Architecture` 匹配、原生 Agent 跳过 `framework/install`；新增 23 项服务端测试通过

## 构建产物

```powershell
pwsh ./StarAgent.Native/packaging/build.ps1
```

默认按 `Bin/Agent/native/<语言>/<平台-架构>/` 输出，并生成 `BUILD-INFO.md`（文件大小与 SHA256）。支持矩阵与跨平台构建要求见 [构建与发布说明](packaging/README.md)。

| 实现 | ProductCode | 默认产物 |
|------|-------------|----------|
| Go | `StarAgent-Go` | 7 个平台目标 |
| Rust | `StarAgent-Rust` | 本机 Windows x64；Linux/macOS 需对应工具链 |
| C++ | `StarAgent-Cpp` | 本机 Windows x64；Linux/macOS 需对应工具链 |

## 文档

- [协议规范](spec/protocol.md) — 唯一事实源
- [JSON Schema](spec/schemas/node-agent.schema.json)
- [样例与测试向量](spec/vectors/basic.json)
- [构建与发布说明](packaging/README.md) — ProductCode、输出目录、平台矩阵、升级包匹配

## 首版能力

- 登录/注销/心跳/令牌续期/服务器迁移
- WebSocket 命令通道 + HTTP 心跳命令降级
- 事件上报、命令回复、指数退避重连
- CPU/内存/磁盘/网络/进程/运行时长等基础指标
- `bash`/`cmd`/`node/synctime`/`node/setchannel`/`node/restart`
- systemd / launchd / Windows Service / OpenWrt procd
- 移动端由 .NET 移动代理覆盖（`StarAgent.Mobile` / `StarAgent.Android` / `StarAgent.iOS`，不在本目录）；`ProductCode=StarAgent-Mobile`，产物在 `Bin/Agent/mobile/`，构建与冒烟见各项目下的 `pack-android.ps1`、`run-android.ps1`、`run-ios.sh`

### 系统与硬件信息

已对齐标准 StarAgent 在节点页展示的主要字段：

- 系统：操作系统名称/版本/种类、架构、机器名、用户名、全部 IPv4、网关+MAC、DNS、时区、DPI、分辨率、C 运行时
- 硬件：CPU、内存、磁盘、产品名、制造商、处理器、GPU、UUID、MachineGuid、序列号、主板、磁盘序列号、物理网卡 MAC
- 指标：CPU 使用率、内存、磁盘容量、网络速率、进程数、运行时长、负载

原生 Agent 的 `Runtime` / `Framework` 上报自身语言运行时（Go/Rust/C++），不枚举本机 .NET Framework 集合。

首版**不包含**：进程守护、远程发布（`deploy/*`）、自身升级、Web 管理面板、插件、防火墙、DNS、截屏。

## 安全默认

- 远程命令执行默认关闭（`AllowExec=false`）。
- 开启后仍受 `AllowedCommands` 白名单、超时和输出上限约束。
- 生产环境必须校验 TLS 证书。

## 目录

```
StarAgent.Native/
├── spec/          # 协议规范、JSON Schema、测试向量
├── conformance/   # 假 StarServer + 语言无关一致性测试
├── go/            # Go 实现
├── rust/          # Rust 实现
├── cpp/           # C++ 实现
└── packaging/     # 统一构建脚本与发布说明
```
