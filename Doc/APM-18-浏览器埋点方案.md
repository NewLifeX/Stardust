# APM-18 浏览器埋点方案

> 版本：v1.0 | 日期：2026-10-20
> 对应需求：浏览器 RUM 一行接入、跨服务链路关联
> 相关文档：[APM-17 监控接入 API](APM-17-监控接入API.md) | [SDK/JavaScript](../SDK/JavaScript/README.md)

---

## 1. 结论

浏览器埋点不需要照搬 Sentry/Datadog 的完整 RUM 能力。星尘已经具备页面加载、SPA 路由、JS/资源错误、fetch/XHR 自动埋点、`sendBeacon` 离页冲刷和 W3C `traceparent` 注入，缺的是：

1. **一行接入**：一个 `<script>` 标签自动初始化，不写 `createTracer().start()`。
2. **后端可控配置**：可选的 `/stardust/browser-config`，集中控制开关、采样与排除项。
3. **公开写入凭据**：短期匿名 Token，前端永远不出现 App Secret。
4. **页面级链路**：后端渲染 HTML 时注入 `traceparent`，让 `page.load` 与后端请求处于同一条链路。

推荐的最小方案：**一个 loader 脚本 + `data-stardust-*` 属性 + 可选配置端点 + 短期 Token + W3C `traceparent`**，其余能力（Web Vitals、Session Replay、OTLP）后置。

---

## 2. 同行怎么做

| 产品 | 接入方式 | 配置来源 | 前端凭据 | 默认自动埋点 | 浏览器 → 后端透传 | 加载策略 |
|------|----------|----------|----------|--------------|-------------------|----------|
| **Sentry** | Loader `<script>`、CDN bundle、npm `Sentry.init` | DSN + init；Loader 配置在 Sentry UI | DSN 公钥 | 错误、面包屑、会话；页面/路由/资源/Web Vitals 需 tracing 集成 | `sentry-trace` + `baggage`，可选 W3C `traceparent` | Loader 小、首错或空闲时懒加载完整 SDK；npm 可 tree-shaking |
| **Datadog RUM** | CDN `datadog-rum.js` + `DD_RUM.init`；npm | 内联 clientToken/applicationId/site | Client Token + Application ID | 会话、视图、资源、长任务、错误、用户操作、Core Web Vitals | Datadog 头 + W3C `traceparent`/`tracestate` | 单个 CDN bundle；推荐 async；npm bundle |
| **New Relic Browser** | 复制 `NREUM` snippet；npm `BrowserAgent`；APM 自动注入 | 内联 `NREUM.info`/`loader_config` | Browser License Key + App ID | 页面、AJAX、JS 错误、会话追踪、SPA、资源、Web Vitals | `newrelic` + `traceparent`/`tracestate`（同源默认） | CDN loader / npm loader / APM 注入 |
| **Grafana Faro** | `initializeFaro`；CDN IIFE；npm | 内联 collector URL、apiKey、app | `apiKey`（公开写入键） | 错误、Web Vitals、会话、视图、用户操作、性能、CSP、Console；fetch/XHR 需 tracing 包 | OTel W3C `traceparent`（TracingInstrumentation） | 核心包 + 独立 tracing 包；npm 可 tree-shaking |
| **OpenTelemetry JS** | 手动组装 `TracerProvider`、Propagator、Exporter、Instrumentations | 内联 endpoint/headers | OTLP endpoint + 自定义头 | 默认无；`getWebAutoInstrumentations()` 提供 document-load/fetch/XHR/user-interaction | W3C `traceparent` + baggage | 多包、需自行打包，接入成本最高 |
| **Elastic RUM** | UMD `<script>` + `elasticApm.init`；npm | 内联 serviceName/serverUrl；可选 centralConfig | 无前端 Token，靠 RUM intake + CORS | 页面、资源、XHR/fetch、SPA、点击、JS 错误、Core Web Vitals | W3C `traceparent`（同源默认） | 单 UMD（约 16 KiB gzip）或 npm |
| **Cloudflare / GA4** | snippet | 内联 site token / measurement ID | 公开站点标识 | 页面浏览、性能、Web Vitals | 无（分析工具，非链路追踪） | 单 snippet |

