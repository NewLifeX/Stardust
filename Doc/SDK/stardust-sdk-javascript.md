# 星尘监控 JavaScript/Node.js SDK

适用于 Node.js 14+ 和浏览器环境，提供星尘 APM 监控的接入能力。

## 安装

Node.js 环境无额外依赖（使用内置 `http`/`https` 和 `zlib` 模块）。浏览器环境使用 `fetch` API。

## 快速开始（Node.js）

```javascript
const { StardustTracer } = require('./stardust-tracer');

const tracer = new StardustTracer('http://star.example.com:6600', 'MyNodeApp', 'MySecret');
tracer.start();

// 手动埋点
const span = tracer.newSpan('业务操作');
span.tag = '参数信息';
try {
    await doSomething();
} catch (err) {
    span.setError(err);
} finally {
    span.finish();
}

// 程序退出
process.on('SIGINT', () => { tracer.stop(); process.exit(); });
```

## 完整代码

```javascript
/**
 * 星尘监控 Node.js SDK
 */

const http = require('http');
const https = require('https');
const zlib = require('zlib');
const { randomBytes } = require('crypto');
const os = require('os');

class Span {
    constructor(name, tracer, parentId = '') {
        this.id = randomBytes(8).toString('hex');
        this.parentId = parentId;
        this.traceId = randomBytes(16).toString('hex');
        this.name = name;
        this.startTime = Date.now();
        this.endTime = 0;
        this.tag = '';
        this.error = '';
        this._tracer = tracer;
    }

    setError(err) {
        this.error = err instanceof Error ? `${err.name}: ${err.message}` : String(err);
    }

    finish() {
        this.endTime = Date.now();
        this._tracer._finishSpan(this);
    }

    toJSON() {
        return {
            Id: this.id,
            ParentId: this.parentId,
            TraceId: this.traceId,
            StartTime: this.startTime,
            EndTime: this.endTime,
            Tag: this.tag,
            Error: this.error,
        };
    }
}

class SpanBuilder {
    constructor(name, maxSamples = 1, maxErrors = 10) {
        this.name = name;
        this.startTime = Date.now();
        this.endTime = 0;
        this.total = 0;
        this.errors = 0;
        this.cost = 0;
        this.maxCost = 0;
        this.minCost = 0;
        this.samples = [];
        this.errorSamples = [];
        this._maxSamples = maxSamples;
        this._maxErrors = maxErrors;
    }

    addSpan(span) {
        const elapsed = span.endTime - span.startTime;
        this.total++;
        this.cost += elapsed;
        if (this.maxCost === 0 || elapsed > this.maxCost) this.maxCost = elapsed;
        if (this.minCost === 0 || elapsed < this.minCost) this.minCost = elapsed;

        if (span.error) {
            this.errors++;
            if (this.errorSamples.length < this._maxErrors) this.errorSamples.push(span);
        } else {
            if (this.samples.length < this._maxSamples) this.samples.push(span);
        }
        this.endTime = Date.now();
    }

    toJSON() {
        return {
            Name: this.name,
            StartTime: this.startTime,
            EndTime: this.endTime,
            Total: this.total,
            Errors: this.errors,
            Cost: this.cost,
            MaxCost: this.maxCost,
            MinCost: this.minCost,
            Samples: this.samples.map(s => s.toJSON()),
            ErrorSamples: this.errorSamples.map(s => s.toJSON()),
        };
    }
}

class StardustTracer {
    constructor(server, appId, secret = '') {
        this.server = server.replace(/\/+$/, '');
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = `${this._getLocalIP()}@${process.pid}`;

        this._token = '';
        this._tokenExpire = 0;

        // 采样参数
        this.period = 60;
        this.maxSamples = 1;
        this.maxErrors = 10;
        this.timeout = 5000;
        this.maxTagLength = 1024;
        this.excludes = [];

        this._builders = new Map();
        this._running = false;
        this._reportTimer = null;
        this._pingTimer = null;
    }

    async start() {
        await this._login();
        this._running = true;
        this._reportTimer = setInterval(() => this._flush(), this.period * 1000);
        this._pingTimer = setInterval(() => this._ping(), 30000);
    }

    stop() {
        this._running = false;
        if (this._reportTimer) clearInterval(this._reportTimer);
        if (this._pingTimer) clearInterval(this._pingTimer);
        this._flush();
    }

    newSpan(name, parentId = '') {
        return new Span(name, this, parentId);
    }

    _finishSpan(span) {
        if (span.name === '/Trace/Report' || span.name === '/Trace/ReportRaw') return;
        for (const exc of this.excludes) {
            if (exc && span.name.toLowerCase().includes(exc.toLowerCase())) return;
        }

        if (span.tag && span.tag.length > this.maxTagLength) {
            span.tag = span.tag.substring(0, this.maxTagLength);
        }

        let builder = this._builders.get(span.name);
        if (!builder) {
            builder = new SpanBuilder(span.name, this.maxSamples, this.maxErrors);
            this._builders.set(span.name, builder);
        }
        builder.addSpan(span);
    }

    async _login() {
        const payload = {
            AppId: this.appId,
            Secret: this.secret,
            ClientId: this.clientId,
            AppName: this.appName,
        };

        try {
            const data = await this._postJson(`${this.server}/App/Login`, payload);
            if (data) {
                this._token = data.Token || '';
                this._tokenExpire = Date.now() / 1000 + (data.Expire || 7200);
                if (data.Code) this.appId = data.Code;
                if (data.Secret) this.secret = data.Secret;
            }
        } catch (err) {
            console.error(`[Stardust] Login failed: ${err.message}`);
        }
    }

    async _ping() {
        const payload = {
            Id: process.pid,
            Name: this.appName,
            Time: Date.now(),
        };

        try {
            const data = await this._postJson(
                `${this.server}/App/Ping?Token=${encodeURIComponent(this._token)}`,
                payload
            );
            if (data && data.Token) {
                this._token = data.Token;
            }
        } catch (err) {
            console.error(`[Stardust] Ping failed: ${err.message}`);
        }
    }

    async _report(buildersData) {
        const payload = {
            AppId: this.appId,
            AppName: this.appName,
            ClientId: this.clientId,
            Builders: buildersData,
        };

        const body = JSON.stringify(payload);

        try {
            let data;
            if (body.length > 1024) {
                const url = `${this.server}/Trace/ReportRaw?Token=${encodeURIComponent(this._token)}`;
                data = await this._postGzip(url, body);
            } else {
                const url = `${this.server}/Trace/Report?Token=${encodeURIComponent(this._token)}`;
                data = await this._postJson(url, payload);
            }
            if (data) this._applyResponse(data);
        } catch (err) {
            console.error(`[Stardust] Report failed: ${err.message}`);
        }
    }

    _applyResponse(result) {
        if (result.Period > 0) this.period = result.Period;
        if (result.MaxSamples > 0) this.maxSamples = result.MaxSamples;
        if (result.MaxErrors > 0) this.maxErrors = result.MaxErrors;
        if (result.Timeout > 0) this.timeout = result.Timeout;
        if (result.MaxTagLength > 0) this.maxTagLength = result.MaxTagLength;
        if (result.Excludes) this.excludes = result.Excludes;
    }

    _flush() {
        if (this._builders.size === 0) return;
        const list = [];
        for (const [, builder] of this._builders) {
            if (builder.total > 0) list.push(builder.toJSON());
        }
        this._builders.clear();
        if (list.length > 0) this._report(list);
    }

    // ========== HTTP 工具 ==========

    _postJson(urlStr, payload) {
        return new Promise((resolve, reject) => {
            const body = JSON.stringify(payload);
            const parsed = new URL(urlStr);
            const mod = parsed.protocol === 'https:' ? https : http;

            const req = mod.request(parsed, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json; charset=utf-8',
                    'Content-Length': Buffer.byteLength(body),
                },
                timeout: 10000,
            }, (res) => {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => {
                    try {
                        const json = JSON.parse(data);
                        resolve(json.code === 0 ? json.data : null);
                    } catch {
                        resolve(null);
                    }
                });
            });

            req.on('error', reject);
            req.on('timeout', () => { req.destroy(); reject(new Error('timeout')); });
            req.write(body);
            req.end();
        });
    }

    _postGzip(urlStr, jsonBody) {
        return new Promise((resolve, reject) => {
            zlib.gzip(Buffer.from(jsonBody, 'utf-8'), (err, compressed) => {
                if (err) { reject(err); return; }

                const parsed = new URL(urlStr);
                const mod = parsed.protocol === 'https:' ? https : http;

                const req = mod.request(parsed, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-gzip',
                        'Content-Length': compressed.length,
                    },
                    timeout: 10000,
                }, (res) => {
                    let data = '';
                    res.on('data', chunk => data += chunk);
                    res.on('end', () => {
                        try {
                            const json = JSON.parse(data);
                            resolve(json.code === 0 ? json.data : null);
                        } catch {
                            resolve(null);
                        }
                    });
                });

                req.on('error', reject);
                req.on('timeout', () => { req.destroy(); reject(new Error('timeout')); });
                req.write(compressed);
                req.end();
            });
        });
    }

    _getLocalIP() {
        const interfaces = os.networkInterfaces();
        for (const name of Object.keys(interfaces)) {
            for (const iface of interfaces[name]) {
                if (iface.family === 'IPv4' && !iface.internal) {
                    return iface.address;
                }
            }
        }
        return '127.0.0.1';
    }
}

module.exports = { StardustTracer, Span, SpanBuilder };
```

