# NODE-StarAgent NativeAOT 适配

> 适用版本：StarAgent 3.7（net10.0）、NewLife.Core 12、NewLife.Remoting 4、NewLife.Agent 10.18
> 关联文档：[NODE-StarAgent内存优化](NODE-StarAgent内存优化.md) | [NODE-StarAgent架构](NODE-StarAgent架构.md)

## 1. 结论

.NET 10 版 StarAgent 已**全面支持 NativeAOT**，并把 JIT 版时代的两块硬阻塞（程序集反射、反射路由）在上游组件里彻底解决。四类目标设备实测通过：

| 设备 | 架构 | 宿主 | 原生包 | 启动 | RSS | 线程 | 登录/心跳 | Web 面板 | 本机 UDP RPC | 子服务管理 |
|------|------|------|--------|------|-----|------|-----------|----------|--------------|------------|
| PC（X5） | win-x64 | Windows 服务 | 16.4 MB 单文件 | ≈1 s | 32~45 MB | 15~19 | ✅ | ✅ | ✅ | ✅ |
| A2 | linux-arm64 | systemd | 19.0 MB 单文件 | 0.14 s | 36~40 MB | 18~22 | ✅ | ✅ | ✅ | ✅ |
| A4 | linux-arm64 | systemd | 19.0 MB 单文件 | — | 39~43 MB | 19~22 | ✅ | ✅ | ✅ | ✅ |
| A6 | linux-arm | systemd | 17.3 MB 单文件 | 0.25 s | 31~34 MB | 16~20 | ✅ | ✅ | ✅ | ✅ |

> 原生包为「默认档」，已开启 `OptimizationPreference=Size` 等体积开关，比首轮 19~20 MB 小 15%（Linux ARM 上约 4%）。
> 需要更小可用 Slim 档（win-x64 12.9 MB / arm64 15.4 MB / arm 13.5 MB），代价是只保留中英词表且异常无堆栈行号，见 §7。

对照 JIT 版（同机型历史数据：A6 73 MB / A4 95 MB / A2 88 MB，启动 8~16 秒），**常驻内存约降 50%~60%，启动从秒级十位数降到亚秒级**。

与旧结论（2026-11 初，A4 只有 29 MB 但插件与面板不可用）相比，本次把功能面补齐，代价是内存比“裸壳”高约 10 MB。

## 2. 发布方式

### 2.1 开关

`PublishAot` **不能**作为命令行全局属性传入（`-p:PublishAot=true` 会波及被引用库的 net45/net40 目标，直接报 `NETSDK1207`）。项目内用独立开关收敛到 net10.0。

日常发布建议用脚本，它已封装 RID、交叉编译参数与两档体积配置（用法见 §7.4）：

```powershell
pwsh Tools/Scripts/publish-aot.ps1 -Rid win-x64                       # 默认档
pwsh Tools/Scripts/publish-aot.ps1 -Rid win-x64 -Slim                 # Slim 档
```

等价的原始命令：

```powershell
# Windows（PC）
dotnet publish StarAgent/StarAgent.csproj -c Release -f net10.0 -r win-x64 `
  --self-contained true -p:StarAgentAot=true -p:UseLocalNewLifeSource=true `
  -p:StripSymbols=true -o .tmp/aot/win-x64
```

```bash
# WSL / Linux：linux-arm64（A2/A4）
dotnet publish StarAgent/StarAgent.csproj -c Release -f net10.0 -r linux-arm64 \
  --self-contained true -p:StarAgentAot=true -p:UseLocalNewLifeSource=true -p:StripSymbols=true \
  -p:CppCompilerAndLinker=clang -p:ObjCopyName=aarch64-linux-gnu-objcopy \
  -o /tmp/aot/arm64

# WSL / Linux：linux-arm（A6）
dotnet publish StarAgent/StarAgent.csproj -c Release -f net10.0 -r linux-arm \
  --self-contained true -p:StarAgentAot=true -p:UseLocalNewLifeSource=true -p:StripSymbols=true \
  -p:CppCompilerAndLinker=clang -p:LinkerFlavor=lld -p:ObjCopyName=arm-linux-gnueabihf-objcopy \
  -o /tmp/aot/arm
```

