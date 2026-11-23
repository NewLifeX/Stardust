# StarAgent 启动性能基准

同一套测试程序，比较 StarAgent 的 .NET 四种发布形态与三种原生实现，在 Windows、WSL 与多台 Linux 机器上的启动开销。

## 测试矩阵

| 变体 | 实现 | 测量命令（空载） | 测量命令（完整初始化） |
|------|------|------------------|------------------------|
| `dotnet-universal` | .NET 框架依赖 | `dotnet StarAgent.dll --version` | `... -status` |
| `dotnet-jit` | .NET JIT 自包含 | `./StarAgent --version` | `... -status` |
| `dotnet-r2r` | .NET R2R 自包含 | `./StarAgent --version` | `... -status` |
| `dotnet-aot` | .NET NativeAOT | `./StarAgent --version` | `... -status` |
| `go` | Go 原生 | `./staragent version` | — |
| `rust` | Rust 原生 | `./staragent version` | — |
| `cpp` | C++ 原生 | `./staragent version` | — |

机器清单在 [machines.json](machines.json)：Windows 本机、WSL、10.0.0.1、A2（linux-arm64）、sh14.newlifex.com。

## 指标

- **wall_ms**：进程创建到退出的墙钟时间，含 fork/exec 与运行时初始化。
- **cpu_ms**：内核统计的 user+sys CPU 时间。
- **rss_kb**：`getrusage` 报告的峰值 RSS（Linux）；Windows 取 `PeakWorkingSet64`。
- **包大小**：发布目录总大小；AOT 与原生按单个可执行文件计，调试符号不计。

Linux 端用 [bench-run.c](bench-run.c) 精确测量：fork 后由父进程 `wait4` 取 `ru_utime/ru_stime/ru_maxrss`，墙钟用 `CLOCK_MONOTONIC`。
Windows 端由 PowerShell 直接 `Process.Start`，读 `TotalProcessorTime` 与 `PeakWorkingSet64`。

## 前置条件

- Windows：.NET SDK（含 net10.0 目标）、Go、Rust、CMake + MSVC；WSL 内装 `dotnet-sdk-10.0`、`dotnet-sdk-aot-10.0`、`clang`、`lld`、`gcc-aarch64-linux-gnu`。
- Linux 构建机（linux-x64 与 linux-arm64 各一台）：`gcc/g++`、`cmake`、Rust（脚本会用 rsproxy 自动安装）、OpenSSL 头文件可选。
- 目标机免密 SSH（`root@...`），以及 `tar`、`timeout`、`coreutils`。
- C++ 基准构建使用 `-DSTARAGENT_WITH_TLS=OFF`（避免 Windows 侧缺少 OpenSSL 导致构建不一致），WebSocket 支持保持开启。

## 用法

```powershell
# 全量：构建 → 部署 → 采样 → 汇总
pwsh Tools/Benchmarks/staragent-bench.ps1

# 只测部分机器，减少采样次数
pwsh Tools/Benchmarks/staragent-bench.ps1 -Machines A2,sh14.newlifex.com -Iterations 10

# 复用已有产物与已部署文件，只重跑采样
pwsh Tools/Benchmarks/staragent-bench.ps1 -SkipBuild -SkipDeploy

# 只构建，不运行
pwsh Tools/Benchmarks/staragent-bench.ps1 -SkipRun
```

产物与结果都在 `.tmp/bench/`：

- `artifacts/`：各变体发布/编译产物
- `remote/`：Windows 本机部署目录
- `results/raw-*.csv`：逐次原始数据
- `results/summary-*.md`：中位数汇总

## 口径说明

- 通用包使用目标机已安装的 .NET 10 运行时，不同机器的补丁版本可能不同（10.0.12 / 10.0.14 / 10.0.15），这是部署现实的一部分。
- `--version` / `version` 是各界面的空载启动路径，用于横向比较运行时与二进制加载成本；`-status` 额外做配置、日志、宿主检测，仅 .NET 有对应实现。
- 绝对毫秒不可跨 OS/架构直接比较，优先看同一台机器内的相对收益。
- 采样前每个变体预热一次，随后连续运行 N 次（默认 15）取中位数；不清页缓存，测的是热缓存下的启动成本。
