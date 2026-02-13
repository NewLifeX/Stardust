# Stardust SDK

本目录包含星尘（Stardust）各语言的 SDK 实现。

## 目录结构

```
SDK/
├── ASP/                # Classic ASP (VBScript) SDK
│   ├── src/            # SDK 核心文件
│   └── examples/       # 示例程序
├── Go/                 # Go 语言 SDK
│   ├── stardust/       # SDK 核心包
│   └── examples/       # 示例程序
├── JavaScript/         # JavaScript/Node.js SDK
│   ├── src/            # SDK 核心模块
│   └── examples/       # 示例程序
├── PHP/                # PHP SDK
│   ├── src/            # SDK 核心类
│   └── examples/       # 示例程序
└── Python/             # Python SDK
    ├── stardust_sdk/   # SDK 核心包
    └── examples/       # 示例程序
```

## 支持的语言

### ASP SDK ✅

适用于 Classic ASP (VBScript) 环境，包含：
- 登录认证、心跳保活
- APM 监控（链路追踪）
- 示例程序

查看 [ASP SDK 文档](ASP/README.md)

### Go SDK ✅

完整实现，包含：
- APM 监控（链路追踪）
- 配置中心
- 完整的单元测试
- 示例程序

查看 [Go SDK 文档](Go/README.md)

### 其他语言

其他语言的 SDK 文档和示例代码请参考各 SDK 目录或 [/Doc/SDK/](/Doc/SDK/) 目录：

- [JavaScript SDK](JavaScript/README.md) | [文档](/Doc/SDK/stardust-sdk-javascript.md)
- [PHP SDK](PHP/README.md) | [文档](/Doc/SDK/stardust-sdk-php.md)
- [Python SDK](Python/README.md) | [文档](/Doc/SDK/stardust-sdk-python.md)
- [Java SDK 文档](/Doc/SDK/stardust-sdk-java.md)

## 贡献

欢迎为其他语言贡献 SDK 实现！请参考 Go SDK 的实现方式。

## License

MIT License
