# 星尘监控 JavaScript/Node.js SDK

> 正式实现与最新 API：[SDK/JavaScript/README.md](../../SDK/JavaScript/README.md) | 接入契约：[APM-17 监控接入 API](../APM-17-监控接入API.md)
>
> 本文为简要说明。早期文档中的完整源码已被 `SDK/JavaScript` 正式实现取代，请勿复制旧代码。

适用于 **Node.js 14+** 与**现代浏览器**，零运行时依赖。

## 功能特性

- ✅ 登录认证、令牌自动续期、`tokenProvider` 后端签发令牌
- ✅ 心跳保活，携带应用性能信息（Node：RSS/堆/CPU/GC；浏览器：JS 堆/硬件线程数等）
- ✅ APM 埋点：手动 Span、同名聚合、GZip 上报、动态采样
- ✅ Node.js：`http`/`https`/`fetch` 出入站、`http.createServer`、Express/Koa 中间件、进程退出自动冲刷；自动埋点 `pg`/`mysql2`、`redis`/`ioredis`
- ✅ 浏览器：UMD 单文件、页面加载/SPA 路由/JS 错误/资源错误/fetch/XHR 自动埋点、`sendBeacon` 离页上报
- ✅ 跨服务链路：Web 入口解析 W3C `traceparent`，出站 HTTP 自动注入

## 安装

```bash
npm install @newlifex/stardust
```

浏览器无需构建，直接引入：

```html
<script src="node_modules/@newlifex/stardust/src/browser.js"></script>
<!-- 或 CDN：https://cdn.jsdelivr.net/npm/@newlifex/stardust/src/browser.js -->
```

## 浏览器快速开始

最简方式：一个 `loader.js` + `data-stardust-*` 配置，无需手写初始化代码：

```html
<script src="https://cdn.example.com/stardust/loader.js"
        data-stardust-app="MyWebApp"
        data-stardust-config="/stardust/browser-config"
        defer></script>
```

`loader.js` 会动态加载同目录 `browser.js`，读取后端配置并自动启动埋点；后端可注入 `<meta name="traceparent" content="...">` 把整个页面链路（`page.load`/路由/fetch/XHR/手动 Span）关联到服务端链路。静态页面可用 `data-stardust-token-value` 配置短期 Token，有后端时推荐 `data-stardust-token` 指向 Token 端点。npm 用户可使用 `import { init } from '@newlifex/stardust/browser'` 一行初始化。

需要手动控制时：

```html
<script src="../src/browser.js"></script>
<script>
  const tracer = Stardust.createTracer({
    server: 'http://star.example.com:6600',
    appId: 'MyWebApp',
    version: '1.2.3',
    // 公网应用推荐由后端签发短期令牌，避免在前端暴露 secret
    tokenProvider: async () => {
      const resp = await fetch('/api/stardust-token', { credentials: 'same-origin' });
      const data = await resp.json();
      return { token: data.token, expire: data.expire };
    },
  });

  tracer.start(); // 自动安装页面加载、路由、JS 错误、fetch/XHR 埋点

  const span = tracer.newSpan('submit-order');
  span.setTag('orderId=123');
  span.finish();
</script>
```

> `secret` 模式仅适用于内网、Electron 或受控环境；公网页面不得硬编码 secret。

## Node.js 快速开始

一行接入：

```javascript
const { init } = require('@newlifex/stardust');
const tracer = init({ server: 'http://star.example.com:6600', appId: 'MyApp', secret: 'MySecret' });
```

需要手动控制启动/停止时：

```javascript
const { StardustTracer } = require('@newlifex/stardust');

const tracer = new StardustTracer({
  server: 'http://star.example.com:6600',
  appId: 'MyApp',
  appName: '订单服务',
  secret: 'MySecret',
  version: '1.2.3',
  heartbeatPeriod: 30,
  enableMeter: true,
});

await tracer.start();

const span = tracer.newSpan('业务操作');
span.setTag('userId=123');
try {
  await doSomething();
} catch (err) {
  span.setError(err);
  throw err;
} finally {
  span.finish();
}

await tracer.stop();
```