工具链（WSL Ubuntu 24.04）：`clang`、`lld`、`zlib1g-dev`、`gcc-aarch64-linux-gnu`、`gcc-arm-linux-gnueabihf`、`binutils-*`。

> **交叉编译要点**
> 1. 不能用 `aarch64-linux-gnu-gcc` 作链接器：.NET 10 的 ILC 会追加 `--target=`，只有 clang 认这个参数。
> 2. armv7 链接要用 `lld`（`-p:LinkerFlavor=lld`），否则默认的 x86 `ld.bfd` 报 `unrecognised emulation mode: armelf_linux_eabi`。
> 3. ILC 的增量判断只看应用工程，**不识别被引用库的改动**；改了 NewLife.Core/Remoting/Agent 后必须先删除 `StarAgent/obj/Release/net10.0/<rid>` 再发布，否则会拿到旧二进制。

### 2.2 本地源码联调开关

AOT 适配涉及上游三个仓库，发布时用 `-p:UseLocalNewLifeSource=true` 把 `C:\X\NewLife.*` 源码以项目引用方式接入（默认仍走 NuGet 包，正常构建/发布不受影响）：

| 仓库 | 改动 |
|------|------|
| Stardust / StarAgent | 新增 `UseLocalNewLifeSource` 条件引用；项目引用不会传递分析器，故显式挂 `NewLife.Core.SourceGenerator`、`NewLife.Remoting.SourceGenerator`，并自行挂载 `Locales/*.yml` |
| NewLife.Remoting / NewLife.Agent | 同上，指向本地 NewLife.Core |

`StarAgent.csproj` 在本地源码模式下额外定义 `LOCAL_NEWLIFE_SOURCE`，用于启用尚未发布到 NuGet 的上游 API（静态命令注册）。

## 3. 适配改造清单

### 3.1 NewLife.Core

| 位置 | 问题（AOT 表现） | 改法 |
|------|-----------------|------|
| `Configuration/Config.cs` | 配置模型属性被裁剪，`Star.config`/`StarAgent.config` 读出空对象后又被写回空文件 | 给 `Config<TConfig>` 的泛型参数加 `DynamicallyAccessedMembers(PublicProperties\|PublicFields)`，让所有 `Config<T>` 子类的配置项经泛型实参传导保留 |
| `Reflection/AssemblyX.cs` | `Assembly.GetReferencedAssemblies()`、`Assembly.GetCallingAssembly()`、仅反射加载在 AOT 下抛 `PlatformNotSupportedException`，插件加载与按名取类型整链失败 | 用 `RuntimeFeature.IsDynamicCodeSupported` 守卫并 try/catch 降级：引用关系判断按“可能引用”放行，调用方程序集返回空，仅反射加载整体跳过 |
| `Messaging/IEventBus.cs`、`Caching/Cache.cs`、`Caching/MemoryCache.cs`、`Caching/ICacheProvider.cs` | 事件总线工厂的 `TEvent` 未标注保留，订阅事件类型序列化为空对象（`IL2095`） | 接口与实现同步加 `DynamicallyAccessedMembers`，保证覆写签名一致 |
| `NuGet/NewLife.Core.props` | `Locales\*.yml` 用反斜杠，Linux 下 `AdditionalFiles` 匹配不到，源生成器不产出 `L`，跨平台构建失败 | 统一改成正斜杠（Windows 同样适用） |

### 3.2 NewLife.Remoting

| 位置 | 问题（AOT 表现） | 改法 |
|------|-----------------|------|
| 新增 `AotRoots.cs` | 登录等动作以**接口**（`ILoginResponse`）声明返回类型，泛型保留注解只作用于接口本身；实际反序列化的 `LoginResponse` 成员被裁剪，表现为“登录成功但名称为空、令牌为空，后续调用 401 鉴权失败” | 用 `DynamicDependency` 保留内置消息模型的公开成员与无参构造，并在 `ClientBase.Init()` 调用 `Ensure()` |

`ApiServer.Register<TService>()`（泛型入口）、`ApiActionTable`（动作静态表与源生成器）在上一轮已完成，本轮直接复用，无需改动。

### 3.3 NewLife.Agent

