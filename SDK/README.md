# Stardust SDK

星尘（Stardust）多语言 SDK。所有语言共用一套 HTTP 接入契约：**登录认证、心跳保活（应用性能）、埋点监控**，详见 [APM-17 监控接入 API](../Doc/APM-17-监控接入API.md)。

## 能力矩阵

| 语言/运行时 | 目录 | 登录 | 心跳+性能 | 埋点 | 配置中心 | 测试 | 状态 |
|-------------|------|:----:|:--------:|:----:|:--------:|:----:|------|
| C#/.NET | [Stardust](../Stardust) | ✅ | ✅ | ✅ | ✅ | ✅ | 已完备 |
| JavaScript（浏览器） | [JavaScript](JavaScript) | ✅ | ✅ | ✅ | - | ✅ | ✅ |
| JavaScript/Node.js | [JavaScript](JavaScript) | ✅ | ✅ | ✅ | - | ✅ | ✅ |
| Java 8+ | [Java](Java) | ✅ | ✅ | ✅ | 🔧 | ✅ | ✅ |
| Go 1.18+ | [Go](Go) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Python 3.7+ | [Python](Python) | ✅ | 🟡 | ✅ | ✅ | 🟡 | 🟡 |
| PHP 7.4+ | [PHP](PHP) | ✅ | 🟡 | ✅ | ✅ | 🟡 | 🟡 |
| Classic ASP | [ASP](ASP) | ✅ | 🟡 | ✅ | - | ❌ | 🟡 |

> ✅ 已实现并测试；🟡 部分实现；🔧 规划中；❌ 未开始。
> Python/PHP/ASP 的登录与埋点已可用，心跳应用性能（`AppInfo`）与令牌续期将在 P1 补齐。

## 快速开始

### JavaScript（浏览器，UMD 单文件）

```html
<script src="https://your-cdn/stardust/src/browser.js"></script>
<script>
  const tracer = Stardust.createTracer({
    server: 'http://star.example.com:6600',
    appId: 'MyWeb',
    // 内网/低敏感场景可直接使用 secret；正式环境推荐 tokenProvider
    tokenProvider: async () => (await fetch('/api/stardust-token')).json(),
    version: '1.2.3',
  });
  tracer.start();

  const span = tracer.newSpan('checkout');
  span.setTag('order=123');
  span.finish();
</script>
```

自动采集：页面加载、SPA 路由、JS 错误、资源错误、`fetch`/`XMLHttpRequest`；页面隐藏/卸载时通过 `sendBeacon` 冲刷数据。

### JavaScript（Node.js）

```javascript
const { StardustTracer } = require('./SDK/JavaScript');

const tracer = new StardustTracer({
  server: 'http://star.example.com:6600',
  appId: 'MyNodeApp',
  secret: 'MySecret',
  version: '1.2.3',
});
await tracer.start();

const span = tracer.newSpan('业务操作');
span.setTag('user=123');
span.finish();
```

### Java

```xml
<dependency>
  <groupId>com.newlife</groupId>
  <artifactId>stardust-sdk</artifactId>
  <version>1.0.0</version>
</dependency>
```

> 发布到 Maven Central 时，若 `com.newlife` 命名空间无法验证，可改用 GitHub 命名空间 `io.github.newlifex`。

```java
StardustTracer tracer = new StardustTracer("http://star.example.com:6600", "MyJavaApp", "MySecret");
tracer.setVersion("1.2.3").start();

try (Span span = tracer.newSpan("业务操作")) {
    span.setTag("order=123");
    doSomething();
}
tracer.stop();
```

详见 [Java SDK](Java/README.md)。

### Go

```go
import "github.com/NewLifeX/Stardust/SDK/Go/stardust"

tracer := stardust.NewTracer("http://star.example.com:6600", "MyGoApp", "MySecret")
tracer.SetVersion("1.2.3")
tracer.Start()
defer tracer.Stop()

span := tracer.NewSpan("业务操作", "")
span.Tag = "order=123"
span.Finish()
```

详见 [Go SDK](Go/README.md)。

## 统一接入要点

| 项目 | 约定 |
|------|------|
| 登录 | `POST /App/Login`，`AppId + Secret + ClientId + Version` |
| 心跳 | `POST /App/Ping?Token=...`，请求体为 `AppInfo`，含 CPU/内存/堆/线程/GC 等性能字段 |
| 埋点 | `POST /Trace/Report`，超过 1KB 用 `/Trace/ReportRaw` + GZip |
| `Version` | **必传**；服务端据此返回 `Excludes`、`EnableMeter` 等动态参数 |
| 令牌 | 到期前 5 分钟重登录；心跳可续签；`code=401` 重登录并重试一次 |
| 时间 | 埋点与 `Info.Time` 为 Unix 毫秒；`Info.StartTime` 为 ISO 8601 字符串 |
| 响应 | 统一 `{ code, data }`；错误 `{ code, message }`，HTTP 状态码固定 200 |

## 目录结构

```
SDK/
├── ASP/                # Classic ASP (VBScript)
├── Go/                 # Go
├── Java/               # Java 8+ Maven
├── JavaScript/         # 浏览器 + Node.js
├── PHP/                # PHP
└── Python/             # Python
```

## 其它语言路线图

| 优先级 | 语言/运行时 | 说明 |
|:------:|-------------|------|
| P1 | Python、PHP | 补 `AppInfo` 心跳性能、令牌续期、`Version` 与动态采样 |
| P2 | Classic ASP、Rust、C/C++ | ASP 补性能字段；Rust 可复用 Native Agent 采集能力 |
| P3 | Kotlin、Ruby、Swift、Dart/Flutter、Lua/OpenResty | 按商务/社区需求启动；Kotlin 复用 Java SDK |

## 发布计划（各语言中央仓库）

| 生态 | 仓库 | 建议坐标/名称 | 说明 |
|------|------|---------------|------|
| NuGet | nuget.org | `NewLife.Stardust` | 已发布 |
| npm | npmjs.com | `@newlifex/stardust`（或 `stardust-tracer`） | `newlife` 已被占用，统一用 `newlifex` |
| Maven Central | central.sonatype.com | `com.newlife:stardust-sdk`（备选 `io.github.newlifex`） | 优先验证 `newlife.com`；无域名时用 GitHub 命名空间；需 GPG 签名 |
| Go | pkg.go.dev / GitHub | `github.com/NewLifeX/Stardust/SDK/Go/stardust` | 无中央注册，打子模块标签即可 |
| crates.io | crates.io | `newlifex-stardust`（`stardust` 已被占用） | Rust SDK 启动时注册 |
| PyPI | pypi.org | `newlifex-stardust` | `stardust`/`stardust-sdk` 已被占用 |
| Packagist | packagist.org | `newlifex/stardust` | Composer 包名可用 |

> 发布需要各仓库维护者账号、2FA/Token、GPG 密钥与 CI 凭据；代码完成不等于自动发布，正式发版前需先做 `--dry-run`/Staging 验证。

## 贡献

欢迎为其它语言贡献 SDK。请先阅读 [APM-17 监控接入 API](../Doc/APM-17-监控接入API.md)，并确保登录、心跳性能、埋点三项能力与统一契约一致；新增语言请同步更新本文件的能力矩阵与路线图。

## License

MIT License