## Express 中间件

```javascript
const { StardustTracer } = require('./stardust-tracer');

const tracer = new StardustTracer('http://star.example.com:6600', 'MyExpressApp', 'secret');
tracer.start();

function stardustMiddleware(req, res, next) {
    const name = `${req.method} ${req.path}`;
    const span = tracer.newSpan(name);
    span.tag = `${req.method} ${req.originalUrl}`;

    const originalEnd = res.end;
    res.end = function (...args) {
        if (res.statusCode >= 400) {
            span.setError(`HTTP ${res.statusCode}`);
        }
        span.finish();
        originalEnd.apply(res, args);
    };

    next();
}

// 使用
const express = require('express');
const app = express();
app.use(stardustMiddleware);
```

## Koa 中间件

```javascript
const { StardustTracer } = require('./stardust-tracer');

const tracer = new StardustTracer('http://star.example.com:6600', 'MyKoaApp', 'secret');
tracer.start();

async function stardustMiddleware(ctx, next) {
    const name = `${ctx.method} ${ctx.path}`;
    const span = tracer.newSpan(name);
    span.tag = `${ctx.method} ${ctx.url}`;

    try {
        await next();
        if (ctx.status >= 400) span.setError(`HTTP ${ctx.status}`);
    } catch (err) {
        span.setError(err);
        throw err;
    } finally {
        span.finish();
    }
}

// 使用
const Koa = require('koa');
const app = new Koa();
app.use(stardustMiddleware);
```

