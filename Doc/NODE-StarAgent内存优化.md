# StarAgent 内存优化

> 版本：v1.0 | 日期：2026-11-08
> 相关：[NODE-StarAgent架构](NODE-StarAgent架构.md) 第 8 节 | [多语言本地化](多语言本地化.md)

本文记录 .NET 版 StarAgent 在 Linux 边缘设备上的常驻内存优化：每项手段的原理、收益、代价，以及测量与运维要点。目标是让后来者不必重新试错一遍。

## 1. 效果总览

局域网真机（2026-11，StarAgent 3.7.2026.1108，框架依赖部署，Web 面板开启，稳态 RSS）：

| 设备 | 架构 | 总内存 | 优化前 | 优化后 | 降幅 | 启动耗时 |
|------|------|--------|--------|--------|------|----------|
| A6 | armv7 4 核 | 486 MB | 140 MB | 64 MB | -54% | 15 秒 → 28 秒 |
| A4 | arm64 2 核 | 481 MB | 173 MB | 90 MB | -48% | → 33 秒 |
| A2 | arm64 4 核 | 949 MB | 150 MB | 87 MB | -42% | → 14 秒 |
| C++ 原生代理（A4） | arm64 | — | — | 7 MB | — | — |

分三批完成：

| 批次 | 手段 | armv7 效果 |
|------|------|-----------|
| 一 | glibc 竞技场、空闲堆阈值、分层编译、HTTP/3、W^X 双映射 | 140 → 83 MB |
| 二 | 关闭预编译镜像（R2R） | 83 → 74 MB |
| 三 | 关闭 ICU | 74 → 64 MB |

只换启动时间，不换包体积：升级包还是同一批程序集，ICU 本就是系统共享库、从不随包分发。三批改完后 `StarAgent.dll` 只从 1.38 MB 涨到 1.40 MB。

## 2. 先讲怎么量，不然优化全是盲人摸象

RSS 会随 GC 周期上下跳，采一次就下结论必然误判。几条实测经验：

- **认准主进程**：`systemctl show StarAgent -p MainPID --value` 拿主 PID，不要用 `pgrep -x dotnet | head -1`——诊断脚本临时起的 dotnet 进程会被误抓。
- **等过一轮全 GC**：框架默认每 10 分钟做一次完整回收，冷启动 60 秒内的数字偏高（A2 就出现过早测 93 MB、稳态 87 MB）。等 10 分钟以上再取。
- **看两部分**：`VmRSS` 是总量，其中大量是**可回收的干净文件页**（内存紧张时内核会自动让位）；真正占死的是匿名页。A6 稳态 64 MB 里只有约 24 MB 是匿名页。
- **共享内存要单独看**：`Pss_Shmem` 属 tmpfs，内核不像干净文件页那样能回收，JIT 代码双映射就藏在这里（详见 3.5）。
- **逐段统计用 `pmap -x` / `smaps_rollup`**，按文件聚合能直接看出哪块该动。
- **确认没有残留的旧进程**：换版后如果新旧两个代理同时在跑，测出来的数没意义（本机实测踩到过一次）。

## 3. 逐项措施

### 3.1 glibc 竞技场：`MALLOC_ARENA_MAX=1`

**问题**：glibc 给每个线程准备独立的内存竞技场（64 位下每个竞技场预留 64 MB 虚拟空间，其中约 132 KB 是真实可写头，其余是 `PROT_NONE` 保护页），并在竞技场内缓存空闲块不归还。代理常驻 20 多个线程，实测光竞技场就吃掉两成常驻内存。

**做法**：必须在进程初始化前设定环境变量。运行时 `mallopt` 改不动——竞技场那时早就建好了。所以 `Program.TuneRuntime` 先用 `setenv` 设好参数，再 `execv` 原地重新加载自身镜像（PID 不变，systemd 无感知），新镜像起来后立刻 `unsetenv`，避免这些参数被它托管的业务进程继承。

三个坑：

1. 不能写 `DllImport("libc")`——Linux 上 `libc.so` 多是链接脚本，直接按库名加载会失败，得逐个试 `libc.so.6` / `libc.so` / `libc`。
2. `execv` 要求 `argv` 以 NULL 结尾，托管数组不会自动补，必须自己多分配一个位置。
3. 直接传 `/proc/self/exe` 会让进程名变成 `exe`（内核按传入路径的文件名命名），平台侧按进程名识别代理的逻辑会失准，所以要 `File.ResolveLinkTarget` 解析出真实路径。

