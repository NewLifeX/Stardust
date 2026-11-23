# APM-18 浏览器埋点方案

> 版本：v1.1 | 日期：2026-11-23
> 对应需求：浏览器 RUM 一行接入、跨服务链路关联、支持"直报星尘"与"应用后端中转"两条链路（[Issue #17](https://github.com/NewLifeX/Stardust/issues/17)）
> 相关文档：[APM-17 监控接入 API](APM-17-监控接入API.md) | [SDK/JavaScript](../SDK/JavaScript/README.md)

---

## 0. 一分钟看懂

两条链路都支持，默认走直报；需要时切换为中转，只改一个 `mode`。

**直报（默认，推荐）**：

```
浏览器页面
  │ ① GET  /stardust/browser-config?appId=MyWeb    拿"怎么报"的配置（模式、地址、采样…）
  │ ② POST /stardust/browser-token  {appId}         拿"门禁卡"（bw_ 签名令牌，10 分钟有效）
  │ ③ POST /Trace/Report                           投递埋点（令牌走 Authorization 头）
  ▼
星尘服务端（监控中心）
```

**中转（应用后端转发）**：

```
浏览器页面
  │ ① POST /stardust/collect                       投递到你自己的后端（可用 Cookie 会话鉴权）
  ▼
应用后端（持有 AppId + Secret）
  │ ② POST /Trace/ReportRelay?Token=<应用令牌>     原样转发，可带 clientIp
  ▼
星尘服务端（强制归属到应用令牌对应的应用）
```

- **直报的数据发给谁**：星尘。埋点不经应用后端转发——后端故障时恰恰是最需要异常数据的时候。
- **中转的数据发给谁**：先到应用后端，再由它转发。适合 CSP 限制、星尘域名被广告拦截、或企业要求出站流量统一走自家后端的场景。
- **前端有什么凭据**：直报只有 10 分钟有效的 `bw_` 令牌，只能投递，不能读数据、不能调用其它接口；App Secret 永不出现在浏览器。中转模式前端不带星尘令牌，可用应用自身会话。
- **应用后端要不要改代码**：直报不要；中转需要加一个转发端点（.NET 用 `BrowserRelayClient` 约 5 行）。

安全边界一句话：前端埋点是**公开可写通道**，数据能当"信号"，不能当"审计证据"（详见 §3.3）。

---

## 1. 结论

浏览器埋点不需要照搬 Sentry/Datadog 的完整 RUM 能力。星尘已经具备页面加载、SPA 路由、JS/资源错误、fetch/XHR 自动埋点、`sendBeacon` 离页冲刷和 W3C `traceparent` 注入，缺的是：

1. **一行接入**：一个 `<script>` 标签自动初始化，不写 `createTracer().start()`。
2. **服务端可控配置**：可选的 `/stardust/browser-config`，集中控制开关、采样与排除项。
3. **公开写入凭据**：短期签名 Token，前端永远不出现 App Secret。
4. **页面级链路**：后端渲染 HTML 时注入 `traceparent`，让 `page.load` 与后端请求处于同一条链路。
5. **两条接入链路**：默认浏览器直报星尘；受 CSP、广告拦截或企业出网策略限制时，可切换为应用后端中转，二者共用同一套埋点与安全模型。

推荐的最小方案：**一个 loader 脚本 + `data-stardust-*` 属性 + 可选配置端点 + 短期签名 Token + 中转模式 + W3C `traceparent`**，其余能力（Web Vitals、Session Replay、OTLP）后置。

### 1.1 直报与中转如何取舍

两种链路都支持，默认直报。"浏览器 → 自己的后端 → 星尘"能隐藏星尘地址、复用登录态，也能绕开浏览器侧限制，代价是把监控绑到了业务后端的可用性上：

| 维度 | 直报星尘（默认） | 经应用后端转发（中转） |
|------|-----------------|------------------------|
| 可用性 | 后端故障不影响采集 | 后端故障时，最需要的后端异常恰好全部丢失 |
| 密钥 | 前端无 secret，由星尘签发短期令牌 | 前端不带星尘令牌；后端持有应用 secret |
| 排障 | 少一跳，链路断点清晰 | 多一跳，需同时看后端转发日志 |
| 成本 | 无额外代码 | 每个业务后端加一个转发端点 |
| 适用 | 绝大多数公网/内网站点 | CSP 限制、星尘域名被广告拦截、企业出网统一代理 |
| 同行 | Sentry/Datadog/New Relic/Elastic/Faro 默认直报 | Sentry `tunnel` 等作为可选透传，用于绕开拦截 |

**同域反代不等于"经应用后端"**：nginx 把 `/stardust/*` 转发给星尘的独立 upstream，不含业务代码；应用后端进程挂了，反代照常工作。它解决的是 CSP `connect-src` 与广告拦截，不会把监控绑到业务可用性上。反代必须指向星尘独立 upstream，不能落在应用后端的路由上。

> 架构决策：前端埋点默认浏览器直报星尘；支持应用后端中转作为可控降级，中转时由服务端强制归属 `AppId` 并保留真实客户端 IP。见《架构设计》关键决策表。

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
        data-stardust-token-value="bw_xxxxxxxxxxxxxxxxxxxxxxxx"
        data-stardust-release="1.2.3"
        defer></script>
```

也可以用 `data-stardust-token="/stardust/browser-token"` 指向 Token 端点；直接配置 Token 时可用 `data-stardust-expire="300"` 指定秒数。`configUrl`/`tokenUrl` 为相对路径时走同域反代；显式指定 `data-stardust-server` 时走跨域直连，两种拓扑见 §3.8。`loader.js` 自动加载同目录 `browser.js`，读取配置并启动追踪器。npm 用户使用：

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
- 星尘服务端已实现该端点（`GET /stardust/browser-config`，`BrowserController`）：有 `appId` 时校验应用存在且启用，未知应用返回错误码；响应带 `Cache-Control: public, max-age=300`，不含任何密钥，`server` 取当前请求的协议与主机，便于同源代理场景直接回连。
- `tokenUrl` 允许相对地址；SDK 按**配置端点地址**解析，因此直连跨域时指向星尘、同域反代时指向页面同源，两种拓扑无需改配置。
- 当前 SDK 消费 `appId`/`server`/`mode`/`reportUrl`/`tokenUrl`/`version`/`environment`/`debug`/`sampleRate`/`ignoreUrls`/`disabled`。

### 3.3 短期签名令牌（证书令牌）

**签发方是星尘服务端**（`BrowserController` + `BrowserTokenService`，均在 `Stardust.Server` 内），不是客户后端。浏览器直接按下面的契约取令牌：

```http
POST /stardust/browser-token
Origin: https://www.example.com
Content-Type: application/json

{ "appId": "MyWeb" }
```

```json
{ "appId": "MyWeb", "token": "bw_xxxxxxxxxxxxxxxxxxxxxxxx", "expire": 600, "server": "https://star.example.com:6600" }
```

令牌是**服务端签名的自包含凭据**（“证书令牌”），格式：

```
bw_{Base64Url(appId)}.{Base64Url(origin)}.{过期Unix秒}.{HMAC-SHA256 签名}
```

- 只被 `Trace/Report` 接受，不能当作应用令牌调用配置、部署等接口。
- 默认 10 分钟过期；前端缓存于 `sessionStorage`，关标签页即失效。
- 绑定签发时的页面来源；上报来源不一致时令牌视为无效。
- 校验不依赖共享缓存，多实例无需 Redis；`BrowserTokenSecret` 需各实例一致。
- 兼容旧版随机令牌：不是签名结构的 `bw_` 令牌继续按缓存校验，自然过期后退出。

上报时优先使用 `Authorization: Bearer bw_xxx`，避免令牌进入 URL 访问日志；`sendBeacon` 无法加头，离页冲刷仍用 `?Token=`。

安全防线：

| 层 | 机制 | 挡住什么 |
|----|------|---------|
| 1 | 签名令牌不可伪造、不可延展，只被 `Trace/Report` 接受 | 被当作应用令牌调用其它受保护接口 |
| 2 | 默认 10 分钟过期，签名内含过期时间 | 长期窃用 |
| 3 | 申请必须带 `Origin`（缺失回退 `Referer`）并命中 `CorsOrigins`；应用须存在且启用 | 其它网站盗用应用名申请令牌 |
| 4 | 令牌绑定申请来源，上报来源不一致即失效 | 令牌被抄到其它站点使用 |
| 5 | 签发限流（应用 + IP，默认 300 次/有效期）与上报限流（应用 + IP，默认 600 次/分钟） | 令牌接口与公开写通道被刷 |
| 6 | 单次上报最多 100 个埋点项；应用还需通过 `WhiteIP` 校验 | 单请求塞入超大批量数据 |
| 7 | 令牌主体必须与上报 `AppId` 一致；应用禁用后申请与上报立即失效 | 拿 A 应用令牌向 B 应用灌数据 |

**残余风险（必须承认，与同行一致）**：

- 令牌是公开可写凭据。访客可从自己浏览器里抄走令牌，在 10 分钟内伪造该应用的埋点。Sentry 的 DSN、Datadog 的 clientToken 同样如此，因为写入通道必须匿名。
- `Origin` 与来源绑定挡不住 curl 等非浏览器客户端（可任意伪造），它只防"别的正常站点误用你的应用名"。
- 因此按限流、采样、数据保留期来控制影响面，而不是指望"防伪"；**前端埋点数据不能作为审计证据**。

服务端实现要点（`BrowserTokenService` + `BrowserController`）：

- 签名密钥 `StarServerSetting.BrowserTokenSecret`，为空时自动生成并持久化；多实例部署需配置同一密钥，轮换后存量令牌最多 10 分钟内自然失效。
- 来源校验取 `Origin`，缺失时回退 `Referer`；未配置 `CorsOrigins` 时放行任意来源（与 CORS 策略一致），配置后仅白名单来源可申请。**未配置是兼容期默认值，生产环境必须配置。**
- 签发限流与上报限流使用 `ICacheProvider`；多实例未共享 Redis 时按节点计数，精度下降但不会失效。
- `Trace/Report` 在令牌不是 JWT 时尝试按浏览器令牌校验，令牌主体必须与上报应用一致，防止冒名灌数据。
- CORS 策略显式允许 `GET`/`POST` 与任意请求头：跨域 JSON POST 与 `Authorization` 头都会触发预检。
- 令牌与埋点报文必须走 HTTPS。直连或反代都应启用 TLS，否则令牌与页面数据会在链路上明文暴露。

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
| Web Vitals | 离页时汇总 LCP/CLS/INP/FCP/TTFB 为 `web.vital.*`，标签含 `value` 与 `rating` |
| 离页冲刷 | `sendBeacon`，回退 `fetch keepalive` |

`traceparent` 默认只注入同源请求；跨域目标需通过 `tracePropagationTargets` 显式配置，避免自定义头触发 CORS 预检并阻断业务请求。

只提供 `disabled`/`ignoreUrls`/`excludes` 等关闭开关，不要求用户逐个配置集成。

- `disabled` 按类别关闭自动埋点：`page`/`route`/`error`/`resource`/`fetch`/`xhr`/`vitals`。
- `ignoreUrls` 按 URL 子串或正则跳过 fetch/XHR 与资源错误，适合健康检查、指标等噪声地址。
- `sampleRate` 按页面采样：未命中采样时只丢弃正常埋点，异常埋点始终上报。

Web Vitals 的 INP 取 `event` 条目最大处理耗时近似，LCP/CLS 依赖浏览器支持；不支持的环境只上报 TTFB/FCP。评级阈值：LCP 2500/4000ms、CLS 0.1/0.25、INP 200/500ms、FCP 1800/3000ms、TTFB 800/1800ms。

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
- Web Vitals 只采集性能数值，不含 DOM 文本、用户输入或资源内容。
- URL 默认去掉查询串与 fragment；提供 `ignoreUrls`/`beforeSend`。
- 页面采样与链路采样分离；`sampleRate` 默认 1，小于 1 时按页面决定是否采集正常埋点，异常始终保留；未采样链路发送 `traceparent` 的 `00` 标志。
- 默认不采集用户身份；`setUser` 需显式调用。
- 支持 `optIn`/`optOut` 与 Do Not Track（按项目合规要求）。

### 3.8 三种部署拓扑

| 拓扑 | 页面配置 | 需要什么 | 适用 |
|------|---------|---------|------|
| **同域反代**（直报，生产推荐） | `data-stardust-config="/stardust/browser-config"` | 网关把 `/stardust/*` 与 `/Trace/Report` 指向星尘独立 upstream | 规避 CORS、CSP `connect-src` 与广告拦截 |
| **直连跨域**（直报） | `data-stardust-server="https://star.example.com:6600"` | 星尘配置 `CorsOrigins` 白名单；页面 CSP `connect-src` 放行星尘域名 | 静态站点、无自有网关 |
| **应用后端中转** | `data-stardust-mode="relay" data-stardust-report="/stardust/collect"` | 应用后端加一个转发端点（见 §3.9） | 星尘域名不可达、企业要求出站统一走自家后端 |

前两种拓扑由星尘签发令牌并接收数据，应用后端不参与。反代只做透传，不解析、不缓存、不缓冲埋点报文。

同域反代的 nginx 片段：

```nginx
location /stardust/ {
    proxy_pass https://star.example.com:6600;   # 星尘独立 upstream，与应用后端无关
    proxy_set_header Host $host;                # 保持同源：配置下发的 server 与页面一致
}
location = /Trace/Report {
    proxy_pass https://star.example.com:6600;
    proxy_set_header Host $host;
}
```

> 广告拦截器可能拦截监控域名。若直连被拦截，优先补同域反代，再考虑应用后端中转（中转会多一跳后端依赖）。

### 3.9 应用后端中转

页面只改两个属性：

```html
<script src="/stardust/loader.js"
        data-stardust-app="MyWebApp"
        data-stardust-mode="relay"
        data-stardust-report="/stardust/collect"
        defer></script>
```

浏览器行为：

- 不调用 `/App/Login`、不申请 `bw_` 令牌、不发送心跳。
- 上报请求默认 `credentials: 'same-origin'`，可直接复用应用自身 Cookie 会话鉴权。
- 报文结构与直报一致（`TraceModel`），`AppId` 会被服务端忽略并强制归属到应用令牌主体。
- 上报地址本身会被自动埋点忽略，避免自采集递归。

应用后端最小实现（.NET，`Stardust.Monitors.BrowserRelayClient`）：

```csharp
var relay = new BrowserRelayClient(); // 读取 StarSetting.Current 的 Server/AppKey/Secret

app.MapPost("/stardust/collect", async (HttpContext ctx) =>
{
    var ip = ctx.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
    await relay.ForwardAsync(ctx.Request.Body, ip, ctx.RequestAborted);
    return Results.Ok(new { code = 0 });
});
```

服务端契约：

```http
POST /Trace/ReportRelay?Token=<应用JWT>&clientIp=203.0.113.7
Content-Type: application/json        # 或 application/x-gzip

<浏览器原始埋点报文>
```

- 只接受应用 JWT（`App/Login` 或 `OAuth/Token` 换取）；浏览器 `bw_` 令牌与匿名请求一律 401。
- 服务端解码 JWT，取 `Subject` 作为应用名，**强制覆盖** `model.AppId`；中转方无法向其它应用写数据。
- `clientIp` 参与 `TraceData.CreateIP`、`AppOnline` 与 `AppMeter`；非法 IP 回退请求来源 IP。应用只能为自己的应用指定 IP。
- 报文上限默认 1 MB（`StarServerSetting.RelayMaxBodySize`），支持 GZip。
- 中转请求共用应用令牌，不按令牌复用在线会话，避免把应用自身的在线记录覆盖成浏览器。

中转的代价：应用后端故障时前端异常全部丢失；因此默认仍推荐直报，中转作为可控降级。

---

## 4. 当前落地状态

已完成：

- npm 包名统一为 `@newlifex/stardust`（浏览器子路径 `@newlifex/stardust/browser`）。
- 新增 `src/loader.js`：支持 `data-stardust-app/config/server/token/token-value/expire/trace-targets/release/env/sample/debug`，兼容旧的 `data-app-id` 等属性。
- 新增 `Stardust.init()`、`Stardust.captureException()`、`Stardust.flush()`。
- 支持 `initialTraceparent`：`page.load` 可复用后端注入的 TraceId/ParentId。
- 页面级链路连续性：`page.load`、`page.view`、fetch/XHR、手动 Span 共用同一页面 TraceId；有初始 `traceparent` 时整体挂到后端链路。
- 保留页面/路由/JS 错误/资源错误/fetch/XHR/离页冲刷。
- 服务端 `GET /stardust/browser-config` 与 `POST /stardust/browser-token` 已实现（`BrowserController`）。`loader.js` 的令牌申请已与端点契约对齐（`POST` + JSON `{appId}`）。
- 直报令牌升级为无状态 HMAC 签名令牌（应用 + 来源 + 过期）；上报优先走 `Authorization` 头；新增来源校验、上报限流与单次条数上限；旧随机令牌继续兼容。
- 新增 `POST /Trace/ReportRelay` 与 `Stardust.Monitors.BrowserRelayClient`；浏览器 SDK 支持 `mode=relay` + `reportUrl`，loader 支持 `data-stardust-mode` / `data-stardust-report`。
- 配置端点的 `sampleRate`/`ignoreUrls`/`disabled` 已被 SDK 消费：页面级采样保留异常，按 URL 与类别关闭自动埋点。
- 新增 Web Vitals 采集：LCP/CLS/INP/FCP/TTFB 在离页时汇总为 `web.vital.*`，默认开启，可用 `enableWebVitals:false` 或 `disabled:['vitals']` 关闭。
- 测试：`BrowserEndpointsTests`（配置下发、来源校验、令牌签发与上报、Authorization/查询串两种上报、来源不一致拒绝、冒名拒绝、令牌不得用于其它接口）+ `BrowserTokenServiceTests`（无状态签名、过期、篡改、来源绑定、旧令牌兼容、签发与上报限流）+ `TraceRelayTests` 5 例（强制归属、客户端IP、浏览器令牌与匿名拒绝、GZip、条数上限）+ 跨域预检 `CorsTests`；`BrowserRelayClientTests` 3 例；`npm test`（浏览器 relay 模式与 loader 用例）。

已知边界：

- 跨域直连要求星尘配置 `CorsOrigins` 白名单、CORS 显式允许 `POST` 与 `Authorization` 头（已实现），且页面 CSP `connect-src` 放行星尘域名。
- `bw_` 令牌可被访客复制，在有效期内伪造该应用埋点。这是公开可写凭据的固有边界，见 §3.3 残余风险。
- 中转模式前端不带星尘令牌，安全性取决于应用自身的会话鉴权与防刷；应用后端应自行限流并校验报文大小。

待后续：

- Web Vitals、Session Replay、OTLP 导出、框架专用包（React/Vue）。
- CDN 正式域名与版本化产物（`stardust.min.js` / `stardust-loader.min.js`）。

---

## 5. 分阶段

| 阶段 | 内容 |
|------|------|
| P1（MVP） | loader + `data-stardust-*`、`Stardust.init`、配置端点回退、页面/路由/错误/fetch/XHR、`traceparent`、开关与采样、令牌契约对齐与跨域预检 |
| P2 | Web Vitals（已完成）、懒加载 loader、Source Map |
| P3 | Session Replay、OTLP/OpenTelemetry 桥接、跨域链路、React/Vue 封装 |

> 原则：一行接入、默认自动、前端无 secret、W3C `traceparent` 唯一跨端契约。
