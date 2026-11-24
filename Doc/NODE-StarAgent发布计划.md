# NODE-StarAgent 发布计划（AOT + any）

> 版本：v1.0 | 日期：2026-11-24
> 适用：StarAgent 3.7.2026.1124、.NET 10、NativeAOT
> 关联：[NODE-StarAgent-AOT适配](NODE-StarAgent-AOT适配.md) | [NODE-StarAgent-多语言性能对比](NODE-StarAgent-多语言性能对比.md) | [服务端发布方式评估](服务端发布方式评估.md)

## 1. 结论

- **主力 RID 用 AOT**：win-x64 / linux-x64 / linux-arm64 发布 NativeAOT 单文件包，启动、内存、包体都最优。
- **其它目标用 any**：其余架构与旧 .NET 目标继续发布框架依赖包（staragent10/90/80/…），由目标机安装对应 .NET 运行时。
- **跨形态升级必须显式支持**：any↔AOT 会在同一目录里切换“托管 DLL 入口”和“原生可执行文件入口”，本次一并修正文件备份、服务入口、执行权限与自愈逻辑。
- **默认档 AOT 保留完整功能与全部语言**；Slim 档（中英词表 + 无堆栈）的 zip 只再省 0.3~1.3 MB，不建议作为默认，按设备档位选择。

## 2. 发布矩阵

| 形态 | 目标 | 产物 | 运行时要求 | 说明 |
|------|------|------|------------|------|
| AOT | win-x64 | `staragent10-win-x64-aot.zip` | 无 | 单文件 `StarAgent.exe` |
| AOT | linux-x64 | `staragent10-linux-x64-aot.zip` / `.tar.gz` | glibc | 单文件 `StarAgent` |
| AOT | linux-arm64 | `staragent10-linux-arm64-aot.zip` / `.tar.gz` | glibc | 单文件 `StarAgent` |
| any | net10.0 | `staragent10.zip` / `.tar.gz` | .NET 10 | 跨平台框架依赖包，含 Linux 启动 shim |
| any | net9.0 / net8.0 / net7.0 / net6.0 | `staragent90/80/70/60.zip` / `.tar.gz` | 对应 .NET | 旧目标继续可用 |
| any | net461 / net45 | `staragent461/45.zip` | .NET Framework | Windows 旧系统 |

> “any” 指不区分 RID 的框架依赖包；同一台机器上只要平台同时提供该 RID 的 AOT 包与 any 包，匹配逻辑会优先选择精确 OS/架构的 AOT 包。Linux tar.gz 内的文件统一以 755 打包，zip 解压后由升级逻辑补执行权限。

## 3. 构建

### 3.1 一键脚本

```powershell
# 全量：AOT 三 RID + 全部 any 目标（旧 pack-agent.bat 已改为转发到此脚本）
pwsh Tools/pack-agent.ps1

# 只发 AOT（例如紧急修复）
pwsh Tools/pack-agent.ps1 -SkipAny

# 只发 any（旧目标）
pwsh Tools/pack-agent.ps1 -SkipAot

# AOT Slim 档
pwsh Tools/pack-agent.ps1 -SlimAot
```

输出到 `Bin/Agent/`：zip/tar.gz 与 `BUILD-INFO.json`（包名、形态、RID、OS/架构、目标运行时、zip 大小/SHA256，以及 tar 文件名/大小/SHA256），可直接用于平台批量登记发布包。

### 3.2 前置条件

- Windows：.NET SDK（net10.0 目标）、PowerShell 7、`tar.exe`。
- WSL（linux-x64 / linux-arm64 交叉编译）：`dotnet-sdk-10.0`、`dotnet-sdk-aot-10.0`、`clang`、`lld`、`zlib1g-dev`、`gcc-aarch64-linux-gnu`、`binutils-aarch64-linux-gnu`。
- Linux AOT 的 glibc 兼容性由构建机决定：**在支持的最老发行版上构建**（当前 WSL Ubuntu 24.04 / glibc 2.39）；如果还要支持 Debian 11、Ubuntu 20.04，应在对应容器或老发行版里构建，或改用 musl。

### 3.3 AOT 体积