**代价**：多一次进程启动（约几百毫秒）。一次性命令（`install`/`uninstall`）不重载，避免白付这笔开销。

### 3.2 空闲堆及时归还：`MALLOC_TRIM_THRESHOLD_` / `MALLOC_MMAP_THRESHOLD_`

glibc 默认的 trim/mmap 阈值偏大，空闲块长期留在进程里。设成 64 KB（`MALLOC_TRIM_THRESHOLD_=65536`、`MALLOC_MMAP_THRESHOLD_=65536`、`MALLOC_TOP_PAD_=0`）后，小块内存走 brk 并尽快归还。

`exec` 不可用的受限环境（如没挂 `/proc` 的容器）有兜底：改用 `mallopt` 在运行时设阈值，虽改不了竞技场，这两项仍生效。

### 3.3 关闭分层编译：`System.Runtime.TieredCompilation=false`

分层编译会同时持有 tier0（快速编译、质量差）与 tier1（慢速编译、质量好）两份代码。代理是长驻进程，启动后代码很快稳定，留两份只是浪费 JIT 代码页。

写在 `runtimeconfig.template.json` 里，不需重载。

### 3.4 关闭 HTTP/3：`System.Net.SocketsHttpHandler.Http3Support=false`

代理只走 HTTP/1.1。不关时 .NET 探测 HTTP/3 会连带装载 `System.Net.Quic`（arm64 实测 0.5~1.4 MB）。运行时用 `AppContext.SetSwitch` 关掉即可。

### 3.5 关闭代码双映射：`System.Runtime.EnableWriteXorExecute=false`

**最值钱也最反直觉的一项**。.NET 默认用 memfd 给 JIT 与预编译代码建一份"读写别名"，实现 W^X（写和执行不共存于同一映射）。这份别名落在 tmpfs，实测独占 **16~27 MB 共享内存**（`Pss_Shmem`）——而 tmpfs 页面内核不能像干净文件页那样回收。

关掉后代码走单映射，RSS 只降 1~2 MB，但 `Pss_Shmem` 归零，整体占用显著下降。

代价：失去 W^X 防护（JIT 代码页可写可执行）。对受控的边缘设备可接受。

### 3.6 关闭预编译镜像：`DOTNET_ReadyToRun=0`

默认直接执行程序集里的 R2R 预编译代码，等于把整份程序集文件页驻留。关掉后改为全量 JIT 一份本地代码，程序集文件页从 43 MB 降到 29 MB（`System.Private.CoreLib` 从 13 MB 降到 5.4 MB）。

**代价是启动变慢约 2 倍**（armv7 15 秒 → 28 秒）。判断：常驻内存按天算，启动只在上电或升级时发生一次，这笔交易对边缘设备划算。稳态执行**不会**变慢——全量 JIT 出来的是优化代码，比分层编译等 tier1 重编译还早。

通过环境变量注入（与 3.1 合并进同一次 `execv` 重载，不额外多一次启动）。

### 3.7 关闭 ICU：`System.Globalization.Invariant=true`

**ICU 是干什么的**：提供区域性数据——日期/数字格式、排序规则、日历，以及 `CultureInfo` 对象本身。它**不负责翻译**，界面显示什么语言跟它无关。

**为什么能关**：代理的界面文案来自自己的词表，语言判定改为依次读核心配置、浏览器 `Accept-Language`、系统 `LC_ALL`/`LC_MESSAGES`/`LANG`，最后才看托管区域性——全程字符串比对，不碰 ICU。实测在关闭 ICU 的进程里带 `Accept-Language: zh-CN` 依然返回中文面板。

**收益**：`libicudata` / `libicui18n` / `libicuuc` 与内部缓存共约 8 MB 不再落地。