| 位置 | 问题（AOT 表现） | 改法 |
|------|-----------------|------|
| `Command/CommandHandlerFactory.cs` | 内建命令已静态登记，但**应用自定义命令**仍靠 `assembly.GetTypes()` + `Activator` 扫描；AOT 下类型元数据被裁剪，`-ListServices`/`-StartService`/`-StopService`/`-RestartService` 静默失效 | 新增静态注册入口 `CommandFactory.RegisterCustomFactory()`；应用静态登记后不再扫描服务程序集，避免重复登记异常 |

### 3.4 Stardust

| 位置 | 问题（AOT 表现） | 改法 |
|------|-----------------|------|
| 新增 `AotRoots.cs` | 节点/部署/服务等模型经 `Object` 参数进出 JSON 通道，成员被裁剪后收发空对象 | `DynamicDependency` 保留 `Models/*` 公开成员；由 `StarClient`、`StarFactory` 构造函数调用 |
| `StarClient`、`LocalStarClient`、命令处理器 | `Invoke(..., new { ... })` 匿名类型作为 RPC 参数，AOT 下发送 `{}` | 改为字典封套，键名与接口保持一致 |
| `Services/StarEventBus.cs`、`AppClient.CreateEventBus` | 事件类型未标注保留（`IL2091`） | 泛型参数补 `DynamicallyAccessedMembers` |

### 3.5 StarAgent

| 位置 | 问题（AOT 表现） | 改法 |
|------|-----------------|------|
| `Program.GetExport<TFunc>` | `Marshal.GetDelegateForFunctionPointer(IntPtr, Type)` 在 AOT 下不可用（`IL3050`） | 改用泛型重载 `GetDelegateForFunctionPointer<TFunc>` |
| `Program.StartLocalServer` | `ApiServer.Register(Object, String)` 宽面入口动作被裁剪，本机 UDP RPC 全丢（`IL2026`） | 改泛型入口 `Register<StarService>()`，再把实例回填给已注册动作（不依赖容器反射构造） |
| `WebPanel/StarApi.cs` | 51 处匿名类型响应，AOT 下全部退化为 `{}`（面板列表/本机信息空白） | 统一改字典封套；键名与原匿名属性一致，前端零改动 |
| `StarAgent.csproj` | `runtimeconfig.template.json` 不被 PublishAot 支持，GC 开关失效并告警 | 迁移为 `RuntimeHostConfigurationOption` 项，JIT/AOT 双端生效 |
| `MyStarClient` | 平台下发的是托管程序集包，覆盖原生可执行文件会静默退回 JIT 版 | AOT 下禁用自动升级并打印一次说明日志，改由人工部署原生包 |
| 新增 `AotRoots.cs` | 自定义命令处理器被裁剪，反射发现失效（未静态注册的构建形态） | `DynamicDependency` 保留 4 个命令处理器的公开构造 |

## 4. 部署

### 4.1 Linux（systemd）

```bash
# 1) 备份并替换
cp -a /root/agent /root/agent.bak-$(date +%Y%m%d%H%M%S)
systemctl stop StarAgent
mv /root/StarAgent.aot /root/agent/StarAgent && chmod +x /root/agent/StarAgent

# 2) 服务入口改为原生可执行文件
sed -i 's#^ExecStart=.*#ExecStart=/root/agent/StarAgent -s#' /etc/systemd/system/StarAgent.service
systemctl daemon-reload

# 3) 带 newlifed 看门狗的机型：新版 newlifed（2026-11-08 起）按 ActiveState 状态机巡检
#    （failed/重启循环才恢复，inactive 且曾成功运行过有 30 分钟宽限），救援副本归固件/newlifed 所有，
#    StarAgent 不写该目录。平台升级前应调用 `newlifed guard-hold [分钟]`，新版本启动并确认在线后 `guard-release`。
#    AOT 部署的黄金副本应包含原生 StarAgent（不是 dll），由固件构建侧装配；当前 build-rescue 只收 *.dll/*.json。

systemctl start StarAgent
```

**回滚**：把 `ExecStart` 改回 `/usr/share/dotnet/dotnet /root/agent/StarAgent.dll -s`，`systemctl daemon-reload && systemctl restart StarAgent`；`/root/agent.bak-*` 是完整旧版目录。

### 4.2 Windows（服务）

运行中的 exe 不能直接覆盖，但可以**重命名**：

