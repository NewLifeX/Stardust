/**
 * 浏览器 SDK 测试：在 Node 中伪造浏览器全局对象，验证登录、心跳、上报、
 * Beacon、自动埋点、路由变化与 JS 错误采集。
 */

'use strict';

const assert = require('assert');

const SERVER = 'http://stardust.test';
const INITIAL_TRACE_ID = '4bf92f3577b34da6a3ce929d0e0e4736';
const INITIAL_PARENT_ID = '00f067aa0ba902b7';
const INITIAL_TRACEPARENT = `00-${INITIAL_TRACE_ID}-${INITIAL_PARENT_ID}-01`;

// ========== 浏览器全局伪造 ==========

class MemoryStorage {
    constructor() {
        this.map = new Map();
    }
    getItem(key) {
        const value = this.map.get(String(key));
        return value === undefined ? null : value;
    }
    setItem(key, value) {
        this.map.set(String(key), String(value));
    }
    removeItem(key) {
        this.map.delete(String(key));
    }
    clear() {
        this.map.clear();
    }
}

function createEventTarget(extra) {
    const listeners = Object.create(null);
    const target = Object.assign({
        addEventListener(type, fn) {
            if (!listeners[type]) listeners[type] = [];
            listeners[type].push(fn);
        },
        removeEventListener(type, fn) {
            const list = listeners[type];
            if (!list) return;
            const index = list.indexOf(fn);
            if (index >= 0) list.splice(index, 1);
        },
        _emit(type, event) {
            const evt = event || {};
            evt.type = type;
            if (!evt.target) evt.target = target;
            const list = (listeners[type] || []).slice();
            for (let i = 0; i < list.length; i++) list[i].call(target, evt);
            return evt;
        },
        _listeners: listeners,
    }, extra || {});
    return target;
}

const location = {
    href: 'https://example.com/app/',
    origin: 'https://example.com',
    pathname: '/app/',
    search: '',
    hash: '',
};

function updateLocation(url) {
    if (!url) return;
    const parsed = new URL(String(url), location.href);
    location.href = parsed.href;
    location.pathname = parsed.pathname;
    location.search = parsed.search;
    location.hash = parsed.hash;
}

const history = {
    pushState(state, title, url) {
        updateLocation(url);
    },
    replaceState(state, title, url) {
        updateLocation(url);
    },
};

const documentObject = createEventTarget({
    hidden: false,
    visibilityState: 'visible',
    readyState: 'complete',
});

const beaconCalls = [];
const navigatorObject = {
    userAgent: 'Mozilla/5.0 TestBrowser',
    platform: 'Win32',
    userAgentData: { platform: 'Windows 11' },
    hardwareConcurrency: 8,
    sendBeacon(url, data) {
        beaconCalls.push({ url: url, data: data });
        return true;
    },
};

const navigationEntry = {
    navigationStart: 100,
    domainLookupStart: 110,
    domainLookupEnd: 130,
    connectStart: 140,
    connectEnd: 200,
    requestStart: 210,
    responseStart: 260,
    domContentLoadedEventStart: 300,
    domContentLoadedEventEnd: 400,
    loadEventStart: 450,
    loadEventEnd: 500,
};

const performanceObject = {
    timeOrigin: 1700000000000,
    memory: {
        usedJSHeapSize: 12345678,
        totalJSHeapSize: 23456789,
    },
    getEntriesByType(type) {
        return type === 'navigation' ? [navigationEntry] : [];
    },
    now() {
        return 0;
    },
};

class FakeBlob {
    constructor(parts, options) {
        this.parts = parts || [];
        this.type = options && options.type ? options.type : '';
    }
}

const fetchCalls = [];
let loginCalls = 0;
const reportResponse = {
    Period: 15,
    MaxSamples: 2,
    MaxErrors: 3,
    Timeout: 4000,
    MaxTagLength: 600,
    RequestTagLength: 256,
    EnableMeter: true,
    Excludes: ['ignore-me'],
};

