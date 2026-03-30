# DeployAgent 使用指南

## 一、功能说明

DeployAgent 是星尘平台的部署客户端，支持两种运行模式：

- **服务模式**：作为后台服务运行，接收星尘服务端的编译命令，自动完成拉取代码、编译、打包、上传全流程
- **命令行模式**：通过命令行直接执行打包等操作

### 命令行模式

```bash
# 打包命令
stardeploy pack <输出zip> [文件模式...]

# 示例：打包所有可执行文件
stardeploy pack app.zip *.exe *.dll *.runtimeconfig.json ./Config/*.config

# 打包整个目录（加 -r 递归压缩）
stardeploy pack app.zip -r ./publish/
```

### 服务模式

不带命令行参数直接运行，DeployAgent 将连接星尘服务端，等待接收 `deploy/compile` 编译命令。

## 二、代码仓库结构

```
my-repo/
├── build/
│   └── build.sh      # 构建脚本
└── publish/          # 产物输出目录
```

## 三、构建脚本示例

在代码仓库中创建 `build/build.sh`：

```bash
#!/bin/bash
set -e

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$BUILD_DIR")"
PUBLISH_DIR="$PROJECT_ROOT/publish"

mkdir -p "$PUBLISH_DIR"
cd "$PROJECT_ROOT"

# .NET 项目构建
dotnet restore
dotnet build -c Release
dotnet publish -c Release -o "$PUBLISH_DIR"
```

## 四、环境要求

- **Git for Windows**（包含 Git Bash）
- **.NET SDK 8.0+**
- **星尘平台**（StarServer 运行中）

## 五、编译和运行

```bash
# 编译
dotnet build DeployAgent\DeployAgent.csproj -c Release

# 服务模式运行（监听星尘命令）
dotnet DeployAgent\bin\Release\net10.0\DeployAgent.dll

# 命令行打包
dotnet DeployAgent\bin\Release\net10.0\DeployAgent.dll pack app.zip -r ./publish/
```

## 六、编译命令参数

在星尘服务端发送编译命令时，支持以下参数：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| Repository | Git 仓库地址 | - |
| Branch | 分支名 | main |
| SourcePath | 本地源码路径（优先于 Repository） | - |
| ProjectPath | 项目相对路径 | - |
| BuildArgs | 编译参数 | - |
| OutputPath | 编译输出目录 | publish |
| PackageFilters | 打包过滤器，支持通配符，分号隔开 | - |
| DeployName | 应用部署集名称 | - |
| PullCode | 是否拉取代码 | false |
| BuildProject | 是否编译项目 | false |
| PackageOutput | 是否打包输出 | false |
| UploadPackage | 是否上传应用包 | false |

## 七、工作流程

```
星尘服务端 → deploy/compile 命令 → DeployAgent
    ↓
Git clone/pull 拉取代码（PullCode=true）
    ↓
执行 build/build.sh 或 dotnet publish（BuildProject=true）
    ↓
打包 publish/ 目录为 zip（PackageOutput=true）
    ↓
上传到星尘平台（UploadPackage=true）
    ↓
完成
```

## 八、部署模式与覆盖文件

### 部署模式

星尘支持多种部署模式，通过应用部署配置选择：

| 模式 | 值 | 说明 |
|------|----|----|
| Standard | 10 | 解压到工作目录运行，推荐 |
| Shadow | 11 | 解压到影子目录运行，工作目录干净，支持热更新 |
| Hosted | 12 | 仅解压不运行，适合 IIS/Nginx 托管 |
| Task | 13 | 一次性任务 |

### 覆盖文件（Overwrite）

在应用版本中设置**覆盖文件**，指定需要从部署包拷贝到工作目录的文件或子目录。支持 `*` 通配符，多项以 `;` 分隔。

**示例**：`wwwroot/*;appsettings.Production.json`

- **标准模式**：zip 直接解压到工作目录，所有文件自然存在
- **影子模式**：zip 解压到影子目录，仅配置文件和覆盖文件列表中匹配的文件/目录会拷贝到工作目录

> ⚠️ 影子模式下，如果应用需要访问 `wwwroot` 等静态资源目录，必须在覆盖文件中配置 `wwwroot/*`，否则工作目录中不会有这些文件。

### 发布模式（版本级别）

| 模式 | 值 | 说明 |
|------|----|----|
| Partial | 1 | 增量包，仅覆盖 |
| Standard | 2 | 标准包，清空可执行文件再覆盖 |
| Full | 3 | 完整包，清空所有文件 |