```powershell
cd C:\X\Stardust\Bin\Local
Rename-Item .\StarAgent.exe StarAgent.exe.old
Copy-Item <新原生包>\StarAgent.exe .\StarAgent.exe
# 用面板 API 触发服务自重启（服务进程为 LocalSystem，可自行控制服务）
$tok = (Invoke-WebRequest "http://127.0.0.1:5500/api/login?user=admin&password=admin" -UseBasicParsing).Content | ConvertFrom-Json
Invoke-WebRequest "http://127.0.0.1:5500/api/control?action=restart" -Headers @{Authorization="Bearer $($tok.data.token)"} -UseBasicParsing
```

原生包与 JIT 包共用同一份 `Config/`，无需改配置。

## 5. 实测记录（2026-11-08）

| 检查项 | PC | A2 | A4 | A6 |
|--------|----|----|----|----|
| 服务状态 | Running | active | active | active |
| 节点登录 | X5 | A2（2006款） | A4-WiFi | A6 |
| 应用登录 + 配置中心 | ✅ | ✅ | ✅ | ✅ |
| Web 面板首页 | 200 / 62 KB | 200 / 65 KB | 200 | 200 |
| `/api/status`、`/api/health`、`/api/languages` | ✅ | ✅ | ✅ | ✅ |
| `/star/services`、`/star/machine`（字典封套） | ✅ | ✅ | ✅ | ✅ |
| 本机 UDP RPC（`-ListServices`） | ✅ | ✅ | ✅ | ✅ |
| 子服务增删/启停（面板 → 进程拉起） | ✅ | ✅ | ✅ | ✅ |
| 升级检查（AOT 下跳过自动覆盖） | ✅ 日志说明 | ✅ | ✅ | ✅ |
| 插件加载 | 无外部插件，无异常 | 同左 | 同左 | 同左 |

回归测试：Core 3663 通过、Remoting 867 通过、Agent 377 通过、Stardust 218 通过（0 失败）。

## 6. 已知边界与后续

| 项 | 现状 | 后续 |
|----|------|------|
| 外部插件（`Plugins` 目录的 DLL） | NativeAOT 无法加载新托管程序集，只能使用编译进代理本体的插件；AOT 启动时会打印一次说明 | 需要外部扩展时用 JIT 版，或把插件源码并入代理构建 |
| 平台自动升级 | 平台下发托管程序集包，不能覆盖原生可执行文件；AOT 下已禁用并说明 | 平台侧提供「原生包」发布类型，或由发布流水线按 RID 分发 |
| NuGet 包版本 | 本轮改动在本地源码仓库（Core 12.0.2026.1108、Remoting 4.0.2026.1108、Agent 10.18.2026.1108） | 发布新版本包后，可去掉 `UseLocalNewLifeSource` 与 `LOCAL_NEWLIFE_SOURCE` 条件，直接 `dotnet publish` |
| 其他 RID | 本轮只验证 win-x64 / linux-arm64 / linux-arm | osx-x64/osx-arm64、linux-musl-*、loongarch64 按同一路径发布验证 |
| 包体积 | 默认档 16.4~19.0 MB（Slim 档 12.9~15.4 MB），JIT 版为 6 MB 程序集 + 需装运行时 | 体积构成与开关收益见 §7；再往下压需裁剪功能面（面板资源、BCL 子系统） |

## 7. 体积构成与优化（2026-11-08 第二轮）

### 7.1 为什么"功能不多"却要十几 MB

NativeAOT 的产物 = **托管运行时 + BCL + 应用代码 + 反射元数据 + 嵌入资源**，五类全部静态链接进同一个可执行文件，没有"省掉运行时"这一选项。用 `-p:IlcGenerateMapFile=true` 生成的映射表逐块统计（win-x64、首轮 19.44 MB）：