旧版位置参数 `new StardustTracer(server, appId, secret)` 仍兼容。

## 自动埋点

Node 追踪器构造时默认安装自动埋点，无需额外调用 `instrument()`；`start()` 只负责登录与上报。

| 类型 | 自动采集 |
|------|----------|
| Web 入口 | `http.createServer`/`https.createServer`（Express/Koa `app.listen` 同样生效），解析上游 `traceparent` |
| 出站 HTTP | `http.request/get`、`https.request/get`、全局 `fetch`（Node 18+），注入 `traceparent` |
| 数据库 | `pg`（`Client.query`）、`mysql2`（`Connection.query/execute`） |
| Redis | `redis`（`sendCommand`）、`ioredis`（`sendCommand`） |

关闭自动埋点：

```javascript
const tracer = new StardustTracer({
  server: 'http://star.example.com:6600',
  appId: 'MyApp',
  secret: 'MySecret',
  autoInstrument: false,
});
```

浏览器通过 `autoInstrument: false` 关闭页面/路由/fetch/XHR 自动埋点。自动入口与 Express/Koa 中间件会去重，只产生一个请求 Span。

## API 摘要

| 成员 | 说明 |
|------|------|
| `new StardustTracer(options)` / `(server, appId, secret)` | 创建 Node 追踪器 |
| `Stardust.createTracer(options)` | 创建浏览器追踪器 |
| `Stardust.init(options)` | 创建并启动浏览器追踪器（loader/npm 一行接入） |
| `Stardust.captureException(err, { tag })` | 手动上报异常 |
| `Stardust.flush()` | 立即上报当前聚合数据 |
| `start()` / `stop()` | 启动（登录 + 调度）/ 停止（冲刷 + 清理） |
| `flush()` | 立即上报当前聚合数据 |
| `newSpan(name[, parentId])` | 创建 Span |
| `Span.setTag(tag)` / `setError(err)` / `finish()` | 设置标签、错误、结束 |
| `installBrowserInterceptors(tracer)` | 手动安装浏览器自动埋点 |

常用配置：`period`、`heartbeatPeriod`、`maxSamples`、`maxErrors`、`maxTagLength`、`enableMeter`、`excludes`、`debug`、`autoFlushOnExit`。

## 心跳性能字段

| 环境 | 字段 |
|------|------|
| Node.js | `Id`、`Name`、`Version`、`CommandLine`、`UserName`、`MachineName`、`IP`、`StartTime`、`ProcessorTime`、`CpuUsage`、`WorkingSet`（RSS）、`HeapSize`、`Handles`、`GCCount`、`Time` |
| 浏览器 | `Id=0`、`Name`、`Version`、`CommandLine`（User-Agent）、`MachineName`、`StartTime`、`WorkingSet`（usedJSHeapSize）、`HeapSize`（totalJSHeapSize）、`Threads`（hardwareConcurrency）、`Listens`（origin）、`Time` |

## 令牌与采样

- 登录响应 `Token` + `Expire`（秒）；到期前 5 分钟主动刷新。
- 心跳响应可返回新 `Token`，SDK 自动替换。
- 响应 `code=401` 时重新登录并重试一次。
- 上报响应中的 `Period`、`MaxSamples`、`MaxErrors`、`Timeout`、`MaxTagLength`、`EnableMeter`、`Excludes` 动态生效。
- `Version` 必须随登录/上报发送，服务端才会返回动态参数。

## 运行测试

```bash
cd SDK/JavaScript
npm test
```

覆盖 Node 基础单测、mock HTTP 集成测试（登录/心跳/上报/GZip/401/周期）、浏览器测试（clientId、心跳、beacon、路由、JS 错误）与自动埋点测试（traceparent、出入口 Span、pg/mysql2、Redis）。

## 相关资源

- [SDK/JavaScript/README.md](../../SDK/JavaScript/README.md)
- [示例：浏览器 Demo](../../SDK/JavaScript/examples/browser-demo.html)
- [APM-17 监控接入 API](../APM-17-监控接入API.md)