| RID | 默认档 exe | Slim 档 exe | 默认档 zip | Slim 档 zip |
|-----|------------:|-----------:|-----------:|-----------:|
| win-x64 | 16.1 MB | 14.8 MB | 7.4 MB | 6.9 MB |
| linux-x64 | 18.0 MB | 17.5 MB | 8.3 MB | 8.0 MB |
| linux-arm64 | 18.5 MB | 18.0 MB | 8.0 MB | 7.7 MB |

已在 `NODE-StarAgent-AOT适配` §7.3 验证过的开关：`OptimizationPreference=Size`、`IlcFoldIdenticalMethodBodies`、`UseSizeOptimizedLinq`、`DebuggerSupport=false`、`MetadataUpdaterSupport=false`；Slim 额外使用语言白名单与 `StackTraceSupport=false`。

本次实测的额外开关（win-x64）：

| 开关 | 实测收益 | 结论 |
|------|----------|------|
| `EventSourceSupport=false` + `HttpActivityPropagationSupport=false` + `InvariantGlobalization=true` + `EnableUnsafeBinaryFormatterSerialization=false` | 14.81 → 14.55 MB（-0.26 MB，zip -0.12 MB） | 收益小，失去 EventSource 诊断与区域化格式，不默认开启 |
| `IlcGenerateCompleteTypeMetadata=false` | 14.81 → 14.81 MB（无变化） | 可能破坏反射/序列化，不采用 |
| `UseSystemResourceKeys=true` | 约 -0.03 MB | 异常消息变资源键，现场排查看不到原文，不采用 |

**结论**：Slim 是唯一有意义的体积杠杆，但 Linux 只省 3%；默认档保留完整语言与堆栈，Slim 仅用于存储特别紧张的 Windows 边缘设备。

## 4. 平台登记

| 字段 | AOT | any |
|------|-----|-----|
| ProductCode | `StarAgent` | `StarAgent` |
| TargetRuntime | `10` | `10`（net10）或对应主版本 |
| OSKind | `Windows` / `Linux` | `Any` |
| Architecture | `X64` / `Arm64` | `Any` |

匹配规则（`ProductRelease.MatchPackage`）先按 OS/架构过滤，再按 TargetRuntime 匹配，精确 OS/架构优先于通用包。因此：

- 主 RID 节点：同时有 AOT 与 any 时，自动选 AOT。
- 非主 RID 节点（win-arm64、osx、linux-arm、musl 等）：只命中 any。
- **要回退到 any**：禁用/下架对应 RID 的 AOT 包，或调整节点通道；匹配不会主动从 AOT 降到 any。

## 5. 跨形态升级

### 5.1 路径矩阵

| 起点 → 终点 | Windows | Linux |
|-------------|---------|-------|
| any → AOT | 基类把 `StarAgent.exe` 改名 `.del`，复制原生 exe；服务入口仍是 `StarAgent.exe` | `StarUpgrade` 备份无扩展名 `StarAgent`；复制原生文件；服务入口从 `dotnet StarAgent.dll` 切到 `StarAgent` |
| AOT → any | 基类把原生 `StarAgent.exe` 改名 `.del`，复制 apphost + DLL；服务入口仍是 `StarAgent.exe` | `StarUpgrade` 备份原生 `StarAgent`；复制 DLL/runtimeconfig；服务入口切到 `dotnet StarAgent.dll` |
| any → any | 基类覆盖 DLL/JSON，服务入口不变 | 同左 |
| AOT → AOT | 基类覆盖 exe，服务入口不变 | 同左 |

### 5.2 已落实的机制