| 构成 | 体积 | 说明 |
|------|------|------|
| 反射元数据 `__dehydrated_data` | 3.73 MB | 类型/成员/自定义属性元数据。序列化、RPC、配置模型、`DynamicDependency` 保留越多，这块越大 |
| `System.Private.CoreLib` | 3.23 MB | 运行时本体：GC、类型系统、异常展开、线程与线程池、P/Invoke 桥、区域化 |
| 冻结字符串 `__FrozenSegmentStart` | 3.09 MB | 编译期确定的字面量：正则、格式串、配置键、日志模板、SQL 片段… |
| `NewLife.Core` | 1.02 MB | 框架本体代码 |
| `System.Net.*` | 0.83 MB | HTTP / WebSocket / Socket 协议栈 |
| 嵌入资源 | 0.80 MB | 面板 `wwwroot`（HTML/JS/CSS 388 KB）+ Agent 面板 340 KB |
| ILC 元数据 blob | 0.77 MB | 运行期类型与虚表信息 |
| `System.Private.Xml` | 0.52 MB | XML 配置与序列化 |
| `System.Linq` + `System.Text.*` + `Regex` | 1.03 MB | LINQ、JSON、字符串处理、正则引擎 |
| `NewLife.Remoting` / `NewLife.Agent` | 0.67 MB | 远程调用与代理宿主 |
| 各程序集 `LocalizationTable` | ≈0.70 MB | 32 种语言词表，Deflate 压缩后仍占位 |
| 异常堆栈映射 | 0.25 MB | `StackTraceSupport` 生成的地址→方法表 |
| 压缩 / 加密 / 网络 / 日志等 | 剩余 | GZip、AES/RSA/SHA、UDP/TCP、日志落盘 |

探针基线说明了"地板"在哪：空 AOT 程序 **0.80 MB**；再加 HttpClient + WebSocket + GZip + SHA256/RSA + System.Text.Json 后 **2.21 MB**。也就是说，一个"什么都不做"的原生代理至少 0.8 MB，而要具备网络+加密+序列化这些基础能力就已经 2.2 MB。

### 7.2 和 C++ 原生代理比，差在哪

仓库自带的 C++ 原生代理（`StarAgent.Native/cpp`）arm64 实测 **7 MB**，同口径下 AOT 版 19 MB。差距不是"编译器不够聪明"，而是两边装的东西不一样：

| 差异 | AOT 版 | C++ 版 |
|------|--------|--------|
| 运行时 | 内嵌完整托管运行时（GC、类型系统、异常、线程池）≈3.2 MB | 裸线程 + 手动内存管理，无此开销 |
| 协议与算法 | 静态链接 BCL 的 HTTP/2、WebSocket、XML、JSON、正则、压缩、TLS | 静态链接 OpenSSL（≈2~3 MB）+ zlib，HTTP/XML/JSON 自己写或更薄的库 |
| 反射元数据 | 3.7 MB（序列化/RPC/配置模型需要） | 手写协议解析，几乎没有 |
| 功能面 | Web 管理面板、插件、远程发布、进程守护、指标采集、日志上传 | 登录/心跳/指标/命令，无面板与远程发布 |

按同能力折算，C++ 版做到与 StarAgent 等价（面板 + 远程发布 + 进程守护 + 指标）大致也要 8~12 MB。AOT 的十几个 MB 里，"多出来"的主要是**拿来即用的运行时与 BCL**，换来的是同一份 C# 代码全平台、功能与 JIT 版完全一致。

### 7.3 体积开关实测（win-x64）

同一份源码、同一个 SDK（10.0.403）逐项开关对照，每一行都在上一行基础上只改一个开关：

| 步骤 | 体积 | 增量 | 代价 |
|------|------|------|------|
| 关闭全部体积开关（`OptimizationPreference=Speed`、方法体折叠关、常规 LINQ、调试器/热重载开） | 21.06 MB | — | — |
| + `OptimizationPreference=Size` | 17.37 MB | −3.69 MB | 少数热路径以代码尺寸优先，实测启动与心跳无差异 |
| + `IlcFoldIdenticalMethodBodies` / `UseSizeOptimizedLinq` / `DebuggerSupport=false` / `MetadataUpdaterSupport=false` | **16.44 MB**（默认档） | −0.93 MB | 不能附加调试器、不支持热重载；代理无热重载场景 |
| + 语言词表白名单 `zh-Hans;en` | 13.42 MB | −3.02 MB | 其余 30 种语言的消息回退英文原文 |
| + `StackTraceSupport=false` | **12.86 MB**（Slim 档） | −0.56 MB | 异常只剩方法名，没有行号与源文件 |
| ~~`UseSystemResourceKeys=true`~~ | −0.03 MB | | 不采用：收益可忽略，却把异常消息变成资源键 |
| ~~面板 `i18n` 资源裁剪~~ | −0.26 MB | | 不采用：面板语言列表与词表对不上，选择器会给出选不动的语言 |

