# PUB-20 部署代理（DeployAgent）

> 版本：v1.0 | 日期：2026-07-15
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

## 代码仓库结构

```
my-repo/
├── build/
│   └── build.sh      # 构建脚本
└── publish/          # 产物输出目录
```

## 编译命令参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| Repository | Git 仓库地址。支持 SSH（`git@host:repo.git`）和 HTTP（`http://user@host/repo.git`）格式 | - |
| Branch | 分支名 | main |
| SourcePath | 本地源码路径（优先于 Repository） | - |
| BuildArgs | 编译参数 | - |
| OutputPath | 编译输出目录 | publish |
| PackageFilters | 打包过滤器 | - |
| PullCode | 是否拉取代码 | false |
| BuildProject | 是否编译项目 | false |
| PackageOutput | 是否打包输出 | false |
| UploadPackage | 是否上传应用包 | false |

## 工作流程

```
星尘服务端 → deploy/compile 命令 → DeployAgent
    ↓
Git clone/pull 拉取代码（PullCode=true）
    ↓
dotnet restore/build/publish（BuildProject=true）
    ↓
按 PackageFilters 打包（PackageOutput=true）
    ↓
上传 zip 到服务端（UploadPackage=true）
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
