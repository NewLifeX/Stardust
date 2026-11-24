# PUB-25 应用包管理（星尘包）设计

> 版本：v2.0 | 日期：2026-11-24 | 状态：已并入发布中心，代码与测试已落地
> 对应 Issue：[#96 星尘包管理计划](https://github.com/NewLifeX/Stardust/issues/96)
> 归属模块：PUB 远程发布（子模块）
> 关联文档：[PUB-19-应用包规范](PUB-19-应用包规范.md) | [PUB-远程发布架构](PUB-远程发布架构.md)

---

## 1. 结论与定位

**做，但只做企业内部的包管理，并直接复用发布中心的部署集与部署版本。**

一句话：给星尘补一个「没有控制台也能装软件」的入口。包来自本机 StarAgent 连的 StarServer，不来自公共市场。

三条边界：

1. **不做公共仓库**：只有管理员在星尘平台上传的内部包。
2. **不做容器**：应用直接跑在宿主机上，没有隔离；只解决「装得上、起得来、停得下」。
3. **不写第二套部署引擎**：包就是发布中心的一个应用部署集，执行复用现有 `ServiceManager` 和部署策略。

`star` 不是新程序，是 StarAgent 的命令行别名：

- Windows：`star.cmd` → `StarAgent.exe`
- Linux/macOS：`star` → `StarAgent`（AOT）或 `dotnet StarAgent.dll`

---

## 2. 与发布中心的关系

**一个应用包 = 一个应用部署集（AppDeploy）；包版本 = 部署版本（AppDeployVersion）。**

| 对比项 | 发布中心（AppDeploy / AppDeployVersion） | 应用包（同一张表） |
|---|---|---|
| 解决的问题 | 把应用发布到指定节点 | 把软件装到本机 |
| 驱动方式 | 控制台推送 | 命令行拉取 |
| 应用数量 | 一个部署集一个应用 | **一个包一个应用**，多应用拆成多个包 |
| 节点绑定 | 有（AppDeployNode） | 不强制绑定，任何节点都能拉 |
| 包身份 | `AppDeploy.FullName` = `项目名/应用名` | 同左 |
| 版本回滚 | 禁用版本 + 重新发布 | 禁用版本 + `star run pkg:version` |
| 存储 | Attachment + IFileStorage | 复用同一套 |
| 鉴权 | 节点令牌 | 复用节点令牌 |
| 菜单 | 发布中心 → 应用部署 | **不新增菜单**，直接用应用部署列表与版本列表 |

### 2.1 为什么合并

- 两者本质都是「某台机器运行某个应用」，只是触发方式不同；分两套表会导致重复上传、重复版本管理和数据漂移。
- 星尘平台「应用管理」里本来就有 `StarServer`、`StarWeb`、`MySQL`、`Redis` 这些应用；包管理继续维护第二份应用目录没有意义。
- 发布中心的版本已经支持 OS/Arch/TargetFramework 匹配，正好覆盖同一个版本的不同 RID 或不同运行时。
- 合并后 `star` 与「发布中心推送」共用同一份版本元数据：控制台发布什么，命令行就拉到什么。

### 2.2 用户故事对照

- `star run -d NewLife/StarServer`：拉取部署集 `NewLife/StarServer` 的当前版本，交给代理守护。
- `star run -d NewLife/StarWeb`：同上，两个应用各自一个包，对应星尘平台里的两个应用。
- `star run -d redis -port 6379`：部署集 `redis`（或 `项目/redis`）按参数启动，交给代理守护。
- `star mysql/mysql`：部署集 `mysql/mysql` 的当前版本，前台运行。

### 2.3 共用什么

- 附件表 `Attachment`、分布式文件存储 `IFileStorage`、短时下载令牌 `DownloadTokenService`。
- 节点登录与鉴权 `[ApiFilter]` + `Context.Device`。
- 控制台区域 `DeploymentArea` 和权限体系。
- `AppDeploy.GetDeployVersion` 的平台匹配逻辑（`PlatformHelper`）。

---

## 3. 总体架构

```
┌──────────────────────────── StarWeb（发布中心 → 应用部署） ──────────────────────────┐
│  AppDeploy 列表（包名/当前版本） │ 版本列表 │ 上传版本 │ 启用/禁用 │ 删除 │
└───────────────────────────────────┬──────────────────────────────────────────────────┘
                                    │ 管理员上传 zip（star.json 可选）
┌───────────────────────────────────▼──────────────────────────────────────────────────┐
│ StarServer                                                                           │
│  PackageController（节点 API）  PackageService（AppDeploy 查询/平台匹配/下载令牌）      │
│  AppDeploy / AppDeployVersion（Stardust.Data）  Attachment / IFileStorage              │
└───────────────────────────────────┬──────────────────────────────────────────────────┘
                                    │ 节点令牌
┌───────────────────────────────────▼──────────────────────────────────────────────────┐
│ 本机 StarAgent（服务）                                                                │
│  StarService 本机 UDP RPC（默认 5500，令牌校验）                                        │
│  ServiceManager（守护、启停、内存限制、健康检查）                                       │
└───────────────────────────────────┬──────────────────────────────────────────────────┘
                                    │ 本机 RPC
┌───────────────────────────────────▼──────────────────────────────────────────────────┐
│ star CLI（StarAgent 的子命令）                                                        │
│  pull / run / ls / stop / start / rm / info / search                                  │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. 包格式 `star.json`（可选）

包 = 一个 zip，**最多一个应用**。zip 根目录的 `star.json` 可选：

- 有：上传时校验并填充部署集的运行参数；版本号、OS/Arch 从清单读取。
- 无：服务端按部署集上的运行参数合成清单；适合 MySQL、Redis 这类只有二进制、没有清单的包。

```json
{
  "schema": 1,
  "name": "NewLife/StarServer",
  "version": "4.0.0",
  "displayName": "星尘服务端",
  "description": "StarServer 服务端",
  "apps": [
    {
      "name": "starserver",
      "displayName": "StarServer",
      "fileName": "StarServer.dll",
      "arguments": "--urls http://*:6600",
      "environments": "ASPNETCORE_ENVIRONMENT=Production",
      "healthCheck": "http://127.0.0.1:6600/health",
      "ports": [ 6600 ],
      "main": true
    }
  ]
}
```

### 4.1 顶层字段

| 字段 | 必填 | 说明 |
|---|:---:|---|
| `schema` | ✅ | 格式版本，当前为 1；不认识就拒绝 |
| `name` | ✅ | `项目名/应用名`，与 `AppDeploy.FullName` 一致，大小写不敏感 |
| `version` | ✅ | 版本号；上传时同步写入 `AppDeployVersion.Version` |
| `displayName` | | 显示名 |
| `description` | | 说明 |
| `os` / `arch` | | 目标平台，0 表示通用；二进制包必须填 |
| `apps` | ✅ | **必须恰好一个应用** |

### 4.2 应用字段

| 字段 | 必填 | 说明 |
|---|:---:|---|
| `name` | ✅ | 包内唯一 |
| `fileName` | ✅ | 相对包根目录的启动文件；也可以是 `ping` 这类系统命令 |
| `arguments` | | 启动参数，直接使用明确值 |
| `workingDirectory` | | 相对包根目录；空则用 `fileName` 所在目录 |
| `environments` | | `key=value;key2=value2` |
| `healthCheck` | | http/tcp/udp 探针 |
| `ports` | | 端口占用检查 |
| `main` | | 接收命令行透传参数的应用；只有一个应用时默认它就是 main |
| `maxMemory` / `priority` / `reloadOnChange` / `startupHook` | | 映射到 `ServiceInfo` |

### 4.3 校验规则

- `schema` 必须是 1。
- `name` 必须匹配 `^[a-z0-9][a-z0-9._-]*/[a-z0-9][a-z0-9._-]*$`（忽略大小写）。
- `version` 非空。
- `apps` 必须恰好一个（服务端上传时强制）；本地 `star run -f` 也按单应用执行。
- `fileName` / `workingDirectory` 必须是相对路径，不能以 `/`、`\`、盘符开头，不能含 `..`。
- `fileName` 指向的文件必须存在于 zip 中（系统命令除外）。

---

## 5. 数据模型（合并后）

所有包都存放在发布中心的两张现有表中，不新增表。

### 5.1 AppDeploy（应用部署集 = 包）

| 列 | 类型 | 说明 |
|---|---|---|
| Id | Int32 | 主键，自增 |
| ProjectId | Int32 | 项目（发布者），映射 `GalaxyProject` |
| AppId | Int32 | 应用，对应 `StarApp` |
| Name | String | 应用名，全局唯一 |
| Category | String | 分类 |
| Enable | Boolean | 启用 |
| Version | String | 当前版本。管理员通过「使用版本」显式设置，`star` 只拉取此版本 |
| MultiVersion | Boolean | 同版本多 RID/TFM 时开启；上传含 `star.json` 的包会自动开启 |
| PackageName | String | 上传文件名匹配，支持 `*` |
| Port / Urls | | 端口与服务地址 |
| FileName / Arguments / WorkingDirectory / UserName / Environments | | 运行参数 |
| MaxMemory / Priority / Mode / AllowMultiple / AutoStop / ReloadOnChange / HealthCheck | | 运行参数 |
| 扩展字段 | | `CreateTime/UpdateTime/CreateUser/UpdateUser/Remark` 等 |

- `FullName` = `ProjectName.IsNullOrEmpty() ? Name : $"{ProjectName}/{Name}"`，不落库。
- 所有 `AppDeploy` 都是潜在包；`SearchPackages` 只返回 `Enable=true` 且 `Version` 非空的部署集。
- 新上传的版本不会自动成为当前版本；管理员在版本列表点「使用版本」后，`star` 才会拉到该版本。

### 5.2 AppDeployVersion（包版本 = 部署版本）

| 列 | 类型 | 说明 |
|---|---|---|
| Id | Int32 | 主键，自增 |
| DeployId | Int32 | 关联 AppDeploy（数据库列名 `AppId`） |
| Version | String | 版本号 |
| Enable | Boolean | 启用 |
| Url | String | 附件地址 `/cube/file?id=...` |
| Hash | String | MD5 |
| Size | Int64 | 文件大小 |
| OS | OSKind | 目标系统，0 通用 |
| Arch | CpuArch | 目标架构，0 通用 |
| TargetFramework | String | 目标框架，空字符串表示通用 |
| 扩展字段 | | 提交信息、创建/更新时间等 |

普通索引：`(DeployId, Version)`，不要求唯一。

> 同一版本可以为不同 OS/Arch/TFM 上传多份。客户端按平台与节点已安装框架精确匹配，`0` / 空表示通用；索引不唯一，平台组合由上传内容决定。

### 5.3 旧表清理

原 `AppPackage` / `AppPackageVersion` 两表已从 Model.xml 删除。生产库确认数据已迁移后，可手工删除：

```sql
DROP TABLE AppPackageVersion;
DROP TABLE AppPackage;
```

---

## 6. 服务端 API

`Stardust.Server/Controllers/PackageController.cs`，节点鉴权（`[ApiFilter]`）。接口路径保持不变，数据源改为 `AppDeploy` / `AppDeployVersion`。

| 接口 | 说明 |
|---|---|
| `Package/Search?key=&pageIndex=&pageSize=` | 搜索启用且已设置当前版本的包 |
| `Package/Get?name=NewLife/StarServer` | 包详情（短名唯一时允许 `StarServer`） |
| `Package/GetVersions?name=` | 版本列表 |
| `Package/GetLatest?name=&os=&arch=&version=` | 当前匹配版本（可显式指定版本回滚）+ 下载地址 |
| `Package/Download?id=` | 返回带短时令牌的下载地址 |

返回模型：

```csharp
public class PackageInfo
{
    public String Name { get; set; }        // NewLife/StarServer
    public String Publisher { get; set; }   // 项目名，可空
    public String ShortName { get; set; }   // AppDeploy.Name
    public String DisplayName { get; set; }
    public String Description { get; set; }
    public String Version { get; set; }     // 当前应用版本
}

public class PackageVersionInfo
{
    public Int32 Id { get; set; }
    public String Name { get; set; }
    public String Version { get; set; }
    public OSKind OS { get; set; }
    public CpuArch Arch { get; set; }
    public String TargetFramework { get; set; }
    public Int64 Size { get; set; }
    public String Hash { get; set; }
    public String Url { get; set; }         // 已附加短时令牌
    public String Manifest { get; set; }    // 服务端合成的单应用清单
    public DateTime PublishTime { get; set; }
}
```

**清单合成**：`PackageService.BuildManifest` 用 `AppDeploy` 的运行字段生成单应用 `StarPackage`；即使 zip 内没有 `star.json`，客户端也能得到完整启动参数。

**平台匹配**：`PlatformHelper.MatchPlatform` + `AppDeployVersion.MatchFramework`；节点没上报框架时只按 OS/Arch 匹配。

**短名解析**：`name` 含 `/` 时按 `FullName` 精确匹配；不含 `/` 时按 `Name` 查，唯一才返回，多个则报「请指定项目名」。

**当前版本**：默认只读取 `AppDeploy.Version`，再按平台与框架从该版本的多份 RID/TFM 中选择；显式传 `version` 时允许拉取指定版本，用于回滚。新上传但未「使用版本」的版本不会被拉取。

**管理端上传**：不走节点 API，走 Web 控制台（见第 7 节）。

---

## 7. 控制台

不新增菜单。使用现有 **发布中心 → 应用部署** 与 **应用部署版本**：

- **应用部署列表**：包名（`项目/应用`）、分类、当前版本、启用。
- **新增/编辑部署集**：项目、应用名、启动文件、启动参数等。
- **上传版本**：在版本列表点「上传版本」，选择一个 zip；服务端解析可选的 `star.json` 并自动填充。
- **版本列表**：版本、OS/Arch/TFM、大小、哈希、启用；管理员点「使用版本」后该版本才成为当前版本。

### 7.1 上传流程

```
管理员选择 zip
  → 服务端保存到 Attachment（复用 IFileStorage）
  → 尝试读取 zip 根目录 star.json
      ├─ 有：校验 schema/包名/单应用；取出版本、OS/Arch
      └─ 无：使用表单版本与平台
  → 包名必须匹配部署集 FullName（带项目前缀时自动关联项目）
  → 填充部署集空白的运行参数（FileName/Arguments/HealthCheck 等）
  → 部署集 MultiVersion=true
  → 创建 AppDeployVersion（Url/Hash/Size/OS/Arch/TFM）
  → 同版本同平台重复时给出明确错误
```

失败时删除刚保存的附件，避免留下无包记录。

### 7.2 权限

- 部署集与版本使用现有 `[EntityAuthorize]` 标准权限。
- 上传版本需要 `PermissionFlags.Update`（WebApi `Upload`）或标准新增/修改权限（Web 表单）。
- 节点 API 只读。

### 7.3 示例：MySQL / Redis

1. 在「应用部署」新增部署集：项目 `library`，名称 `redis`，启动文件 `redis-server`，参数按需填写。
2. 上传对应平台/架构的 `redis-*.zip`（可以没有 `star.json`）。
3. 节点执行 `star run -d library/redis -port 6379`；短名唯一时可直接 `star run -d redis -port 6379`。

---

## 8. `star` CLI

### 8.1 命令

| 命令 | 说明 |
|---|---|
| `star <包名>` | 前台运行；需要守护时用 `star run -d` |
| `star run <包名>[:版本] [-d] [参数...]` | 运行；`-d` 交给 StarAgent 守护 |
| `star pull <包名>[:版本]` | 只下载，不运行 |
| `star ls` | 列出本机已安装的包 |
| `star info <包名>` | 查看包信息和版本 |
| `star start <包名>` | 启动已安装的守护应用 |
| `star stop <包名>` | 停止并禁止自动启动 |
| `star restart <包名>` | 重启 |
| `star rm <包名>[:版本]` | 停止 + 删除服务和文件；代理未运行时只删本地缓存 |
| `star search <关键字>` | 搜索服务端包 |
| `star --help` / `--version` | 帮助/版本 |

**Docker 式参数透传**：

- `star run -d redis -port 6379`
- 包名之后的参数原样追加到应用启动参数，不做变量替换。
- 参数位置：包名之后、`--` 之后的全部内容。

**`-d` 的语义**：

- 有 `-d`：应用加入 StarAgent 服务列表，`Enable=true`，由代理守护；重启代理后仍在。
- 无 `-d`：CLI 前台拉起，Ctrl+C 停止；不写入服务列表。

**本地文件**：

- `star run -f ./redis-7.2.zip`：直接运行本地包，便于离线交付和测试。
- `star pull -f ./redis-7.2.zip`：安装到本地缓存。
- 本地 zip 仍要求根目录 `star.json`；服务端包可以没有。

### 8.2 `star` 命令怎么来

`star` 是 StarAgent 的别名，不复制二进制：

- Windows：启动时在安装目录生成 `star.cmd`，内容 `"<安装目录>\StarAgent.exe" %*`；没有 exe 时用 `dotnet "<安装目录>\StarAgent.dll" %*`。代理不修改系统 PATH，安装脚本负责把安装目录加入 PATH；未加入时可直接运行 `star.cmd`。
- Linux/macOS：生成安装目录下的 `star` 脚本，并尽量软链到 `/usr/local/bin/star`，优先 `StarAgent`，否则 `dotnet StarAgent.dll`；无权限时只保留安装目录脚本。
- 卸载脚本清理 `/usr/local/bin/star` 软链；Windows 下 `star.cmd` 随安装目录一起删除。

生成时机：在 `MyService.Init()` 中调用 `StarLauncher.EnsureAlias()`（安装和启动都会经过），失败只记日志、不阻断启动。`StarAgentTool`（dotnet tool）是独立命令，不自动创建 `star`。

### 8.3 命令分发

在 `StarAgent/Program.cs` 的 `Main` 中，完成 `TuneRuntime`、`StarLocalization.InitLogCulture`、`AotRoots.Ensure`、`XTrace.UseConsole` 之后，进入 `-upgrade` / `-delay` / 服务菜单之前增加：

```
if (PackageCommand.TryRun(args)) return;
```

优先于现有 `-install`、`-run`、zip 部署菜单；`-` 开头的参数仍走原逻辑，保证兼容。

`StarAgentTool.csproj` 通过链接文件复用同一份 `Program.cs`，新增的 `PackageCommand.cs` 需要同步加入 `<Compile Include="..\StarAgent\PackageCommand.cs" Link="PackageCommand.cs" />`。

---

## 9. 本地包存储与运行

### 9.1 目录

守护模式（代理有权限）：

```
{AgentBase}/../packages/
  {project}/{name}/{version}/
    star.json                       # 服务端清单或 zip 内清单
    ...
  {project}/{name}/current          # 文本文件，记录当前版本
```

前台模式（普通用户）：

```
~/.stardust/packages/             # Windows: %USERPROFILE%\.stardust\packages
  {project}/{name}/{version}/
```

> 前台包只在用户缓存中，守护包在代理目录下。`star ls` 合并展示。

### 9.2 下载

- 从 `Package/GetLatest` 拿 URL、Hash、Manifest。
- 复用 `HttpDownloadOptions`：分段超时、重试、`ExpectedHash` 校验。
- 下载到临时文件，校验通过后再原子移动到版本目录。
- 服务端清单写入版本目录 `star.json`，作为节点上的实际运行配置。

### 9.3 运行

**前台（无 `-d`）**：

1. 解压到用户缓存。
2. 启动应用。
3. 子进程继承当前控制台，日志直接输出。
4. Ctrl+C：终止整个进程树；等待退出；返回非零退出码时打印。

**后台（`-d`）**：

1. 本机 RPC `PackageInstall(name, version, args)`。
2. 代理下载/解压到代理包目录。
3. 生成 `ServiceInfo`：
   - `Name` = 包名（单应用时不再拼接应用名）
   - `Package` / `PackageVersion` / `PackageApp` 字段
   - `FileName` / `WorkingDirectory` 绝对路径
   - `Enable=true`
4. 调用 `ServiceManager.Install`（或 `Add` + `Start`），由代理守护。
5. 返回启动结果。

### 9.4 停止 / 删除

- `star stop`：本机 RPC `PackageStop`，停止应用并把 `Enable=false`，避免守护进程重新拉起。
- `star start`：`Enable=true` + 启动。
- `star rm`：`PackageStop` → 从服务列表移除 → 删除包目录（所有版本或指定版本）。
- 删除前检查进程是否已退出；未退出则强制结束并等待，再删目录，避免 Windows 文件占用。

### 9.5 升级与回滚

- `star run -d pkg` 发现已安装且版本不同：
  1. 下载新版本。
  2. 停止旧版本应用。
  3. 用新版本路径替换服务定义并启动。
  4. 启动失败 → 保持旧版本服务定义并重新启动旧版本。
  5. 成功后写 `current`。
- `star run -d pkg:oldVersion` 显式回滚。
- 旧版本目录保留，`star rm` 时才删除。

---

## 10. 关键流程

### 10.1 上传包

```
Web 上传 zip → Attachment.SaveFile → 尝试读取 star.json（可选）
  → 校验单应用与包名 → 填充 AppDeploy 空参数 → AppDeployVersion 落库
  → 新版本保持未应用状态，管理员点「使用版本」后才写入 AppDeploy.Version
```

### 10.2 `star run -d NewLife/StarServer`

```
star → 本机 RPC 可用？
  ├─ 是 → PackageInstall → 代理用 StarClient 请求 Package/GetLatest → 下载 → 解压 → ServiceManager.Install
  └─ 否 → CLI 用节点身份登录 → 请求 Package/GetLatest → 下载到用户缓存 → 提示先启动代理（-d 必须代理在运行）
```

> 代理已在运行时，CLI 不重新登录，避免同 Code 会话互相顶掉。

### 10.3 `star run -d redis -port 6379`

```
star → 查 Package/GetLatest(redis) → 服务端生成单应用清单
  → -port 6379 原样追加到 redis-server 启动参数 → 代理守护 redis-server
```

### 10.4 `star mysql/mysql`

```
star → 查 Package/GetLatest(mysql/mysql) → 前台运行
```

---

## 11. 关键设计决策

| 决策 | 选择 | 理由 | 被否决方案 |
|---|---|---|---|
| 模块归属 | 直接并入发布中心 | 一套应用目录、一套版本、一处上传 | 新建 AppPackage 子模块（重复建设） |
| 包身份 | `项目名/应用名` | 与企业内部命名一致，可短名解析 | 单独 Publisher 列（与项目重复） |
| 包表 | 复用 AppDeploy | 一个包一个应用，字段完全够用 | 新表 AppPackage/AppPackageVersion（装不下多应用的理由已消失） |
| 版本表 | 复用 AppDeployVersion | 平台/框架匹配、附件、当前版本指针已有 | 新表包版本（重复实现） |
| `star.json` | 可选 | 二进制包没有清单也能上传；控制台配置即事实源 | 强制清单（MySQL/Redis 包整理成本高） |
| 多应用包 | 不做 | 一个包一个应用，与「应用管理」一一对应 | 一个包含多应用（与发布中心模型冲突） |
| 执行引擎 | 复用 ServiceManager + IDeployStrategy | 已有守护、内存限制、健康检查、崩溃重启 | 新写进程管理器（双倍维护） |
| `star` 入口 | StarAgent 的别名 | 与用户理解一致，不新增二进制 | 重命名 StarAgent.exe（影响服务身份/升级） |
| 包来源 | 本机代理连接的 StarServer | 企业内部、可控、可审计 | 公共 registry（生态成本高） |
| CLI 与服务端通信 | 优先本机 RPC，代理未运行才直连 | 避免同节点会话互踢 | 始终直连（会顶掉代理会话） |
| 默认运行方式 | 一律前台，守护使用 `-d` | 规则简单，不在部署集上增加开关 | 按包配置默认模式（多一个字段和分支） |
| 参数传递 | 原样追加启动参数 | 发布中心与包管理使用同一套明确参数 | 变量替换（发布中心不适用） |
| 版本选择 | 当前版本 + 平台 + 框架 | 新上传版本不自动生效，管理员显式应用 | 最新上传（可能尚未准备好） |

---

## 12. 安全

| 风险 | 应对 |
|---|---|
| 任意节点上传包 | 上传只走 Web 控制台，需要管理员权限；节点 API 只读 |
| 下载即执行 | HTTPS + MD5 校验 + 管理端审核；后续可加签名 |
| zip 路径穿越 | 上传时校验条目路径；客户端解压后再校验所有文件在目标目录内 |
| 包内路径逃逸 | `fileName` / `workingDirectory` 禁止绝对路径和 `..` |
| 端口冲突 | 清单声明 `ports`，启动前检查，冲突时明确报错 |
| 会话互踢 | 代理运行时走本机 RPC，不重复登录 |
| 本地 RPC 被滥用 | 复用 `LocalApiToken` 与 `CheckLocal` 校验 |
| 包体过大 | 复用 `StarServerSetting.MaxUploadSize` |

---

## 13. 兼容与数据库变更

### 13.1 兼容

- `AppDeploy` / `AppDeployVersion` 复用现有列，不新增包专属字段，老数据不受影响。
- 旧 `Package` API 路径与 DTO 保持兼容；`Manifest` 仍由服务端按部署集合成，不落库。
- 混合版本：旧服务端或旧代理不返回包接口时，CLI 提示升级 StarAgent 或 StarServer；代理在跑时不会改直连，避免同节点会话把代理顶下线。
- `Stardust` 库目标含 `net45`，禁用高版本 BCL API：
  - `Process.Kill(true)` 用 `#if NET5_0_OR_GREATER`，低版本用 `taskkill /T /F` 或 `pkill -P`。
  - JSON 用 NewLife `ToJsonEntity` / `ToJson`，不用 `System.Text.Json`。
- Linux 解压后保留可执行权限；`DeployStrategyBase` 已有 `chmod +x`。
- Windows 用 `star.cmd`，Linux/macOS 用 `star` 脚本；路径含空格要加引号。

### 13.2 数据库变更（生产必读）

XCode `Migration=On` 只会新建表/列，**不会删除旧唯一索引**。升级时把原来的唯一索引改成普通索引：

```sql
-- MySQL
DROP INDEX IU_AppDeployVersion_DeployId_Version ON AppDeployVersion;
CREATE INDEX IX_AppDeployVersion_DeployId_Version
  ON AppDeployVersion (AppId, Version);
```

```sql
-- SQL Server
DROP INDEX IU_AppDeployVersion_DeployId_Version ON AppDeployVersion;
CREATE INDEX IX_AppDeployVersion_DeployId_Version
  ON AppDeployVersion (AppId, Version);
```

```sql
-- SQLite（开发/测试）
DROP INDEX IF EXISTS IU_AppDeployVersion_DeployId_Version;
DROP INDEX IF EXISTS IU_AppDeployVersion_DeployId_Version_OS_Arch_TargetFramework;
CREATE INDEX IF NOT EXISTS IX_AppDeployVersion_DeployId_Version
  ON AppDeployVersion (AppId, Version);
```

> 旧表 `AppPackage` / `AppPackageVersion` 确认不再使用后可手工删除；不再由 Model.xml 生成。

---

## 14. 测试计划与结果

| 测试 | 内容 | 结果 |
|---|---|---|
| `StarPackageTests` | 合法清单、缺字段、非法包名、绝对路径、`..`、未知 schema | ✅ 通过 |
| `StarPackageStoreTests` | 解压、版本目录、current、哈希不匹配、zip-slip、删除、服务端清单无 star.json | ✅ 通过 |
| `PackageManifestReaderTests` | 根目录清单、缺失清单、非法清单、`TryRead` 可选读取 | ✅ 通过 |
| `PackageServiceTests` | 当前版本、平台匹配、指定版本、目标框架、项目/短名解析、禁用过滤、搜索 | ✅ 通过 |
| `PackageControllerTests` | 节点鉴权、搜索、当前版本、下载令牌 | ✅ 通过 |
| `AppPackageHelperTests` | 可选清单、项目自动关联、运行参数回填、单应用校验 | ✅ 3 例通过 |
| 部署相关 ServerTest | 部署版本匹配、上传安全、安装回填等 | ✅ 106 例通过 |
| `Stardust.Tests` 包相关 | CLI 命令解析、清单、存储 | ✅ 65 例通过 |
| 控制台合并包上传（E2E） | 隔离站点创建 `NewLife/StarServer`，上传带清单 zip 自动关联项目并回填运行参数；再上传无清单 zip；旧 `AppPackageVersion` 路由返回 404 | ✅ 2026-11-24 手工验证 |
| `star run -d NewLife/StarServer` / `StarWeb` | 真实二进制包升级/回滚 | ⏳ 待按 README 打包验收 |

---

## 15. 实施切片（合并后）

| 切片 | 内容 | 主要文件 |
|---|---|---|
| S1 数据模型 | `AppDeploy` 复用现有运行参数；`AppDeployVersion` 改为普通 `(DeployId,Version)` 索引；删除旧包表 | `Stardust.Data/Deployment/Model.xml` |
| S2 数据层 | `FullName`、`Refresh` 节点计数、`GetDeployVersion` 平台化、`MatchFramework`、`SearchPackages` 当前版本过滤 | `应用部署.Biz.cs`、`部署版本.Biz.cs` |
| S3 服务端 API | `PackageService` 改为查询部署集/版本并合成清单 | `Stardust.Server/Services/PackageService.cs` |
| S4 上传/控制台 | 复用部署集/版本控制器，可选 `star.json`，填充运行参数；列表展示当前版本 | `Stardust.Web/.../AppDeployController.cs`、`AppDeployVersionController.cs` |
| S5 客户端 | `StarPackageStore` 服务端清单安装；RPC/CLI 传递清单 | `Stardust/Deployment/StarPackageStore.cs`、`StarAgent/*` |
| S6 测试 | 包服务、API、存储、清单、部署匹配 | `Stardust.Tests/Packages/*`、`Stardust.ServerTests/*` |
| S7 文档/示例 | PUB-25/PUB-19/架构/需求/功能清单/README；拆 `newlife-starserver` + `newlife-starweb` | `Doc/*`、`Samples/Packages/*` |

---

## 16. 明确不做

- 公共包市场、评分、搜索排名。
- 官方 Redis/MySQL 仓库（只提供内部上传方式）。
- 容器隔离、网络隔离、文件系统隔离。
- 一个包含多个应用的包；多应用请拆成多个部署集。
- 自动更新检查/定时升级（后续可做 `star upgrade`）。
- `star logs`（v1 先看代理日志文件，v2 再按包过滤）。
- 包签名与证书体系（v1 只做哈希校验）。
- 服务端新存储：复用 Attachment / IFileStorage。
- 第二套进程管理器。

---

## 17. 风险与待确认

| 项 | 说明 | 建议 |
|---|---|---|
| 旧唯一索引 | 生产库不删旧索引时，同版本多 RID 上传会失败 | 按 13.2 改为普通索引 |
| 旧包表数据 | 已上传的 AppPackage 数据不会自动迁移 | 手工重新上传到部署集，或写一次性迁移脚本 |
| `ServiceInfo` 新字段的混合版本 | 旧服务端是否忽略未知 JSON 字段 | 发布前用旧版本服务端做一次兼容测试 |
| 前台退出 | 孤儿进程 | Windows `Kill(true)`，低版本 `taskkill /T`；Linux `pkill -P` |
| MySQL 首次初始化 | `mysqld --initialize` 不是普通启动 | v1 文档说明；v2 增加 `setup` 脚本 |
| 代理目录权限 | 普通用户无法写代理目录 | 守护包由代理写；前台包写用户缓存 |
| 包体上传上限 | 大包可能超过 100MB | 复用 `MaxUploadSize`，文档提示调整 |
| `star` 与 `staragent` 重名 | 两个命令并存 | 文档明确：`star` 是代理别名，`staragent` 是 dotnet tool |

---

## 18. 实现位置

| 能力 | 实现位置 |
|---|---|
| 包清单模型与校验 | `Stardust/Models/StarPackage.cs` |
| 包信息与 RPC 结果 DTO | `Stardust/Models/PackageInfo.cs`、`StarPackageResults.cs` |
| 本地包仓库 | `Stardust/Deployment/StarPackageStore.cs` |
| 服务端数据 | `Stardust.Data/Deployment/应用部署.cs`、`部署版本.cs`（Model.xml） |
| 清单读取（上传校验） | `Stardust.Data/Deployment/PackageManifestReader.cs` |
| 上传回填 | `Stardust.Data/Deployment/AppPackageHelper.cs` |
| 平台/框架匹配 | `Stardust.Data/Deployment/PlatformHelper.cs`、`部署版本.Biz.cs` |
| 节点 API | `Stardust.Server/Controllers/PackageController.cs`、`Services/PackageService.cs` |
| 控制台 | `Stardust.Web/Areas/Deployment/Controllers/AppDeployController.cs`、`AppDeployVersionController.cs` |
| star 命令 | `StarAgent/PackageCommand.cs`、`StarAgent/StarLauncher.cs`、`StarAgent/Program.cs` 入口分发 |
| 本机 RPC | `StarAgent/StarService.cs` 的 `PackageSearch/Get/GetLatest/Fetch/Install/Stop/Start/Remove/List` |
| 服务字段 | `Stardust/Models/ServiceInfo.cs` 的 `Package/PackageVersion/PackageApp` |
| 测试 | `Stardust.Tests/Packages/*`、`Stardust.ServerTests/Deployment/PackageServiceTests.cs`、`Controllers/PackageControllerTests.cs` |

---

## 19. 验证结果（2026-11-24）

| 验证 | 结果 |
|---|---|
| 全解决方案编译 | ✅ `星尘.slnx` 0 错误 |
| `PackageServiceTests` + `PackageControllerTests` | ✅ 13 通过 |
| 部署相关 ServerTest | ✅ 73 通过 |
| `Stardust.Tests` 包相关 | ✅ 65 通过 |
| 服务端清单安装（zip 无 star.json） | ✅ 单元测试通过 |
| 本地 zip 缺 star.json | ✅ 明确报错 |
| 控制台合并包上传 | ✅ 隔离 StarWeb：创建 `NewLife/StarServer`，上传带清单 zip 自动关联项目、回填 `FileName/Arguments/HealthCheck`；无清单 zip 使用表单版本 |
| 旧应用包路由 | ✅ `/Deployment/AppPackageVersion/Add` 返回 404 |
| 真实 StarServer+StarWeb 二进制包 | ⏳ 按 `Samples/Packages/newlife-starserver`、`newlife-starweb` 打包验收 |

> 2026-11-24 合并时修复：`SearchPackages` 空值过滤、`MatchFramework` 对 `net8.0` 形式框架的匹配、服务端清单安装不依赖 zip 内 `star.json`、`star rm` 孤儿进程与目录占用。