**共性规律**：

- **一行接入**是绝对主流：一个 script 标签或一个 `init` 调用。
- **公开凭据**：DSN、Client Token、Browser License Key、apiKey 都允许出现在前端，但只具备写入权限，并配合域名白名单/限流。
- **默认自动埋点**：页面、路由、错误、fetch/XHR 默认开启；Replay、Web Vitals、日志等可选项后置。
- **W3C `traceparent`** 正在成为跨端链路的标准，Sentry/Datadog/New Relic/Elastic 均支持或默认使用。
- **加载策略**：Sentry 用小 loader 懒加载完整 SDK；Datadog/Elastic 用单 bundle；OTel 由用户自行打包。

参考：
- Sentry Loader：https://docs.sentry.io/platforms/javascript/install/loader/
- Sentry distributed tracing：https://docs.sentry.io/platforms/javascript/tracing/distributed-tracing/
- Datadog RUM setup：https://docs.datadoghq.com/real_user_monitoring/application_monitoring/browser/setup/client/
- Datadog RUM & Traces：https://docs.datadoghq.com/tracing/other_telemetry/rum/
- New Relic browser install：https://docs.newrelic.com/docs/browser/browser-monitoring/installation/install-browser-monitoring-agent/
- Grafana Faro quick start：https://github.com/grafana/faro-web-sdk/blob/main/docs/sources/tutorials/quick-start-browser.md
- OpenTelemetry JS browser：https://opentelemetry.io/docs/languages/js/getting-started/browser/
- Elastic RUM install：https://www.elastic.co/docs/reference/apm/agents/rum-js/install-agent

---

## 3. 推荐方案

### 3.1 一行接入

静态站点或后端模板直接输出：

```html
<script src="https://cdn.example.com/stardust/loader.js"
        data-stardust-app="MyWebApp"
        data-stardust-config="/stardust/browser-config"
        defer></script>
```

不需要配置端点时：

```html
<script src="https://cdn.example.com/stardust/loader.js"
        data-stardust-app="MyWebApp"
        data-stardust-server="https://star.example.com:6600"
        data-stardust-token-value="eyJ..."
        data-stardust-release="1.2.3"
        defer></script>
```

也可以用 `data-stardust-token="/stardust/browser-token"` 指向 Token 端点；直接配置 Token 时可用 `data-stardust-expire="300"` 指定秒数。`loader.js` 自动加载同目录 `browser.js`，读取配置并启动追踪器。npm 用户使用：

```js
import { init } from '@newlifex/stardust/browser';

init({
  appId: 'MyWebApp',
  configUrl: '/stardust/browser-config',
  tokenUrl: '/stardust/browser-token',
});
```

### 3.2 配置端点（可选）

`GET /stardust/browser-config` 返回同源、可缓存、无 secret 的配置：

```json
{
  "appId": "MyWebApp",
  "server": "https://star.example.com:6600",
  "release": "1.2.3",
  "environment": "production",
  "tokenUrl": "/stardust/browser-token",
  "sampleRate": 1.0,
  "tracePropagationTargets": ["https://api.example.com"],
  "ignoreUrls": ["/health", "/metrics"],
  "disabled": []
}
```

- 配置端点失败时回退到 `data-stardust-*` 与默认值，不阻塞页面加载。
- `disabled` 是关闭开关列表（`page`/`route`/`error`/`resource`/`fetch`/`xhr`），不是必填的集成列表。
- 建议在 `sessionStorage` 缓存 5 分钟。

### 3.3 短期匿名 Token

`POST /stardust/browser-token` 校验 `appId`、Origin/Referer、域名白名单与限流，签发 5–10 分钟、仅 `trace:write` 的匿名 Token。前端只持有短期 Token，永不出现 App Secret；`401` 时刷新一次并重试。`sendBeacon` 无法设置自定义头，推荐 `fetch(..., { keepalive: true })` 或专用 `/Trace/ReportBeacon`。

### 3.4 链路关联

后端渲染 HTML 时注入：