**代价**：托管侧拿不到具名区域性，日志、配置注释、异常提示回落英文（`Localization.SetLanguage` 会捕获 `CultureNotFoundException` 并保持自动模式）。要中文日志的部署用环境变量 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0` 覆盖——**环境变量优先级高于 runtimeconfig**。

**放在哪一层很关键**：写在 `runtimeconfig.json` 里（见 `StarAgent.csproj` 的 `RuntimeHostConfigurationOption`，只对非 Windows RID 生成），而不是靠环境变量注入。原因是有些嵌入式发行版**根本没装 ICU**，.NET 在缺少 ICU 时会启动失败；走环境变量注入时，进程可能在重载镜像之前就先崩了，而 runtimeconfig 在运行时初始化阶段就生效，这类系统才真正能跑起来。

### 3.8 GC 参数（`runtimeconfig.template.json`）

```json
"System.GC.HeapCount": 1,
"System.GC.HeapHardLimit": 209715200,
"System.GC.ConserveMemory": 9,
"System.GC.RetainVM": false
```

单堆、保守回收、不保留虚拟内存。托管堆本来就小，这几项主要防止 GC 把进程内存撑高。

> 单独调 GC 的其他参数（`GCHeapHardLimit` 设 16/32/48 MB、`GCgen0size=1MB`、`gcConcurrent=0`）实测无收益甚至略升，不必再试。

## 4. 剩余构成与地板

优化后 armv7 设备（A6，64 MB）的构成，约 40 MB 是可被内核回收的干净文件页，真正独占的匿名页约 24 MB：

| 组成 | 约占用 | 能否再降 |
|------|--------|----------|
| 运行时本体（CoreLib 5.4 + coreclr 3.8 + clrjit 1.6 + JIT 代码） | 22 MB | 否，任何 .NET 进程的地板 |
| 星尘客户端栈（Http/TLS/Json/Remoting/追踪） | 9 MB | 否，心跳与命令通道必需 |
| BCL 其余（Xml/Cryptography/libstdc++/libc） | 8 MB | 仅能改配置格式等大改换取 |
| 自研程序集（NewLife.* + Stardust + StarAgent） | 4 MB | 否 |
| 匿名页（托管堆 + 原生堆 + 线程栈 + JIT 代码） | 24 MB | 已接近下限 |

再要显著下降只有两条路：

- **换原生代理**：C++ 版 7 MB，差一个数量级，但缺进程守护、远程发布、Web 面板、插件、自升级。
- **NativeAOT**：理论 20~30 MB，需各平台交叉 AOT 工具链并适配反射密集的配置与序列化代码，属大型改造。

## 5. 虚拟内存与线程

`VmSize`（VIRT）动辄三四百 MB，看着比 RSS 吓人得多，但它和 RSS 是两回事：VIRT 只是地址空间占位，不占物理页，不影响 OOM 判定，也不影响性能。真正要盯的是 RSS 与其中的匿名页。

不过既然看着吓人，还是把账算清楚（armv7 设备实测，VmSize 366 MB / RSS 65 MB）：

| 组成 | 约占用 | 说明 |
|------|--------|------|
| 线程栈 | **168 MB** | 21 个线程 × 8 MB（glibc 默认跟随 `ulimit -s`，本机 8192 KB） |
| 程序集文件映射 | 70 MB | 关闭 R2R 后已是压缩过的量 |
| GC 堆区预留 | 40 MB | 三段 `---p`，GC 的地址空间预留 |
| 原生堆与其他 | 90 MB | 含 glibc 竞技场（已限为 1 个）、主线程栈、.NET 运行时内部区 |

### 5.1 为什么要这么多个线程

线程数随负载在 21~27 之间浮动，不是固定值。构成大致是：

| 用途 | 数量 | 能否省 |
|------|------|--------|
| 主线程 + GC 后台 + Finalizer | 3~4 | 否 |
| ThreadPool worker | 随 CPU 核数扩展（4 核设备常见 4~8） | 否，心跳、命令处理、HTTP 面板都靠它 |
| ThreadPool IO 完成端口 | 1~2 | 否 |
| 定时器与服务巡检 | 2~3 | 否，60 秒心跳与 10 分钟升级检查必需 |
| 诊断与事件管道 | 2 | 可以，但实测 `DOTNET_EnableDiagnostics=0` 只少两个线程、RSS 不变，得不偿失 |

所以线程这块没有实质优化空间——它们都是干活的人，不是闲着占位。

### 5.2 如果确实想压 VIRT：降栈

线程栈是 VIRT 的大头。在服务单元里限制栈大小，实测：

| 配置 | VmSize | VmRSS |
|------|--------|-------|
| 默认（`ulimit -s` = 8 MB） | 366 MB | 65 MB |
| `LimitSTACK=4194304`（4 MB） | 288 MB | 62.8 MB |
| `LimitSTACK=2097152`（2 MB） | 244 MB | 62.7 MB |

```ini
# /etc/systemd/system/StarAgent.service.d/stack.conf
[Service]
LimitSTACK=4194304
```

**代价必须说清楚**：`RLIMIT_STACK` 是进程级限制，StarAgent 拉起的业务子进程会**继承**这个值。.NET 业务应用仍能拿到自己的托管栈（默认 1.5 MB），但 Java 等依赖大栈的运行时在 2 MB 下可能启动失败。

**结论**：VIRT 不影响真实资源，而改栈限制会把影响传到它守护的业务进程上，所以**默认不改**。确实需要压低 VIRT 的设备，从 4 MB 起步，改完重点回归它守护的业务进程能否正常启动。

> `DOTNET_DefaultStackSize` 对 ThreadPool 线程无效（实测 VmSize 无变化），只有 `RLIMIT_STACK` 起作用。

## 6. 试过但不采用

| 选项 | 结论 |
|------|------|
| `PublishReadyToRun` | 程序集体积涨 2~3 倍、升级包变大，内存基本持平（JIT 代码页减少被文件页抵消） |
| 关闭诊断端口（`DOTNET_EnableDiagnostics=0`） | 内存几乎无变化，只少两个线程，不值得牺牲可诊断性 |
| 定时 `malloc_trim(0)` | 页面虽有归还，WorkingSet 只从 83180K 变 83300K（噪声级），没有可回收的碎片 |
| GC 细调参数 | 托管堆本来就小，无收益 |
| 把 ICU 放回环境变量注入 | 有系统没装 ICU 时会先崩，改用 runtimeconfig |

## 7. 运维手册

| 想做什么 | 怎么做 |
|---------|--------|
| 恢复完整区域性（要中文日志） | 服务单元加 `Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0`，重启服务 |
| 恢复预编译镜像（换更快的启动） | 服务单元加 `Environment=DOTNET_ReadyToRun=1` |
| 压低虚拟内存（VIRT） | 服务单元加 `LimitSTACK=4194304`；注意该限制会传给被托管的业务进程，理由与风险见第 5 节 |
| 确认优化是否生效 | 看 `/proc/<pid>/environ` 是否有 `MALLOC_ARENA_MAX`、`DOTNET_ReadyToRun`；`pmap -x <pid> \| grep icu` 应无输出 |
| 换版后核对 | `systemctl show StarAgent -p MainPID --value` 与 `ps -eo pid,etimes,args \| grep StarAgent.dll` 必须只有一个进程 |

> ⚠️ **救援副本**：带固件的设备由 `newlifed` 看门狗守护代理，代理停止十几秒就会用固件内置的救援副本覆盖部署目录。换版时必须同时刷新救援副本，否则设备会悄悄回滚到老版本（实测 A6 曾这样退回 116 MB）。代理已接管这件事：`MyService.SyncRescueCopy` 在稳定运行 3 分钟后自动同步。

> ⚠️ **部署要整包**：升级时务必带上同一份发布产物里的全部程序集与 `runtimeconfig.json`。只替换 `StarAgent.dll` 会出现 `TypeInitializationException: Could not load file or assembly 'Stardust, Version=...'` 的崩溃循环。

## 8. 上游可优化项（跨仓库，未实施）

| 程序集 | 占用 | 装载来源 | 可行的改法 |
|--------|------|----------|-----------|
| `System.Linq.Expressions` | 2.1 MB | `NewLife.Reflection.DefaultReflect.RegisterAndCreate`，首次访问 `Config<T>.Current` 触发 | 对象工厂改为 `Activator.CreateInstance` 或缓存委托 |
| `System.Reflection.Metadata` + `System.Collections.Immutable` | 1.7 MB | 异常堆栈解析源码行号，首次带文件信息的 `StackTrace` 触发 | 记录异常时不再请求文件行号 |
| `System.Private.Xml` | 3.1 MB | `XmlConfigProvider` 读取 `*.config` | 配置文件改用 JSON（`FileConfigProvider` 按扩展名选解析器） |
