# Stardust JavaScript SDK

星尘监控（Stardust APM）JavaScript/Node.js SDK，零运行时依赖，支持 Node.js 14+ 与现代浏览器。

- **Node.js**：应用性能心跳、埋点聚合、GZip 上报、令牌自动续期、进程退出自动刷新。
- **浏览器**：单文件 UMD、稳定 ClientId、页面加载/路由/AJAX/JS 错误自动埋点、`sendBeacon` 离页上报。

所有接口均为 HTTP POST，成功返回 `{ code: 0, data: {...} }`，失败返回 `{ code, message }`（HTTP 200）。令牌通过 `?Token=...` 查询参数传递。

---

## 安装

```bash
# 发布到 npm 后
npm install stardust-tracer

# 或直接复制源码
cp -r SDK/JavaScript/src /your-project/stardust-tracer
```

浏览器无需构建步骤，直接引入 `src/browser.js`：

```html
<script src="/path/to/stardust-tracer/src/browser.js"></script>
```

---

## 浏览器一键接入

```html
<script src="../src/browser.js"></script>
<script>
    const tracer = Stardust.createTracer({
        server: 'http://star.example.com:6600',
        appId: 'MyWebApp',
        appName: '我的网站',
        version: '1.1.0',
        // 公网应用必须使用 tokenProvider，不要在前端写 secret
        tokenProvider: async () => {
            const resp = await fetch('/api/stardust-token', { credentials: 'same-origin' });
            const data = await resp.json();
            return { token: data.token, expire: data.expire };
        },
        period: 60,
        autoInstrument: true,
        debug: false,
    });

    tracer.start();

    // 手动埋点
    const span = tracer.newSpan('submit-order');
    span.setTag('orderId=123');
    try {
        await submitOrder();
    } catch (err) {
        span.setError(err);
        throw err;
    } finally {
        span.finish();
    }
</script>
```

页面卸载或进入后台时，SDK 会调用：

```js
navigator.sendBeacon(`${server}/Trace/Report?Token=...`, new Blob([json], { type: 'application/json' }));
```

`sendBeacon` 不可用时自动退回 `fetch(..., { keepalive: true })`。

### ClientId 与令牌缓存

| 数据 | 存储位置 | 说明 |
|------|----------|------|
| ClientId | `localStorage["stardust.clientId.<appId>"]` | `browser@<32 位十六进制>`，同一应用稳定复用 |
| Token | `sessionStorage["stardust.token.<appId>"]` | JSON `{ token, expire }`，`expire` 为毫秒时间戳 |

存储不可用（隐私模式、SecurityError）时回退到内存，不影响页面运行。

### tokenProvider 推荐模式（公网必备）

`secret` 会出现在前端源码中，**任何公网可访问的页面都不得使用 secret 模式**。推荐由后端用 AppId/Secret 登录后，把短期令牌下发给前端：

```js
const tracer = Stardust.createTracer({
    server: 'http://star.example.com:6600',
    appId: 'MyWebApp',
    tokenProvider: async () => {
        const resp = await fetch('/api/stardust-token', { credentials: 'same-origin' });
        const data = await resp.json();
        return { token: data.token, expire: data.expire }; // expire 支持相对秒/绝对秒/绝对毫秒
    },
});
```

- 返回字符串或 `{ token, expire }` 均可，支持 `async`。
- 令牌将在 5 分钟内过期时主动刷新；响应 `code: 401` 或 HTTP 401 时刷新并重试一次。
- `secret` 模式仅建议用于内网、Electron 或受控环境。

---

## Node.js 快速开始

```js
const { StardustTracer } = require('stardust-tracer');

const tracer = new StardustTracer({
    server: 'http://star.example.com:6600',
    appId: 'MyApp',
    appName: '订单服务',
    secret: 'MySecret',
    version: '1.1.0',
    period: 60,
    heartbeatPeriod: 30,
    enableMeter: true,
    autoFlushOnExit: true,
    debug: false,
});

async function main() {
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

    await tracer.stop(); // 停止定时器并上报剩余数据
}

main();
```

旧版位置参数仍然可用：

```js
const tracer = new StardustTracer('http://star.example.com:6600', 'MyApp', 'MySecret');
```

Node.js 心跳会自动采集：

