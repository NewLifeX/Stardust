# PUB-20 部署代理（DeployAgent）

> 版本：v1.1 | 日期：2026-10-20（v1.0：2026-07-15）
> 对应需求：PUB-20 编译节点/部署代理

---

## 功能说明

DeployAgent 是星尘平台的部署客户端，支持两种运行模式：

- **服务模式**：作为后台服务运行，接收星尘服务端的编译命令，自动完成拉取代码、编译、打包、上传全流程
- **命令行模式**：通过命令行直接执行打包操作

## 命令行模式

```bash
# 打包命令
stardeploy pack <输出zip> [文件模式...]

# 示例：打包所有可执行文件
stardeploy pack app.zip *.exe *.dll *.runtimeconfig.json ./Config/*.config

# 打包整个目录（加 -r 递归压缩）
stardeploy pack app.zip -r ./publish/
```

## 服务模式

不带命令行参数直接运行，DeployAgent 将连接星尘服务端，等待接收 `deploy/compile` 编译命令。

## 项目类型与编译方式（ProjectKind）

AppDeploy 实体上的「项目类型」（`ProjectKind`，默认 `DotNet=1`）决定编译节点上的编译方式
（枚举定义 `Stardust/Models/ProjectKinds.cs`，执行逻辑 `DeployAgent/DeployService.cs` 的 `BuildProject`）：

| 值 | 名称 | 编译方式 | 命令 |
|----|------|----------|------|
| 1 | DotNet | dotnet SDK 编译 | `dotnet publish "{ProjectPath}" -o "{OutputPath}" {BuildArgs}` |
| 2 | MSBuild | MSBuild 编译 | `msbuild "{ProjectPath}" /p:OutputPath="{OutputPath}" {BuildArgs}` |
| 99 | Custom | 自定义构建脚本 | 执行脚本（见下节），`BuildArgs` 被忽略 |

## 自定义构建脚本（ProjectKind=Custom）

应用部署集的「项目类型」选 **Custom(99)** 时，编译节点上的 DeployAgent 不执行 dotnet/msbuild，
改为执行仓库里预设的构建脚本（`DeployAgent/DeployService.cs` → `BuildProject` case 99 → `ExecuteBuildScript`）。
适用于前端（npm/pnpm + vite/webpack）、脚本语言等非 dotnet 项目。

### 脚本入口怎么配置

「应用部署」编辑页 **编译参数** 分组：

| 字段 | 填法 | 说明 |
|------|------|------|
| 项目类型 ProjectKind | `Custom` | 触发自定义脚本编译 |
| 项目路径 ProjectPath | 留空 或 脚本相对路径 | 留空 = 约定路径 `{仓库根}/build/build.sh`；填写 = 该值就是**脚本文件路径**（相对仓库根），如 `web/build.sh` |
| 编译参数 BuildArgs | （Custom 下无效） | 代码里 Custom 分支不使用 BuildArgs，既不拼命令也不传给脚本；脚本参数请写死或读仓库内配置文件 |

> 语义差异：DotNet/MSBuild 的 ProjectPath 指「项目文件路径」，Custom 指「**脚本文件路径**」。

同时在「编译节点」（AppBuildNode）上配置执行步骤：

| 字段 | 填法 | 说明 |
|------|------|------|
| 拉取源代码 PullCode | ✔ | 先 git clone/pull（配置了源代码目录 SourcePath 则用固定目录，否则克隆到临时目录） |
| 编译项目 BuildProject | ✔ | 触发脚本执行 |
| 输出目录 OutputPath | 留空（默认 `publish`）或 `dist` 等 | **脚本产物的落点目录**（相对仓库根），也是后续打包目录 |
| 打包输出 PackageOutput | ✔ | 将输出目录压缩为 zip |
| 上传应用包 UploadPackage | ✔ | 上传 zip 并自动创建部署版本 |

### 脚本放哪里

脚本必须**提交在代码仓库里**（Agent 克隆/拉取代码后才能执行）：