## 浏览器环境（使用 fetch）

```javascript
class StardustBrowserTracer {
    constructor(server, appId, secret = '') {
        this.server = server.replace(/\/+$/, '');
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = `browser@${Math.random().toString(36).substring(2, 10)}`;
        this._token = '';
        this.period = 60;
        this.maxSamples = 1;
        this.maxErrors = 10;
        this.maxTagLength = 1024;
        this._builders = new Map();
    }

    async start() {
        await this._login();
        this._timer = setInterval(() => this._flush(), this.period * 1000);
    }

    stop() {
        clearInterval(this._timer);
        this._flush();
    }

    newSpan(name) {
        const span = {
            Id: crypto.randomUUID().replace(/-/g, '').substring(0, 16),
            ParentId: '',
            TraceId: crypto.randomUUID().replace(/-/g, ''),
            StartTime: Date.now(),
            EndTime: 0,
            Tag: '',
            Error: '',
        };

        return {
            ...span,
            setTag(tag) { span.Tag = tag; },
            setError(err) { span.Error = String(err); },
            finish: () => {
                span.EndTime = Date.now();
                this._addSpan(name, span);
            }
        };
    }

    _addSpan(name, span) {
        let builder = this._builders.get(name);
        if (!builder) {
            builder = { Name: name, StartTime: Date.now(), EndTime: 0, Total: 0, Errors: 0, Cost: 0, MaxCost: 0, MinCost: 0, Samples: [], ErrorSamples: [] };
            this._builders.set(name, builder);
        }
        const elapsed = span.EndTime - span.StartTime;
        builder.Total++;
        builder.Cost += elapsed;
        if (!builder.MaxCost || elapsed > builder.MaxCost) builder.MaxCost = elapsed;
        if (!builder.MinCost || elapsed < builder.MinCost) builder.MinCost = elapsed;
        builder.EndTime = Date.now();

        if (span.Error) {
            builder.Errors++;
            if (builder.ErrorSamples.length < this.maxErrors) builder.ErrorSamples.push(span);
        } else {
            if (builder.Samples.length < this.maxSamples) builder.Samples.push(span);
        }
    }

    async _login() {
        try {
            const resp = await fetch(`${this.server}/App/Login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ AppId: this.appId, Secret: this.secret, ClientId: this.clientId }),
            });
            const json = await resp.json();
            if (json.code === 0 && json.data) this._token = json.data.Token;
        } catch (err) {
            console.error('[Stardust] Login failed:', err);
        }
    }

    async _flush() {
        if (this._builders.size === 0) return;
        const list = Array.from(this._builders.values()).filter(b => b.Total > 0);
        this._builders.clear();
        if (list.length === 0) return;

        try {
            await fetch(`${this.server}/Trace/Report?Token=${encodeURIComponent(this._token)}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ AppId: this.appId, ClientId: this.clientId, Builders: list }),
            });
        } catch (err) {
            console.error('[Stardust] Report failed:', err);
        }
    }
}
```
