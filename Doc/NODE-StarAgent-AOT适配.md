# NODE-StarAgent NativeAOT 适配

> 适用版本：StarAgent 3.7（net10.0）、NewLife.Core 12、NewLife.Remoting 4、NewLife.Agent 10.18
> 关联文档：[NODE-StarAgent内存优化](NODE-StarAgent内存优化.md) | [NODE-StarAgent架构](NODE-StarAgent架构.md)

## 1. 结论

.NET 10 版 StarAgent 已**全面支持 NativeAOT**，并把 JIT 版时代的两块硬阻塞（程序集反射、反射路由）在上游组件里彻底解决。四类目标设备实测通过：

| 设备 | 架构 | 宿主 | 原生包 | 启动 | RSS | 线程 | 登录/心跳 | Web 面板 | 本机 UDP RPC | 子服务管理 |
|------|------|------|--------|------|-----|------|-----------|----------|--------------|------------|
| PC（X5） | win-x64 | Windows 服务 | 19.4 MB 单文件 | ≈1 s | 35~44 MB | 14~17 | ✅ | ✅ | ✅ | ✅ |
| A2 | linux-arm64 | systemd | 19.9 MB 单文件 | 0.14 s | 39 MB | 17 | ✅ | ✅ | ✅ | ✅ |
| A4 | linux-arm64 | systemd | 19.9 MB 单文件 | — | 40 MB | 17 | ✅ | ✅ | ✅ | ✅ |
| A6 | linux-arm | systemd | 18.1 MB 单文件 | 0.25 s | 33 MB | 17~20 | ✅ | ✅ | ✅ | ✅ |

对照 JIT 版（同机型历史数据：A6 73 MB / A4 95 MB / A2 88 MB，启动 8~16 秒），**常驻内存约降 50%~60%，启动从秒级十位数降到亚秒级**。

与旧结论（2026-11 初，A4 只有 29 MB 但插件与面板不可用）相比，本次把功能面补齐，代价是内存比“裸壳”高约 10 MB。

## 2. 发布方式

### 2.1 开关

`PublishAot` **不能**作为命令行全局属性传入（`-p:PublishAot=true` 会波及被引用库的 net45/net40 目标，直接报 `NETSDK1207`）。项目内用独立开关收敛到 net10.0：

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
| `Program.SyncRescueCopy` | 无 `StarAgent.dll` 时直接返回，看门狗救援副本停留在固件旧版 | AOT 部署改为同步当前原生可执行文件 |
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

# 3) 带 newlifed 看门狗的机型：救援副本同步原生二进制
if [ -d /usr/share/newlife/rescue/staragent ]; then
  cp -f /root/agent/StarAgent /usr/share/newlife/rescue/staragent/StarAgent
  chmod +x /usr/share/newlife/rescue/staragent/StarAgent
fi

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
| 包体积 | 单文件约 19 MB（JIT 版为 6 MB 程序集 + 需装运行时） | 需要更小可用 `-p:OptimizationPreference=Size`，与内存/启动权衡 |
