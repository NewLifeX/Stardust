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
| Repository | Git 仓库地址 | - |
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