function jsonResponse(data, status) {
    return {
        status: status || 200,
        ok: (status || 200) < 400,
        json() {
            return Promise.resolve(data);
        },
    };
}

function fakeFetch(url, init) {
    const text = String(url);
    fetchCalls.push({ url: text, init: init || {} });

    if (text.indexOf(SERVER + '/App/Login') === 0) {
        loginCalls++;
        return Promise.resolve(jsonResponse({
            code: 0,
            data: { Token: 'browser-token', Expire: 7200 },
        }));
    }
    if (text.indexOf('/App/Login') !== -1) {
        return Promise.resolve(jsonResponse({ code: 0, data: { Token: 'ignored-login' } }));
    }
    if (text.indexOf('/App/Ping') !== -1) {
        return Promise.resolve(jsonResponse({
            code: 0,
            data: { Token: 'browser-renewed', Expire: 7200 },
        }));
    }
    if (text.indexOf('/Trace/Report') !== -1) {
        return Promise.resolve(jsonResponse({ code: 0, data: reportResponse }));
    }
    if (text.indexOf('fail') !== -1) {
        return Promise.resolve(jsonResponse({ code: 500, message: 'mock failure' }, 500));
    }
    return Promise.resolve(jsonResponse({ code: 0, data: { ok: true } }));
}

class FakeXMLHttpRequest {
    constructor() {
        this._listeners = Object.create(null);
        this.headers = Object.create(null);
        this.status = 0;
    }
    open(method, url) {
        this.method = method;
        this.url = url;
    }
    setRequestHeader(name, value) {
        this.headers[String(name).toLowerCase()] = value;
    }
    addEventListener(type, fn) {
        if (!this._listeners[type]) this._listeners[type] = [];
        this._listeners[type].push(fn);
    }
    send() {
        this.status = 200;
        this._emit('load');
    }
    _emit(type) {
        const list = (this._listeners[type] || []).slice();
        for (let i = 0; i < list.length; i++) list[i].call(this, { type: type });
    }
}
FakeXMLHttpRequest.prototype.__fake = true;

const fakeWindow = createEventTarget({
    document: documentObject,
    location: location,
    history: history,
    navigator: navigatorObject,
    performance: performanceObject,
    localStorage: new MemoryStorage(),
    sessionStorage: new MemoryStorage(),
    fetch: fakeFetch,
    XMLHttpRequest: FakeXMLHttpRequest,
    Blob: FakeBlob,
});

function setGlobal(name, value) {
    try {
        global[name] = value;
    } catch (err) {
        // 某些只读全局变量无法覆盖
    }
}

setGlobal('window', fakeWindow);
setGlobal('document', documentObject);
setGlobal('location', location);
setGlobal('navigator', navigatorObject);
setGlobal('performance', performanceObject);
setGlobal('localStorage', fakeWindow.localStorage);
setGlobal('sessionStorage', fakeWindow.sessionStorage);
setGlobal('fetch', fakeFetch);
setGlobal('XMLHttpRequest', FakeXMLHttpRequest);
setGlobal('Blob', FakeBlob);

// 必须在前面的全局伪造完成后再加载 SDK
const BrowserTracer = require('../src/browser');
const middlewareBrowser = require('../src/middleware/browser');

let passed = 0;
let failed = 0;

async function test(name, fn) {
    try {
        await fn();
        console.log(`✅ ${name}`);
        passed++;
    } catch (err) {
        console.error(`❌ ${name}`);
        console.error(`   ${err && err.stack ? err.stack : err}`);
        failed++;
    }
}

function waitFor(predicate, timeout) {
    const start = Date.now();
    return new Promise((resolve, reject) => {
        const tick = function () {
            let ok = false;
            try {
                ok = predicate();
            } catch (err) {
                reject(err);
                return;
            }
            if (ok) {
                resolve();
                return;
            }
            if (Date.now() - start > (timeout || 2000)) {
                reject(new Error('等待条件超时'));
                return;
            }
            setTimeout(tick, 10);
        };
        tick();
    });
}