| 字段 | 说明 |
|------|------|
| `Id` | 进程号 `process.pid` |
| `Name` / `Version` | 应用名与版本 |
| `CommandLine` | `process.argv.join(' ')` |
| `UserName` / `MachineName` | 系统用户与主机名 |
| `IP` | 本机非回环 IPv4 |
| `StartTime` | 进程启动时间（ISO） |
| `ProcessorTime` | 累计 CPU 时间（毫秒） |
| `CpuUsage` | 采样周期 CPU 时间 / 实际时间 |
| `WorkingSet` / `HeapSize` | RSS / heapTotal（字节） |
| `Threads` | Node 固定为 0 |
| `Handles` | 活跃句柄数（受保护采集） |
| `Connections` | 固定为 0 |
| `GCCount` | `perf_hooks` GC 事件增量 |
| `Listens` | 空字符串 |
| `Time` | 当前 Unix 毫秒 |

浏览器心跳 `Info`：

| 字段 | 取值 |
|------|------|
| `Id` | 固定 0 |
| `CommandLine` | `navigator.userAgent` |
| `MachineName` | `navigator.userAgentData.platform` / `navigator.platform` |
| `StartTime` | `new Date(performance.timeOrigin).toISOString()` |
| `ProcessorTime` / `CpuUsage` | 固定 0 |
| `WorkingSet` | `performance.memory.usedJSHeapSize` |
| `HeapSize` | `performance.memory.totalJSHeapSize` |
| `Threads` | `navigator.hardwareConcurrency` |
| `Handles` / `Connections` / `GCCount` | 固定 0 |
| `Listens` | `location.origin` |
| `Time` | 当前 Unix 毫秒 |

---

## Express / Koa 中间件

```js
const express = require('express');
const { StardustTracer } = require('stardust-tracer');
const createExpressMiddleware = require('stardust-tracer/middleware/express');

const tracer = new StardustTracer({ server, appId, secret, version: '1.1.0' });
tracer.start();

const app = express();
app.use(createExpressMiddleware(tracer));
```

```js
const Koa = require('koa');
const createKoaMiddleware = require('stardust-tracer/middleware/koa');

app.use(createKoaMiddleware(tracer));
```

- 优先使用路由模板路径（`req.route.path` / `ctx.routePath` / `_matchedRoute`），回退到实际 path。
- 状态码 `>= 400` 自动标记为错误。
- 自动忽略 `/App/`、`/Trace/` 监控接口。
- 响应结束事件只结算一次，不会重复上报。

---

## 配置选项

| 选项 | 默认值 | Node | 浏览器 | 说明 |
|------|--------|:----:|:------:|------|
| `server` | — | ✓ | ✓ | 星尘服务器地址 |
| `appId` | — | ✓ | ✓ | 应用 ID |
| `appName` | 同 `appId` | ✓ | ✓ | 应用显示名 |
| `secret` | `''` | ✓ | ✓ | 应用密钥；浏览器公网禁用 |
| `version` | `1.1.0` | ✓ | ✓ | 应用版本，登录/上报必须携带 |
| `clientId` | `localIP@pid` / `browser@...` | ✓ | ✓ | 客户端标识 |
| `tokenProvider` | — | ✓ | ✓ | 自定义令牌提供者 |
| `period` | `60` | ✓ | ✓ | 上报周期（秒），服务端可动态调整 |
| `heartbeatPeriod` | `30` | ✓ | ✓ | 心跳周期（秒），浏览器隐藏时暂停 |
| `maxSamples` | `1` | ✓ | ✓ | 每个埋点名称最大正常样本数 |
| `maxErrors` | `10` | ✓ | ✓ | 每个埋点名称最大错误样本数 |
| `maxTagLength` | `1024` | ✓ | ✓ | 标签最大长度 |
| `requestTagLength` | `1024` | ✓ | ✓ | 请求标签最大长度 |
| `timeout` | `5000` | ✓ | ✓ | 服务端判定的超时阈值（毫秒） |
| `httpTimeout` | `10000` | ✓ | — | Node HTTP 请求超时（毫秒） |
| `enableMeter` | `true` | ✓ | ✓ | 上报时附带 `Info` 性能信息 |
| `autoInstrument` | `true` | — | ✓ | 自动安装 fetch/XHR/页面/路由/错误埋点 |
| `autoFlushOnExit` | `true` | ✓ | — | 进程退出时自动刷新 |
| `debug` | `false` | ✓ | ✓ | 输出调试日志（默认静默） |
| `excludes` | `[]` | ✓ | ✓ | 排除的埋点名称（子串匹配） |

---

## 浏览器自动埋点

`autoInstrument: true`（默认）或手动 `installBrowserInterceptors(tracer)`：

