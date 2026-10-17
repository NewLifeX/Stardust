# 原生 Agent 构建与发布

本文是 Go / Rust / C++ 原生 StarAgent 的**统一构建与发布说明**，包括产品编码约定、输出目录、支持平台和升级包匹配规则。

## 1. 产品编码（ProductCode）

每种实现使用独立的 `ProductCode`，不要共用标准 .NET Agent 的 `StarAgent`：

| 实现 | ProductCode | 升级包 `TargetRuntime` |
|------|-------------|------------------------|
| .NET StarAgent | `StarAgent` | `6` / `7` / `8` / `9` / `10` 等 |
| Go 原生 Agent | `StarAgent-Go` | `go` |
| Rust 原生 Agent | `StarAgent-Rust` | `rust` |
| C++ 原生 Agent | `StarAgent-Cpp` | `cpp` |
| 移动端 .NET Agent | `StarAgent-Mobile` | —（不走自升级） |

**不建议把平台写进 ProductCode**（例如 `StarAgent-Go-Linux-Amd64`）：

- 服务端登录时会校验节点原有 `ProductCode` 与新登录值一致，ProductCode 变化会导致老节点被判定为“产品编码不符”。
- `ProductRelease` 按 `ProductCode` 精确过滤；平台拆分后每个平台都要维护独立版本记录。
- 平台差异已经由 `Node.OSKind` / `Node.Architecture` 与 `ProductPackage.OSKind` / `ProductPackage.Architecture` 表达，升级时先按产品编码隔离，再按平台选择安装包。

因此：**ProductCode 区分实现，平台字段区分平台。**

## 2. 输出目录

构建脚本默认输出到仓库根目录：

```text
Bin/Agent/native/
├── BUILD-INFO.md                       # 本次构建清单（大小、SHA256、跳过项）
├── go/
│   ├── linux-amd64/staragent
│   ├── linux-arm64/staragent
│   ├── linux-armv7/staragent
│   ├── windows-amd64/staragent.exe
│   ├── windows-arm64/staragent.exe
│   ├── darwin-amd64/staragent
│   └── darwin-arm64/staragent
├── rust/
│   └── windows-amd64/staragent.exe    # 本机工具链可构建的目标
└── cpp/
    └── windows-amd64/staragent.exe    # 本机工具链可构建的目标
```

`Bin/` 已被 `.gitignore` 忽略，构建产物不会进入源码提交。每次运行脚本会清理对应语言的旧产物，并重新生成 `BUILD-INFO.md`。

移动端产物与原生 Agent 统一挂在 `Bin/Agent/mobile/`：

```text
Bin/Agent/mobile/
├── android/
│   ├── com.newlife.staragent-Signed.apk   # dotnet build 产物
│   ├── apk/StarAgent-Aot.apk              # pack-android.ps1 打包
│   └── artifacts/                         # run-android.ps1 日志
└── ios/
    ├── *.app                              # dotnet build 产物
    └── artifacts/ios-smoke.json           # run-ios.sh 冒烟结果
```

移动端构建命令：

```powershell
pwsh ./StarAgent.Android/pack-android.ps1 -Mode Aot
pwsh ./StarAgent.Android/run-android.ps1 -Smoke
bash ./StarAgent.iOS/run-ios.sh
```

## 3. 构建命令

```powershell
# 构建全部本机可构建目标（Go 全平台 + 本机 Rust/C++）
pwsh ./StarAgent.Native/packaging/build.ps1

# 只构建 Go（Go 可在任意平台交叉编译全部目标）
pwsh ./StarAgent.Native/packaging/build.ps1 -SkipRust -SkipCpp

# 指定输出目录
pwsh ./StarAgent.Native/packaging/build.ps1 -OutDir D:\StarAgent\dist
```

构建完成后打开 `Bin/Agent/native/BUILD-INFO.md` 查看每个发行文件的大小与 SHA256。

## 4. 平台支持矩阵

当前按 **3 个 OS 家族**设计：Windows、Linux（含 OpenWrt/musl）、macOS。