1. **AOT 不再禁用自动更新**：`MyStarClient` 允许下载原生包；若平台仍下发托管包，则按跨形态升级切回 JIT。
2. **无扩展名入口备份**：`StarUpgrade` 在 Linux 上先把 `StarAgent` 改名为 `.old`（基类 `Update` 开头会删除所有 `.del`，直接用 `.del` 会被当场清掉）；更新成功后提升为 `.del` 供 `Rollback()` 恢复，更新失败立即从 `.old` 还原。
3. **服务入口切换**：`MyStarClient.Restart` 在强制更新重启前、以及升级完成后的 `ReRegisterServiceForCurrentForm`，都会按新形态改写 systemd `ExecStart`（`dotnet StarAgent.dll` ↔ 原生 `StarAgent`）。
4. **启动自愈**：`UpgradeHealer` 检测服务入口形态与目录实际形态不一致时自动重装服务；同时修复自包含 apphost 被误判为“过期 apphost”而替换成 shim 的旧问题。
5. **执行权限**：`Upgrade.Run` 在 Linux 启动原生文件前 `chmod +x`；`tar.gz` 以 755 权限打包，zip 解压后由启动逻辑补权限。
6. **冒烟与回滚**：沿用既有 3 秒冒烟 + `.del` 回滚；原生与托管形态都适用。

### 5.3 仍需注意

- **外部插件**：AOT 不能加载 `Plugins` 目录的新托管程序集；要么把固定插件编译进代理，要么该节点继续用 R2R/JIT。
- **glibc**：AOT 二进制绑定构建机 glibc；老系统必须用老环境构建。
- **平台自动升级**：AOT 包必须按 RID 上传；平台不要把 linux-x64 的包发给 arm64 节点（现有 OS/架构匹配已拦截）。
- **回退通道**：AOT 出问题时，最稳妥是保留上一版 any 包并禁用 AOT 包；节点会按 any 包升级回 JIT。
- **配置与数据**：升级只覆盖程序文件，`Config/`、`Data/`、`Log/`、`Update/` 保留。

## 6. 验证清单

| 用例 | 步骤 | 通过标准 |
|------|------|----------|
| any → AOT（Windows 服务） | 安装 any 版本 → 平台发布 win-x64 AOT → 等待/触发升级 | 服务重启后运行原生 exe，节点在线，版本更新 |
| any → AOT（Linux systemd） | 同上 | `ExecStart` 切到原生 `StarAgent`，节点在线 |
| AOT → any（Windows 服务） | 安装 AOT → 下架 AOT/发布 any → 触发升级 | 服务运行 `StarAgent.exe`（apphost），节点在线 |
| AOT → any（Linux systemd） | 同上 | `ExecStart` 切到 `dotnet StarAgent.dll`，节点在线 |
| 冒烟失败回滚 | 用损坏的新包触发升级 | 旧文件恢复，旧进程继续运行，节点不掉线 |
| 权限 | Linux 上 zip 解压后升级 | 原生文件可执行，服务能启动 |
| 插件 | AOT 节点加载外部插件 | 预期不支持；日志明确提示，或用内置插件/R2R |
| 版本 | AOT/any 登录上报版本 | 与平台产品版本一致，升级判定正常 |

### 6.1 已完成验证

- **文件层**：`StarUpgradeTests` 覆盖 Linux 原生入口 `.old` 备份、基类 `DeleteBackup` 不会误删、更新成功后提升 `.del`、更新失败还原，以及 any→AOT、AOT→any 两种方向的 `Update + Rollback`。
- **服务入口**：WSL + systemd 实测 `dotnet StarAgent.dll -s` ↔ 原生 `StarAgent -s` 互切；启动自愈把 `ExecStart` 从 `dotnet ...dll -s` 改成 `/tmp/agent-any/StarAgent -s`，再切回 `dotnet ...dll -s`，两次服务都能正常启动。
- **真实心跳**：WSL、10.0.0.1、A2、sh14、Windows 均用 `-run`/`once` 连 `http://star.newlifex.com:6600` 完成登录 + 首次心跳；AOT Slim 包在 WSL x64 与 A2 arm64 上均验证通过。

## 7. 分阶段落地

1. **试点**：PC（win-x64）、A2（linux-arm64）、sh14/10.0.0.1（linux-x64）先发 AOT；保留 any 包作为回退。
2. **观察**：一周内关注节点在线率、升级成功率、内存/启动、异常日志；重点看 any↔AOT 切换的节点历史。
3. **扩大**：主 RID 全量切 AOT；非主 RID 保持 any。
4. **收口**：确认回退通道可用后，评估是否给 Windows 渠道默认 Slim；Linux 两档只差 0.3 MB，维持默认档即可。