```
my-repo/
├── build/
│   └── build.sh      # ProjectPath 留空时的默认脚本位置
└── web/              # 前端子项目（示例）
    └── ...
```

想放别的位置，就把 ProjectPath 指过去（如 `web/build.sh`、`scripts/ci/build.sh`）。
Agent 以**仓库根**为工作目录执行脚本（Linux 直接 `bash "{script}"` 并 chmod +x；Windows 用 **Git Bash** 执行，未装 Git for Windows 会直接报错——见「环境要求」）。
建议脚本开头自行 cd 到稳定位置：

```bash
cd "$(dirname "$0")/.."    # 从 build/ 回到仓库根
```

执行环境注意：
- 脚本依赖的工具链（node/npm/pnpm 等）需在编译节点上预装
- **整个脚本限时 10 分钟**（DeployAgent 对外部进程统一 600 秒超时）；前端依赖安装慢时，给编译节点配 `SourcePath` 固定源码目录以复用 node_modules
- 保持 LF 行尾：仓库加 `.gitattributes`（`*.sh text eol=lf`），避免 Windows 编辑后 CRLF 导致 `bash\r` 报错

### 环境变量注入（现状）

**平台目前不向脚本注入任何构建相关变量**（ProjectPath/OutputPath 等）。
`CreateProcessStartInfo` 只额外设置 Git 交互禁用两个变量（`GIT_TERMINAL_PROMPT=0`、`GCM_INTERACTIVE=Never`，`DeployAgent/DeployService.cs` 的 `CreateProcessStartInfo`），
未清空子进程环境 → **脚本继承 DeployAgent 服务进程的环境**，即编译节点上配置的系统/服务环境变量脚本都能读到。

推荐模式（有就用、没有按默认，`bash` 内建写法）：

- `SRC_DIR="${SRC_DIR:-web}"`：前端工程目录（相对仓库根），节点级覆盖用（仓库目录调整时只改节点环境变量，不改脚本）
- `OUT_DIR="${OUT_DIR:-publish}"`：**必须与编译节点的「输出目录」一致**（该值按编译节点逐节点可配，脚本无法写死）——这是环境变量注入最有价值的点

平台原生注入 `OutputPath` 等变量属于潜在增强项（`CompileCommand`/`CreateProcessStartInfo` 改造），当前尚未实现，勿依赖。

**脚本可以自己拿到仓库绝对路径，但不要依赖**：执行时工作目录就是仓库根，`pwd -P` / `$PWD` 即绝对路径；脚本嵌套深时 `git rev-parse --show-toplevel` 最稳。然而默认（无 SourcePath）模式下仓库在临时目录 `stardust-build-{GUID}/repo`，每次构建不同、结束即删——脚本应全程使用相对路径，产物放进输出目录即可（Agent 打包只认「仓库根 + OutputPath」的相对定位）。

默认值与仓库不匹配 = 构建失败：`${SRC_DIR:-web}` 在无注入时等价写死 `web`，仓库无 `web/` 目录时 `cd` 失败 → `set -e` 退出码 1 → Agent 报编译失败。**把默认值改成仓库里实际的前端目录名；前端工程就在仓库根时用 `.`**（此时构建命令的 outDir 相应改为 `"$OUT_DIR"`，不再向上一级）。

### 脚本怎么写

职责边界——脚本只做三件事，其余交给 Agent：

1. 安装/还原依赖
2. 执行构建
3. 把**最终产物**放进输出目录（编译节点的 OutputPath，默认 `publish/`）

**极简默认脚本**（放 `build/build.sh`，可复制即用）：