| 实现 | Windows x64 | Windows ARM64 | Linux x64 | Linux ARM64 | Linux ARMv7 | macOS x64 | macOS ARM64 |
|------|:-----------:|:-------------:|:---------:|:-----------:|:-----------:|:---------:|:-----------:|
| Go | ✅ 本机 | ✅ 本机 | ✅ 本机 | ✅ 本机 | ✅ 本机 | ✅ 本机 | ✅ 本机 |
| Rust | ✅ 本机 | 需 MSVC ARM64 链接器 | 需 musl 交叉工具链 | 需 musl 交叉工具链 | — | 需 macOS SDK | 需 macOS SDK |
| C++ | ✅ 本机 | 需 MSVC ARM64 工具链 | 源码支持，需 Linux 构建 | 源码支持，需交叉工具链 | 源码支持，需交叉工具链 | 源码支持，需 macOS 构建 | 源码支持，需 macOS 构建 |

说明：

- **Go**：`CGO_ENABLED=0`，7 个目标均可在本机一次构建完成。
- **Rust**：Windows x64 已实测；Linux musl 交叉需要 `musl-gcc` 或 `cargo-zigbuild`，macOS 需要 Apple SDK，通常在对应平台或 CI 构建。
- **C++**：Windows x64 已实测；Linux/macOS 源码路径完整，需要对应平台工具链与 OpenSSL/IXWebSocket 依赖。
- 标记“本机”表示当前 Windows 构建机可直接产出；其余目标需要在对应平台或 CI 上补齐。

按语言汇总：

| 实现 | 可发布目标数 | 说明 |
|------|-------------:|------|
| Go | 7 | linux amd64/arm64/armv7、windows amd64/arm64、darwin amd64/arm64 |
| Rust | 5 | windows x64、linux musl x64/arm64、macOS x64/arm64 |
| C++ | 5 | windows x64、linux x64/arm64、macOS x64/arm64 |

> 平台组合合计不是简单相加：不同语言覆盖的目标集合有重叠，按“语言 + OS + 架构”去重后共 **17 个可发布组合**。本机一次构建实际产出通常为 **9 个文件**（Go 7 + Rust Windows x64 + C++ Windows x64）。

## 5. 升级包匹配

服务端升级流程：

1. `ProductRelease.ProductCode` 必须等于节点 `Node.ProductCode`，先隔离不同实现。
2. 在版本内按 `ProductPackage.OSKind` / `Architecture` 过滤平台包，精确匹配优先于 `Any`。
3. 原生 Agent 使用 `go` / `rust` / `cpp` 匹配 `TargetRuntime`；.NET Agent 使用主版本号。
4. 原生 Agent 首版不执行自更新，收到升级响应只记录日志并上报事件，由运维手动替换二进制。

发布原生 Agent 时，请确保 `ProductRelease` 与 `ProductPackage` 的产品编码、OSKind、Architecture、TargetRuntime 与上表一致。

## 6. 本次实测产物

2026-10-17 在 Windows 10 x64 构建机上运行 `build.ps1` 的结果：

| 实现 | 目标平台 | 文件大小 |
|------|----------|---------:|
| Go | linux-amd64 | 7.55 MB |
| Go | linux-arm64 | 7.00 MB |
| Go | linux-armv7 | 7.50 MB |
| Go | windows-amd64 | 7.50 MB |
| Go | windows-arm64 | 6.80 MB |
| Go | darwin-amd64 | 7.68 MB |
| Go | darwin-arm64 | 7.14 MB |
| Rust | windows-amd64 | 5.13 MB |
| C++ | windows-amd64 | 0.83 MB |

> 以上为单文件原始大小，实际发布包的大小与 SHA256 以 `Bin/Agent/native/BUILD-INFO.md` 为准。当前 C++ 本机构建未找到 OpenSSL，仅支持 `http/ws`；生产环境需安装 OpenSSL 后重新构建以启用 `https/wss`。

三种实现的 `once` 模式均已在公网 StarServer `http://star.newlifex.com:6600` 完成登录与心跳验证。