function parseBody(call) {
    try {
        return JSON.parse(call.init.body);
    } catch (err) {
        return null;
    }
}

function getHeaderValue(headers, name) {
    if (!headers) return null;
    if (typeof headers.get === 'function') return headers.get(name);
    const lower = name.toLowerCase();
    for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === lower) return headers[key];
    }
    return null;
}

function findCalls(pathPart) {
    return fetchCalls.filter(function (call) {
        return call.url.indexOf(pathPart) !== -1;
    });
}

function findBuilder(body, name) {
    if (!body || !Array.isArray(body.Builders)) return null;
    for (let i = 0; i < body.Builders.length; i++) {
        if (body.Builders[i].Name === name) return body.Builders[i];
    }
    return null;
}

async function main() {
    const tracer = new BrowserTracer({
        server: SERVER,
        appId: 'BrowserApp',
        appName: 'Browser App',
        secret: 'browser-secret',
        version: '2.3.4',
        period: 120,
        heartbeatPeriod: 30,
        debug: false,
        initialTraceparent: INITIAL_TRACEPARENT,
    });

    try {
        await test('全局导出与 clientId 持久化', async () => {
            assert.strictEqual(fakeWindow.StardustBrowserTracer, BrowserTracer, '应暴露 StardustBrowserTracer');
            assert.strictEqual(fakeWindow.Stardust.version, '1.1.0', '应暴露 version');
            assert.strictEqual(typeof fakeWindow.Stardust.createTracer, 'function', '应暴露 createTracer');
            assert.strictEqual(typeof middlewareBrowser.installBrowserInterceptors, 'function', '中间件应重新导出');

            const first = tracer.clientId;
            assert.ok(/^browser@[0-9a-f]{32}$/.test(first), 'clientId 格式应为 browser@32位十六进制');
            assert.strictEqual(fakeWindow.localStorage.getItem('stardust.clientId.BrowserApp'), first, 'clientId 应持久化');

            const second = new BrowserTracer({ server: SERVER, appId: 'BrowserApp' });
            assert.strictEqual(second.clientId, first, '相同 appId 应复用 clientId');

            const created = fakeWindow.Stardust.createTracer({ server: SERVER, appId: 'BrowserApp' });
            assert.ok(created instanceof BrowserTracer, 'createTracer 应返回浏览器追踪器');
        });

        await test('登录请求携带 Version / IP / ClientId 并缓存令牌', async () => {
            const before = loginCalls;
            await tracer.start();

            assert.strictEqual(loginCalls, before + 1, '应发起一次登录');
            const logins = findCalls(SERVER + '/App/Login');
            const call = logins[logins.length - 1];
            const body = parseBody(call);
            assert.ok(body, '登录请求应有 JSON 正文');
            assert.strictEqual(body.AppId, 'BrowserApp', 'AppId 应匹配');
            assert.strictEqual(body.AppName, 'Browser App', 'AppName 应匹配');
            assert.strictEqual(body.Secret, 'browser-secret', 'Secret 应匹配');
            assert.strictEqual(body.Version, '2.3.4', '登录应携带 Version');
            assert.strictEqual(body.IP, '', '浏览器 IP 应为空');
            assert.strictEqual(body.ClientId, tracer.clientId, 'ClientId 应匹配');

            const cached = JSON.parse(fakeWindow.sessionStorage.getItem('stardust.token.BrowserApp'));
            assert.strictEqual(cached.token, 'browser-token', '登录令牌应缓存');
            assert.ok(cached.expire > Date.now(), '缓存应包含未来过期时间');
        });

        await test('心跳 Info 包含内存与硬件信息', async () => {
            await tracer._ping();

            const pings = findCalls('/App/Ping');
            assert.ok(pings.length > 0, '应发送心跳');
            const call = pings[pings.length - 1];
            const body = parseBody(call);

            assert.ok(call.url.indexOf('Token=') !== -1, '心跳应携带令牌');
            assert.strictEqual(body.Id, 0, 'Id 应为 0');
            assert.strictEqual(body.Name, 'Browser App', 'Name 应匹配');
            assert.strictEqual(body.Version, '2.3.4', 'Version 应匹配');
            assert.strictEqual(body.CommandLine, 'Mozilla/5.0 TestBrowser', 'CommandLine 应为 UA');
            assert.strictEqual(body.UserName, '', 'UserName 应为空');
            assert.strictEqual(body.MachineName, 'Windows 11', 'MachineName 应取 userAgentData.platform');
            assert.strictEqual(body.IP, '', 'IP 应为空');
            assert.strictEqual(body.StartTime, new Date(1700000000000).toISOString(), 'StartTime 应取 timeOrigin');
            assert.strictEqual(body.ProcessorTime, 0, 'ProcessorTime 应为 0');
            assert.strictEqual(body.CpuUsage, 0, 'CpuUsage 应为 0');
            assert.strictEqual(body.WorkingSet, 12345678, 'WorkingSet 应取 usedJSHeapSize');
            assert.strictEqual(body.HeapSize, 23456789, 'HeapSize 应取 totalJSHeapSize');
            assert.strictEqual(body.Threads, 8, 'Threads 应取 hardwareConcurrency');
            assert.strictEqual(body.Handles, 0, 'Handles 应为 0');
            assert.strictEqual(body.Connections, 0, 'Connections 应为 0');
            assert.strictEqual(body.GCCount, 0, 'GCCount 应为 0');
            assert.strictEqual(body.Listens, 'https://example.com', 'Listens 应为页面 origin');
            assert.ok(Math.abs(body.Time - Date.now()) < 10000, 'Time 应接近当前时间');
        });

        await test('上报包含 Version / Info / Builders 并应用响应参数', async () => {
            fetchCalls.length = 0;

            const span = tracer.newSpan('manual.op');
            span.setTag('manual-tag');
            span.finish();
            await tracer.flush();

            const reports = findCalls('/Trace/Report');
            assert.strictEqual(reports.length, 1, '应上报一次');
            const body = parseBody(reports[0]);

            assert.strictEqual(body.AppId, 'BrowserApp', 'AppId 应匹配');
            assert.strictEqual(body.AppName, 'Browser App', 'AppName 应匹配');
            assert.strictEqual(body.ClientId, tracer.clientId, 'ClientId 应匹配');
            assert.strictEqual(body.Version, '2.3.4', '上报应携带 Version');
            assert.ok(body.Info, '上报应包含 Info');
            assert.strictEqual(body.Info.Version, '2.3.4', 'Info.Version 应匹配');
            assert.ok(Array.isArray(body.Builders) && body.Builders.length > 0, '应包含 Builders');

            const manual = findBuilder(body, 'manual.op');
            assert.ok(manual, '应包含手动埋点');
            assert.strictEqual(manual.Total, 1, '手动埋点计数应匹配');
            assert.strictEqual(manual.Samples[0].Tag, 'manual-tag', '样本 Tag 应匹配');

            const pageLoad = findBuilder(body, 'page.load');
            assert.ok(pageLoad, '应包含页面加载埋点');
            assert.strictEqual(pageLoad.Samples[0].Tag, 'dns=20;tcp=60;ttfb=50;dom=100;load=50', '页面加载 Tag 应匹配');
            assert.strictEqual(pageLoad.Samples[0].TraceId, INITIAL_TRACE_ID, 'page.load 应复用后端 TraceId');
            assert.strictEqual(pageLoad.Samples[0].ParentId, INITIAL_PARENT_ID, 'page.load 应挂到后端 SpanId 下');

            assert.strictEqual(tracer.period, 15, 'Period 应被应用');
            assert.strictEqual(tracer.maxSamples, 2, 'MaxSamples 应被应用');
            assert.strictEqual(tracer.maxErrors, 3, 'MaxErrors 应被应用');
            assert.strictEqual(tracer.maxTagLength, 600, 'MaxTagLength 应被应用');
            assert.strictEqual(tracer.requestTagLength, 256, 'RequestTagLength 应被应用');
            assert.ok(tracer.excludes.indexOf('ignore-me') >= 0, 'Excludes 应被应用');
            assert.ok(tracer._reportTimer, '上报定时器应存在');
        });

        await test('pagehide 使用 sendBeacon 上报', async () => {
            const span = tracer.newSpan('beacon.span');
            span.finish();
            assert.strictEqual(tracer._builders.size, 1, '应有一个待上报构建器');

            beaconCalls.length = 0;
            fakeWindow._emit('pagehide');

            assert.strictEqual(beaconCalls.length, 1, '应调用 sendBeacon');
            assert.ok(beaconCalls[0].url.indexOf('/Trace/Report?Token=') !== -1, 'Beacon 地址应正确');
            assert.ok(beaconCalls[0].data instanceof FakeBlob, 'Beacon 应发送 Blob');
            assert.strictEqual(beaconCalls[0].data.type, 'application/json', 'Blob 类型应为 application/json');

            const payload = JSON.parse(beaconCalls[0].data.parts[0]);
            assert.strictEqual(payload.Version, '2.3.4', 'Beacon 应携带 Version');
            assert.ok(findBuilder(payload, 'beacon.span'), 'Beacon 应包含待上报构建器');
            assert.strictEqual(tracer._builders.size, 0, 'Beacon 后构建器应清空');
        });

        await test('页面隐藏暂停心跳，恢复可见立即心跳', async () => {
            const before = findCalls('/App/Ping').length;

            documentObject.hidden = true;
            documentObject.visibilityState = 'hidden';
            documentObject._emit('visibilitychange');
            await new Promise((resolve) => setTimeout(resolve, 50));
            assert.strictEqual(findCalls('/App/Ping').length, before, '隐藏时不应心跳');

            documentObject.hidden = false;
            documentObject.visibilityState = 'visible';
            documentObject._emit('visibilitychange');
            await waitFor(function () {
                return findCalls('/App/Ping').length > before;
            });
        });

        await test('fetch 拦截器生成 METHOD path 埋点', async () => {
            assert.strictEqual(tracer._builders.has('GET /api/data'), false, '初始不应存在');

            await fakeWindow.fetch('/api/data?q=1');
            const sameOrigin = tracer._builders.get('GET /api/data');
            assert.ok(sameOrigin, '同源请求应生成路径埋点');
            assert.strictEqual(sameOrigin.total, 1, '同源请求计数应匹配');
            assert.strictEqual(sameOrigin.samples[0].tag, 'https://example.com/api/data?q=1', 'Tag 应为完整 URL');

            const dataCalls = findCalls('/api/data?q=1');
            const dataHeaders = dataCalls[dataCalls.length - 1].init.headers || {};
            const traceparent = getHeaderValue(dataHeaders, 'traceparent');
            assert.ok(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/.test(traceparent || ''), 'fetch 应注入 traceparent');

            await fakeWindow.fetch('https://api.example.com/users?x=1');
            const crossOrigin = tracer._builders.get('GET api.example.com/users');
            assert.ok(crossOrigin, '跨域请求应生成 host+path 埋点');

            await fakeWindow.fetch('/api/fail');
            const failed = tracer._builders.get('GET /api/fail');
            assert.ok(failed, '失败请求应生成埋点');
            assert.strictEqual(failed.errors, 1, '状态码 >=400 应计为错误');

            const countBefore = tracer._builders.size;
            await fakeWindow.fetch('/Trace/Report?Token=x');
            await fakeWindow.fetch('/App/Login');
            await fakeWindow.fetch('/App/Ping');
            assert.strictEqual(tracer._builders.size, countBefore, '监控接口不应生成埋点');
        });

        await test('XMLHttpRequest 拦截器生成埋点', async () => {
            const xhr = new FakeXMLHttpRequest();
            xhr.open('POST', '/api/xhr?q=1');
            xhr.send();

            const builder = tracer._builders.get('POST /api/xhr');
            assert.ok(builder, 'XHR 应生成埋点');
            assert.strictEqual(builder.total, 1, 'XHR 计数应匹配');
            assert.ok(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/.test(xhr.headers.traceparent || ''), 'XHR 应注入 traceparent');
        });

        await test('withSpan 维护活动 Span 栈并继承上下文', async () => {
            let parent = null;
            let child = null;

            tracer.withSpan('ctx.parent', function (span) {
                parent = span;
                child = tracer.newSpan('ctx.child');
                child.finish();
            });

            assert.ok(parent, '应返回父 Span');
            assert.strictEqual(parent._finished, true, 'withSpan 结束时应结算父 Span');
            assert.strictEqual(child.parentId, parent.id, '子 Span 父 ID 应匹配');
            assert.strictEqual(child.traceId, parent.traceId, '子 Span 应继承 TraceId');
            assert.strictEqual(tracer._spanStack.length, 0, '活动 Span 栈应清空');
        });

        await test('页面内 Span 共用页面 TraceId', async () => {
            const first = tracer.newSpan('page.first');
            const second = tracer.newSpan('page.second');
            first.finish();
            second.finish();

            assert.strictEqual(first.traceId, INITIAL_TRACE_ID, '页面内 Span 应复用后端 TraceId');
            assert.strictEqual(second.traceId, INITIAL_TRACE_ID, '多个页面内 Span 应共享同一 TraceId');
        });

        await test('SPA 路由变化生成 page.view 埋点且去重', async () => {
            fakeWindow.history.pushState({}, '', '/app/users?id=1');
            const users = tracer._builders.get('page.view /app/users');
            assert.ok(users, 'pushState 应生成 page.view');
            assert.strictEqual(users.total, 1, '首次路由计数应为 1');

            fakeWindow.history.pushState({}, '', '/app/users?id=2');
            assert.strictEqual(tracer._builders.get('page.view /app/users').total, 1, '连续相同路径应去重');

            fakeWindow.history.pushState({}, '', '/app/orders');
            assert.ok(tracer._builders.get('page.view /app/orders'), '新路径应生成埋点');

            location.href = 'https://example.com/app/home';
            location.pathname = '/app/home';
            fakeWindow._emit('popstate');
            assert.ok(tracer._builders.get('page.view /app/home'), 'popstate 应生成埋点');
        });

        await test('JS 错误、Promise 拒绝与资源错误生成埋点', async () => {
            fakeWindow._emit('error', {
                message: 'boom',
                filename: 'app.js',
                lineno: 12,
                colno: 3,
                error: new Error('boom'),
            });

            const jsError = tracer._builders.get('js.error');
            assert.ok(jsError, '应生成 js.error');
            assert.strictEqual(jsError.total, 1, 'js.error 计数应匹配');
            assert.ok(jsError.errorSamples[0].error.indexOf('Error: boom') >= 0, '错误消息应被记录');
            assert.ok(jsError.errorSamples[0].tag.indexOf('source=app.js') >= 0, 'Tag 应包含来源');

            fakeWindow._emit('unhandledrejection', { reason: new Error('rejected') });
            assert.strictEqual(tracer._builders.get('js.error').total, 2, 'Promise 拒绝应计入 js.error');

            fakeWindow._emit('error', {
                target: { tagName: 'IMG', src: 'https://example.com/missing.png' },
            });
            const resourceError = tracer._builders.get('resource.error');
            assert.ok(resourceError, '应生成 resource.error');
            assert.ok(resourceError.errorSamples[0].error.indexOf('IMG') >= 0, '资源错误应记录元素类型');
        });

        await test('相同 appId 复用 sessionStorage 中的令牌', async () => {
            const before = loginCalls;
            const previousFetch = fakeWindow.fetch;
            fakeWindow.fetch = fakeFetch;

            const cachedTracer = new BrowserTracer({
                server: SERVER,
                appId: 'BrowserApp',
                appName: 'Browser App',
                version: '2.3.4',
                period: 120,
            });
            try {
                await cachedTracer.start();
                assert.strictEqual(loginCalls, before, '有缓存令牌时不应重新登录');
            } finally {
                await cachedTracer.stop();
                fakeWindow.fetch = previousFetch;
            }
        });

        await test('tokenProvider 替代登录并缓存令牌', async () => {
            const before = loginCalls;
            let providerCalls = 0;
            const previousFetch = fakeWindow.fetch;
            fakeWindow.fetch = fakeFetch;

            const providerTracer = new BrowserTracer({
                server: SERVER,
                appId: 'ProviderApp',
                appName: 'Provider App',
                version: '2.3.4',
                tokenProvider: async function () {
                    providerCalls++;
                    return { token: 'provider-token', expire: 3600 };
                },
                period: 120,
            });
            try {
                await providerTracer.start();
                assert.strictEqual(providerCalls, 1, 'tokenProvider 应被调用一次');
                assert.strictEqual(loginCalls, before, '不应调用登录接口');

                const span = providerTracer.newSpan('provider.op');
                span.finish();
                fetchCalls.length = 0;
                await providerTracer.flush();

                const reports = findCalls('/Trace/Report');
                assert.ok(reports.length > 0, '应上报');
                assert.ok(reports[reports.length - 1].url.indexOf('Token=provider-token') !== -1, '应使用 provider 令牌');
                const body = parseBody(reports[reports.length - 1]);
                assert.strictEqual(body.AppId, 'ProviderApp', 'AppId 应匹配');

                const cached = JSON.parse(fakeWindow.sessionStorage.getItem('stardust.token.ProviderApp'));
                assert.strictEqual(cached.token, 'provider-token', 'provider 令牌应缓存');
            } finally {
                await providerTracer.stop();
                fakeWindow.fetch = previousFetch;
            }
        });

        await test('localStorage 不可用时回退到内存 clientId', async () => {
            const originalWindowStorage = fakeWindow.localStorage;
            const originalGlobalStorage = global.localStorage;
            const throwing = {
                getItem() {
                    throw new Error('denied');
                },
                setItem() {
                    throw new Error('denied');
                },
                removeItem() {
                    throw new Error('denied');
                },
            };
            fakeWindow.localStorage = throwing;
            global.localStorage = throwing;
            try {
                const temp = new BrowserTracer({ server: SERVER, appId: 'NoStorageApp' });
                assert.ok(/^browser@[0-9a-f]{32}$/.test(temp.clientId), '应回退生成内存 clientId');
            } finally {
                fakeWindow.localStorage = originalWindowStorage;
                global.localStorage = originalGlobalStorage;
            }
        });
    } finally {
        await tracer.stop();
    }
}

const guard = setTimeout(() => {
    console.error('❌ 浏览器测试超时');
    process.exit(1);
}, 30000);

main().then(() => {
    clearTimeout(guard);
    console.log('\n' + '='.repeat(50));
    console.log(`浏览器测试完成: ${passed} 通过, ${failed} 失败`);
    console.log('='.repeat(50));
    if (failed > 0) process.exit(1);
}, (err) => {
    clearTimeout(guard);
    console.error(err && err.stack ? err.stack : err);
    process.exit(1);
});