```bash
#!/usr/bin/env bash
set -euo pipefail

# 可选环境变量：SRC_DIR 前端工程目录、OUT_DIR 产物输出目录（未设置时用默认值）
SRC_DIR="${SRC_DIR:-web}"
OUT_DIR="${OUT_DIR:-publish}"

cd "$(dirname "$0")/.."            # 仓库根
cd "$SRC_DIR"                      # 前端工程目录

if [ -f pnpm-lock.yaml ]; then     # 有 pnpm 锁文件用 pnpm，否则 npm
  pnpm install --frozen-lockfile
  pnpm run build -- --outDir "../$OUT_DIR" --emptyOutDir
else
  npm ci
  npm run build -- --outDir "../$OUT_DIR" --emptyOutDir
fi
```

> 补充可选注入：`NPM_REGISTRY="${NPM_REGISTRY:-}"`，非空时依赖安装前执行 `npm config set registry "$NPM_REGISTRY"`（pnpm 同理 `pnpm config set registry`），用于内网源加速。默认脚本刻意不写，保持极简。
>
> 通用建仓模板（任选）：`set -euo pipefail`、`cd "$(dirname "$0")/.."` 回仓库根、按 `"${VAR:-默认}"` 读取 `SRC_DIR`/`OUT_DIR` 环境变量、依赖安装、构建产物输出到 `OUT_DIR` 对应路径（等价表达：`vite build --outDir "../$OUT_DIR" --emptyOutDir`）。

要点：
- **退出码非 0 = 编译失败**：`set -e` 让任一步失败即退出，Agent 终止任务并把脚本 stdout/stderr 回传到部署历史（单条事件约 2000 字符上限、保留尾部），错误信息尽量放日志最后
- 脚本与 DotNet 一样走 `deploy/compile`，无需感知星尘的存在；不要在脚本里自己打包 zip 或调上传接口——脚本成功后 Agent 统一打包上传
- 不要动仓库里的 `.git`（Agent 依赖它读取提交号/提交说明，自动附带在部署版本上）
- 节点级定制走环境变量（继承自 DeployAgent 服务进程），同名变量未设置时脚本使用内置默认值——见「环境变量注入（现状）」

### 脚本产出什么 / 做到什么程度

做到「**输出目录里是可直接部署的完整产物 + 退出 0**」即可。脚本成功后 Agent 自动接管：

1. **打包**：把 `{仓库根}/{OutputPath}` 整体压缩为 `{部署集名}-{yyyyMMdd-HHmmss}.zip`。包内为相对输出目录的路径（**无顶层目录**）。「打包过滤器 PackageFilters」留空=全部；填写则按文件名通配符递归匹配（如 `*.html;*.js;*.css`，分号分隔）
2. **上传**：调服务端 `/Deploy/UploadBuildFile`，自动创建「部署版本」，版本号 `v{yyyyMMdd-HHmmss}`，附带 Git 提交信息
3. **发布**：由「部署节点」按 AppDeploy 发布参数拉取该版本运行（前端纯静态包一般配合 nginx 部署）

### 完整配置示例（前端 vite 项目）

- 应用部署 → 编译参数：项目类型 `Custom`；项目路径 `build/build.sh`（或留空走默认位置）；分支 `main`
- 编译节点：拉取源代码 ✔、编译项目 ✔、输出目录 `publish`、打包输出 ✔、上传应用包 ✔；频繁构建建议配 SourcePath 复用 node_modules

## 代码仓库结构

DotNet 项目编译时只需给出 ProjectPath；Custom（脚本）项目的仓库结构见上文「脚本放哪里」：

```
my-repo/
├── build/
│   └── build.sh      # 自定义构建脚本（ProjectKind=Custom）
└── publish/          # 产物输出目录（OutputPath，脚本负责写入）
```

## 编译命令参数