| 类型 | 埋点名称 | 说明 |
|------|----------|------|
| fetch | `GET /api/users` | 同源取 pathname；跨域取 `host+pathname`；忽略查询串 |
| XMLHttpRequest | `POST /api/order` | 同上；状态码 `>= 400` 或网络失败记错误 |
| 页面加载 | `page.load` | Tag：`dns=..;tcp=..;ttfb=..;dom=..;load=..` |
| SPA 路由 | `page.view /users` | 包装 `pushState` / `replaceState`，监听 `popstate` / `hashchange`，连续重复路径去重 |
| JS 错误 | `js.error` | `window.error` 与 `unhandledrejection` |
| 资源错误 | `resource.error` | 捕获阶段监听图片/脚本/样式加载失败 |

自动忽略包含 `/Trace/`、`/App/Login`、`/App/Ping` 的请求，避免自监控递归。

手动安装：

```js
const { installBrowserInterceptors } = require('stardust-tracer/browser');
installBrowserInterceptors(tracer);
```

---

## 手动埋点 API

```js
const span = tracer.newSpan('operation-name', parentId);
span.setTag('key=value');
span.setError(err);
span.finish(); // 幂等，重复调用只结算一次
```

- `newSpan(name, parentId?)` 返回 Span（`id`、`traceId`、`startTime` 等）。
- `setTag` / `setError` 支持链式调用。
- 数据自动按名称聚合：调用次数、错误数、总耗时、最大/最小耗时、样本。

---

## 服务端响应参数

登录、心跳与上报响应中的以下字段会被自动应用：

| 字段 | 说明 |
|------|------|
| `Period` | 上报周期（秒）；变化时立即重建定时器 |
| `MaxSamples` | 最大正常样本数 |
| `MaxErrors` | 最大错误样本数 |
| `Timeout` | 超时判定阈值（毫秒） |
| `MaxTagLength` | 标签最大长度 |
| `RequestTagLength` | 请求标签最大长度 |
| `EnableMeter` | 是否在 Report 中携带 `Info` |
| `Excludes` | 排除的埋点名称数组 |

- 只有登录/上报请求携带 `Version` 时，服务端才会返回 `Excludes` / `EnableMeter`；SDK 始终携带 `Version`。
- 令牌在 5 分钟内过期会主动重新登录；业务码或 HTTP 状态为 401 时重新登录并重试一次。

### 上报协议

- 普通上报：`POST /Trace/Report?Token=...`，JSON 正文。
- 大报文（Node，正文 > 1024 字节）：`POST /Trace/ReportRaw?Token=...`，`Content-Type: application/x-gzip`，GZip 压缩正文。
- 上报正文：

```json
{
  "AppId": "MyApp",
  "AppName": "MyApp",
  "ClientId": "192.168.1.100@12345",
  "Version": "1.1.0",
  "Info": { "Id": 12345, "Name": "MyApp", "Version": "1.1.0" },
  "Builders": [
    {
      "Name": "GET /api/users",
      "StartTime": 1707744000000,
      "EndTime": 1707744060000,
      "Total": 150,
      "Errors": 3,
      "Cost": 45000,
      "MaxCost": 500,
      "MinCost": 50,
      "Samples": [],
      "ErrorSamples": []
    }
  ]
}
```

---

## 故障排查

| 现象 | 排查方向 |
|------|----------|
| 登录失败 | 检查 `server`、`appId`、`secret`；打开 `debug: true` 查看错误消息 |
| 数据未上报 | 确认调用了 `start()`；Node 退出前调用 `await tracer.stop()`；检查 `period` |
| 浏览器 CORS | 服务端需允许页面域名跨域，并允许 `Content-Type: application/json` |
| 浏览器 401 循环 | 检查 `tokenProvider` 返回值与过期时间；确认后端令牌接口可用 |
| 心跳无性能数据 | `enableMeter` 是否为 `true`；Node 心跳是否被 `excludes` 排除 |
| 上报走 `ReportRaw` | 正文超过 1024 字节时的正常行为，服务端需支持 `application/x-gzip` |
| ClientId 每次变化 | `localStorage` 是否被禁用或清理 |

---

## 测试

```bash
cd SDK/JavaScript
npm test
```

等价于依次执行：

```bash
node test/test.js
node test/integration.test.js
node test/browser.test.js
```

- `test/test.js`：Span、SpanBuilder、Tracer 配置与导出。
- `test/integration.test.js`：本地 HTTP Mock 验证登录/心跳/上报字段、GZip、动态 Period、401 重登录、stop/flush。
- `test/browser.test.js`：伪造浏览器全局，验证 ClientId、登录版本、心跳 Info、上报、Beacon、fetch/XHR、路由与 JS 错误埋点。

语法检查：

```bash
node --check src/browser.js
node --check src/tracer.js
```

---

## 许可证

MIT License

## 相关链接

- [星尘监控平台](http://star.newlifex.com)
- [GitHub 仓库](https://github.com/NewLifeX/Stardust)
- [完整文档](https://newlifex.com)