```html
<meta name="traceparent" content="00-<32hex-trace-id>-<16hex-span-id>-01">
```

浏览器解析后，用该 TraceId/ParentId 创建 `page.load` Span；fetch/XHR 继续注入当前 Span 的 `traceparent`，后端 `TracerMiddleware` 用 `NewServerSpan` 接续链路。这样可得到 **浏览器 → 后端 → 数据库/Redis** 的完整链路。

规则：

- 不覆盖业务已显式设置的 `traceparent`。
- 默认只对同源和显式配置的第一方 API 注入，避免把链路头泄露给第三方。
- 未采样时发送 `00` 标志，交给后端按父级采样决定。

### 3.5 默认自动埋点与开关

保持现有默认，全部由 `autoInstrument: true` 开启：

| 能力 | 行为 |
|------|------|
| 页面加载 | `page.load`，Navigation Timing 作为 Tag |
| SPA 路由 | `page.view <path>` |
| JS 错误 | `js.error`，含未处理 Promise |
| 资源错误 | `resource.error` |
| fetch/XHR | 出站 Span、状态码/异常、`traceparent` 注入 |
| 离页冲刷 | `sendBeacon`，回退 `fetch keepalive` |

`traceparent` 默认只注入同源请求；跨域目标需通过 `tracePropagationTargets` 显式配置，避免自定义头触发 CORS 预检并阻断业务请求。

只提供 `disabled`/`ignoreUrls`/`excludes` 等关闭开关，不要求用户逐个配置集成。

### 3.6 最小公开 API

```js
Stardust.init(options?)          // 创建并启动；loader 自动调用
Stardust.captureException(err)    // 手动上报异常
Stardust.startSpan(name)          // 手动 Span
Stardust.flush()                  // 立即上报
Stardust.version
Stardust.tracer                   // 高级用法
```

保留现有 `Stardust.createTracer(options)` + `tracer.start()` 兼容旧接入。

### 3.7 隐私与采样默认值

- 默认不采集 Cookie、Authorization、请求/响应体、表单值和 DOM 文本。
- URL 默认去掉查询串与 fragment；提供 `ignoreUrls`/`beforeSend`。
- 页面采样与链路采样分离；未采样链路发送 `traceparent` 的 `00` 标志。
- 默认不采集用户身份；`setUser` 需显式调用。
- 支持 `optIn`/`optOut` 与 Do Not Track（按项目合规要求）。

---

## 4. 当前落地状态

已完成：

- npm 包名统一为 `@newlifex/stardust`（浏览器子路径 `@newlifex/stardust/browser`）。
- 新增 `src/loader.js`：支持 `data-stardust-app/config/server/token/token-value/expire/trace-targets/release/env/sample/debug`，兼容旧的 `data-app-id` 等属性。
- 新增 `Stardust.init()`、`Stardust.captureException()`、`Stardust.flush()`。
- 支持 `initialTraceparent`：`page.load` 可复用后端注入的 TraceId/ParentId。
- 页面级链路连续性：`page.load`、`page.view`、fetch/XHR、手动 Span 共用同一页面 TraceId；有初始 `traceparent` 时整体挂到后端链路。
- 保留页面/路由/JS 错误/资源错误/fetch/XHR/离页冲刷。

待后续：

- `GET /stardust/browser-config` 与 `POST /stardust/browser-token` 服务端端点。
- Web Vitals、Session Replay、OTLP 导出、框架专用包（React/Vue）。
- CDN 正式域名与版本化产物（`stardust.min.js` / `stardust-loader.min.js`）。

---

## 5. 分阶段

| 阶段 | 内容 |
|------|------|
| P1（MVP） | loader + `data-stardust-*`、`Stardust.init`、配置端点回退、页面/路由/错误/fetch/XHR、`traceparent`、开关与采样 |
| P2 | 配置/Token 服务端端点、Web Vitals、懒加载 loader、Source Map |
| P3 | Session Replay、OTLP/OpenTelemetry 桥接、跨域链路、React/Vue 封装 |

> 原则：一行接入、默认自动、前端无 secret、W3C `traceparent` 唯一跨端契约。