`deploy/compile` 下发的完整参数见 `Stardust/Models/CompileCommand.cs`（AppDeploy 表单字段经 `Stardust.Web/Services/DeployService.cs` 组装）：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| Repository | Git 仓库地址。支持 SSH（`git@host:repo.git`）和 HTTP（`http://user@host/repo.git`）格式 | - |
| DeployKey | SSH 私钥（PEM 文本），用于非交互拉取私有仓库 | - |
| RepoUserName / RepoPassword | HTTPS 克隆用户名 / 密码（节点密钥 AES 加密下发） | - |
| Branch | 分支名 | main |
| SourcePath | 本地源码路径（优先于 Repository，目录含 .git 则可复用） | - |
| ProjectKind | 项目类型：1=DotNet，2=MSBuild，99=Custom（脚本） | 1 |
| ProjectPath | 项目/脚本路径（相对仓库根；见「项目类型与编译方式」） | - |
| BuildArgs | 编译参数（附加到 dotnet/msbuild 命令后；Custom 类型不使用） | - |
| OutputPath | 编译输出目录（相对仓库根），打包来源 | publish |
| PackageFilters | 打包过滤器（文件名通配符，分号分隔；留空打包输出目录全部文件） | - |
| DeployName | 应用部署集名称（用于上传建版本） | - |
| PullCode | 是否拉取代码 | false |
| BuildProject | 是否编译项目 | false |
| PackageOutput | 是否打包输出 | false |
| UploadPackage | 是否上传应用包 | false |

## 工作流程

```
星尘服务端 → deploy/compile 命令 → DeployAgent
    ↓
Git clone/pull 拉取代码（PullCode=true，无 SourcePath 时克隆到临时目录）
    ↓
编译（BuildProject=true，按 ProjectKind 分派）
    ├─ 1 DotNet  → dotnet publish "{ProjectPath}" -o "{OutputPath}" {BuildArgs}
    ├─ 2 MSBuild → msbuild "{ProjectPath}" /p:OutputPath="{OutputPath}" {BuildArgs}
    └─ 99 Custom → 执行仓库内构建脚本（默认 build/build.sh，见上文专节）
    ↓
读取 .git 提交信息（随版本附带）
    ↓
按 PackageFilters 打包输出目录（PackageOutput=true）
    ↓
上传 zip 到服务端（UploadPackage=true，自动创建部署版本）
```

## 环境要求

- Git for Windows（包含 Git Bash）
- .NET SDK 8.0+
- 星尘平台（StarServer 运行中）

## 仓库认证配置

使用 HTTP 格式仓库地址（`http://user@host/repo.git`）时，首次部署需要手动完成凭据缓存：

**Windows 机器**：在目标机器上手动执行一次 `git clone`，Git Credential Manager 会弹出认证窗口，输入密码后凭据即被缓存，后续 DeployAgent 可无交互拉取。

```powershell
# 在目标编译节点上手动执行一次
git clone http://username@git.example.com/group/project.git
# 输入密码后，凭据被 GCM 缓存，后续 DeployAgent 无需再次认证
```

**Linux 机器**：如果使用 HTTP 格式，必须使用带用户名的 HTTP 格式（`http://user@host/repo.git`），Linux 无 GUI 认证窗口，不带用户名会直接报错。建议配合 SSH Key（通过 `DeployKey` 参数）或提前配置 `git credential-store`。

## FAQ

### 上传失败：远程主机强迫关闭了一个现有的连接

**错误信息**：
```
System.Exception: 上传失败，已重试 3 次：Error while copying content to a stream.
 ---> System.Net.Http.HttpRequestException: Error while copying content to a stream.
 ---> System.IO.IOException: Unable to write data to the transport connection: 远程主机强迫关闭了一个现有的连接。.
 ---> System.Net.Sockets.SocketException (10054): 远程主机强迫关闭了一个现有的连接。
```

**原因**：上传的应用包文件过大，超过了服务端的请求体大小限制（默认 100MB，最大 1GB）。

**解决方案**：登录星尘平台，进入系统设置，调整 `MaxUploadSize` 配置项。该配置项控制 `/Deploy/UploadBuildFile` 接口允许的最大请求体字节数，默认值为 100000000（100MB），最大不超过 1GB（1073741824 字节）。根据实际需要调大该值，或设置为 0 使用 Kestrel 默认限制。