> 首轮发布的 19.44 MB 落在「全关 21.06」与「Size 17.37」之间：那一版没有显式开关，用的是 .NET 10 的默认值。

> 语言白名单必须是**全局属性**：只有用环境变量（而非 `-p:`，分号会被 MSBuild 当属性分隔符）传给 `dotnet publish`，它才会传播到 NewLife.Core / Remoting / Agent / Stardust 全部被引用工程的源生成器。

### 7.4 两档发布 profile

| RID | 首轮基线 | 默认档 | Slim 档 |
|-----|---------|--------|---------|
| win-x64 | 19.44 MB | **16.44 MB**（−15%） | **12.86 MB**（−34%） |
| linux-arm64 | 19.90 MB | **19.00 MB**（−5%） | **15.40 MB**（−23%） |
| linux-arm | 18.10 MB | **17.32 MB**（−4%） | **13.54 MB**（−25%） |

- **默认档**（`StarAgentAot=true` 时由 csproj 自动启用）：`Size` + 方法体折叠 + 小体积 LINQ + 关闭调试器/热重载。**保留全部 32 种语言词表与异常堆栈**，四端生产环境使用此档。
- **Slim 档**（`-Slim`）：在默认档之上再做语言白名单（仅中文 + 英文）与 `StackTraceSupport=false`。适合镜像/带宽敏感、或只服务中英文场景的边缘设备；代价是现场排查异常时看不到行号。ARM 上 `Size` 收益不明显（约 0.8 MB），Slim 的收益主要来自语言与堆栈裁剪。

```powershell
# 本机
pwsh Tools/Scripts/publish-aot.ps1 -Rid win-x64
pwsh Tools/Scripts/publish-aot.ps1 -Rid win-x64 -Slim -Out .tmp/aot/win-slim

# 交叉编译（WSL Ubuntu 24.04，需 clang + lld + 交叉 binutils）
pwsh Tools/Scripts/publish-aot.ps1 -Rid linux-arm64 -Compiler clang -ObjCopy aarch64-linux-gnu-objcopy
pwsh Tools/Scripts/publish-aot.ps1 -Rid linux-arm -Compiler clang -LinkerFlavor lld -ObjCopy arm-linux-gnueabihf-objcopy
```

### 7.5 语言白名单与面板语言列表

裁剪语言词表后有个副作用：面板语言下拉框仍按内置清单列出 32 种语言，用户选了没有词表的语言，界面与日志会静默回退原文。第二轮一并修掉：

1. **构建期**：Core 的源生成器读取 `NewLifeLocalizationLanguages`（`NewLifeLocalizationGenerateL`/`NewLifeLocalizationOverwrite` 同一套机制），只为白名单内的语言生成 `LocalizationTable`；`NewLife.Core.csproj` 与打包后的 `NuGet/NewLife.Core.props` 都声明了 `CompilerVisibleProperty`，本地源码构建与 NuGet 引用行为一致。
2. **运行期**：`Localization.GetLanguageOptions()`（面板语言下拉框的数据源）只返回**真正有词表**的语言，完整语言清单仍由 `GetLanguages()` 提供；Slim 档下 `/api/languages` 自然只剩「简体中文 / English」两项。
3. **无 ICU 也要能用**：词表键改为直接按语言名推导（`GetCultureKey(String)`），不再为取键去创建 `CultureInfo`。设备侧以 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` 运行（不装 ICU），创建具名区域性会抛异常，旧实现会让筛选把全部语言判成"不可用"。

### 7.6 结论

- 16 MB 左右是"托管运行时 + 全套 BCL + 反射元数据"的合理量级，继续压缩的边际收益很低：再砍只能动功能面（面板资源、XML/正则、语言词表）或换语言栈。
- 真要 1 MB 级别，答案不是"优化 AOT"，而是"换实现"——仓库里的 C++/Rust/Go 原生代理就是这个路线，代价是没有面板、插件、远程发布与进程守护，且功能面与 C# 版不再一致。
- 当前默认档在四端已验证：功能与 JIT 版一致，体积 −4%~−15%，内存与启动保持首轮水平。
