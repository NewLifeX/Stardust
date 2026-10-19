/**
 * 自动埋点测试：traceparent、出站/入站 HTTP、中间件复用、DB 与 Redis。
 */

'use strict';

const http = require('http');
const { EventEmitter } = require('events');
const assert = require('assert');
const {
    StardustTracer,
    Span,
    parseTraceparent,
    formatTraceparent,
    generateTraceId,
    generateSpanId,
} = require('../src');
const httpInstrument = require('../src/instrument/http');
const dbInstrument = require('../src/instrument/db');
const redisInstrument = require('../src/instrument/redis');
const createExpressMiddleware = require('../src/middleware/express');
const { postJson } = require('../src/http');

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

/**
 * 创建用于自动埋点的收集型追踪器。
 * @param {Object} [extra] 额外配置
 * @returns {Object} 追踪器
 */
function createAutoTracer(extra) {
    const options = Object.assign({
        server: 'http://127.0.0.1:1',
        appId: 'InstrumentApp',
        autoInstrument: true,
    }, extra || {});

    const tracer = new StardustTracer(options);
    tracer.spans = [];
    tracer._finishSpan = function (span) {
        tracer.spans.push(span);
    };
    return tracer;
}

/**
 * 启动本地 HTTP 服务器。
 * @param {Function} handler 请求处理函数
 * @returns {Promise<Object>} { server, port, url }
 */
function listen(handler) {
    return new Promise((resolve, reject) => {
        const server = http.createServer(handler);
        server.once('error', reject);
        server.listen(0, '127.0.0.1', () => {
            const address = server.address();
            resolve({ server, port: address.port, url: 'http://127.0.0.1:' + address.port });
        });
    });
}

/**
 * 关闭服务器。
 * @param {Object} server 服务器
 * @returns {Promise<void>} 完成
 */
function close(server) {
    return new Promise((resolve) => {
        try {
            server.close(() => resolve());
        } catch (err) {
            resolve();
        }
    });
}

/**
 * 发起 GET 请求并读取完整响应。
 * @param {String} url 地址
 * @param {Object} [headers] 请求头
 * @returns {Promise<Object>} { statusCode, body }
 */
function httpGet(url, headers) {
    return new Promise((resolve, reject) => {
        const req = http.get(url, { headers: headers || {} }, (res) => {
            const chunks = [];
            res.on('data', (chunk) => chunks.push(chunk));
            res.on('end', () => resolve({ statusCode: res.statusCode, body: Buffer.concat(chunks).toString('utf8') }));
        });
        req.on('error', reject);
    });
}

/**
 * 按名称查找 Span。
 * @param {Array} spans Span 列表
 * @param {String} name 名称
 * @returns {Object|null} Span
 */
function findSpan(spans, name) {
    for (const span of spans) {
        if (span.name === name) return span;
    }
    return null;
}

async function main() {
    await test('traceparent 解析与格式化', async () => {
        const traceId = '0af7651916cd43dd8448eb211c80319c';
        const spanId = 'b7ad6b7169203331';

        const parsed = parseTraceparent('00-' + traceId + '-' + spanId + '-01');
        assert.ok(parsed, '合法 traceparent 应解析成功');
        assert.strictEqual(parsed.traceId, traceId, 'TraceId 应匹配');
        assert.strictEqual(parsed.spanId, spanId, 'SpanId 应匹配');
        assert.strictEqual(parsed.sampled, true, 'flags=01 应采样');

        const upper = parseTraceparent(['00-' + traceId.toUpperCase() + '-' + spanId.toUpperCase() + '-00']);
        assert.ok(upper, '数组头值应解析成功');
        assert.strictEqual(upper.traceId, traceId, '大写应归一化为小写');
        assert.strictEqual(upper.sampled, false, 'flags=00 不采样');

        const short = parseTraceparent('00-abcd-' + spanId + '-01');
        assert.ok(short, '兼容 .NET 短 TraceId');
        assert.strictEqual(short.traceId, 'abcd', '短 TraceId 应保留');

        assert.strictEqual(parseTraceparent(''), null, '空值应返回 null');
        assert.strictEqual(parseTraceparent('00-not-hex-' + spanId + '-01'), null, '非法 TraceId 应返回 null');
        assert.strictEqual(parseTraceparent('00-' + traceId + '-short-01'), null, '非法 SpanId 应返回 null');

        assert.strictEqual(formatTraceparent(traceId, spanId), '00-' + traceId + '-' + spanId + '-01', '格式化结果应匹配');
        assert.strictEqual(formatTraceparent(traceId, spanId, false), '00-' + traceId + '-' + spanId + '-00', '不采样应写 00');
        assert.ok(/^[0-9a-f]{32}$/.test(generateTraceId()), 'TraceId 应为 32 位十六进制');
        assert.ok(/^[0-9a-f]{16}$/.test(generateSpanId()), 'SpanId 应为 16 位十六进制');
    });

    await test('Span 支持显式 traceId 与 parentId', async () => {
        const span = new Span('explicit', { _finishSpan: () => { } }, { parentId: 'parent-1', traceId: 'trace-1' });
        assert.strictEqual(span.parentId, 'parent-1', '父 ID 应匹配');
        assert.strictEqual(span.traceId, 'trace-1', 'TraceId 应匹配');

        const legacy = new Span('legacy', { _finishSpan: () => { } }, 'parent-2');
        assert.strictEqual(legacy.parentId, 'parent-2', '旧版父 ID 参数应兼容');
        assert.strictEqual(legacy.traceId.length, 32, '旧版调用应生成 TraceId');
    });

    await test('autoInstrument:false 不安装任何补丁', async () => {
        const tracer = new StardustTracer({
            server: 'http://127.0.0.1:1',
            appId: 'InstrumentApp',
            autoInstrument: false,
        });
        tracer.spans = [];
        tracer._finishSpan = function (span) {
            tracer.spans.push(span);
        };

        assert.strictEqual(tracer.autoInstrument, false, 'autoInstrument 应为 false');
        assert.strictEqual(http.request.__stardustWrapped, undefined, 'http.request 不应被包装');
        assert.strictEqual(http.createServer.__stardustWrapped, undefined, 'http.createServer 不应被包装');

        const target = await listen((req, res) => res.end('ok'));
        try {
            await httpGet(target.url + '/plain');
            assert.strictEqual(tracer.spans.length, 0, '不应产生任何 Span');
        } finally {
            await close(target.server);
        }
    });

    await test('出站 HTTP 注入 traceparent 并关联父 Span', async () => {
        const tracer = createAutoTracer();
        let incomingHeaders = null;
        const target = await listen((req, res) => {
            incomingHeaders = req.headers;
            res.end('ok');
        });

        try {
            let parentSpan = null;
            await tracer.withSpan('parent-op', async (span) => {
                parentSpan = span;
                const result = await httpGet(target.url + '/ping?a=1');
                assert.strictEqual(result.statusCode, 200, '请求应成功');
            });

            const parsed = parseTraceparent(incomingHeaders.traceparent);
            assert.ok(parsed, '应注入 traceparent');
            assert.strictEqual(parsed.traceId, parentSpan.traceId, 'traceId 应继承父 Span');
            assert.strictEqual(parsed.spanId.length, 16, '注入的 spanId 应为 16 位');

            const outbound = findSpan(tracer.spans, 'GET 127.0.0.1:' + target.port + '/ping');
            assert.ok(outbound, '应生成出站 Span');
            assert.strictEqual(outbound.parentId, parentSpan.id, '出站 Span 的父 ID 应为当前 Span');
            assert.strictEqual(outbound.traceId, parentSpan.traceId, '出站 Span 应复用 TraceId');
            assert.strictEqual(parsed.spanId, outbound.id, 'traceparent 的 spanId 应为出站 Span ID');
            assert.ok(outbound.tag.indexOf('/ping?a=1') !== -1, 'Tag 应为完整 URL');

            const serverSpan = findSpan(tracer.spans, 'GET /ping');
            assert.ok(serverSpan, '服务端应解析出来自入站请求的 Span');
            assert.strictEqual(serverSpan.traceId, parentSpan.traceId, '服务端 Span 应复用 TraceId');
            assert.strictEqual(serverSpan.parentId, outbound.id, '服务端 Span 的父 ID 应为出站 Span ID');
        } finally {
            await close(target.server);
        }
    });

    await test('http.request 兼容 null/undefined options 与 callback', async () => {
        const tracer = createAutoTracer();
        let incomingHeaders = null;
        const target = await listen((req, res) => {
            incomingHeaders = req.headers;
            res.end('ok');
        });

        try {
            const call = (options) => new Promise((resolve, reject) => {
                const timer = setTimeout(() => reject(new Error('callback not called')), 2000);
                const req = http.request(target.url + '/null-options', options, (res) => {
                    clearTimeout(timer);
                    res.resume();
                    res.on('end', resolve);
                });
                req.on('error', (err) => {
                    clearTimeout(timer);
                    reject(err);
                });
                req.end();
            });

            await call(null);
            assert.ok(parseTraceparent(incomingHeaders.traceparent), 'null options 应正常发送并注入 traceparent');

            await call(undefined);
            assert.ok(parseTraceparent(incomingHeaders.traceparent), 'undefined options 应正常发送并注入 traceparent');

            assert.ok(findSpan(tracer.spans, 'GET 127.0.0.1:' + target.port + '/null-options'), '应生成出站 Span');
        } finally {
            await close(target.server);
        }
    });

    await test('出站 fetch 注入 traceparent 并关联父 Span', async () => {
        if (typeof globalThis.fetch !== 'function') return;

        const tracer = createAutoTracer();
        let incomingHeaders = null;
        const target = await listen((req, res) => {
            incomingHeaders = req.headers;
            res.end('ok');
        });

        try {
            let parentSpan = null;
            await tracer.withSpan('fetch-parent', async (span) => {
                parentSpan = span;
                const response = await globalThis.fetch(target.url + '/fetch?q=1');
                await response.text();
                assert.strictEqual(response.status, 200, 'fetch 应成功');
            });

            const parsed = parseTraceparent(incomingHeaders.traceparent);
            assert.ok(parsed, 'fetch 应注入 traceparent');
            assert.strictEqual(parsed.traceId, parentSpan.traceId, 'fetch traceId 应继承父 Span');

            const outbound = findSpan(tracer.spans, 'GET 127.0.0.1:' + target.port + '/fetch');
            assert.ok(outbound, '应生成 fetch 出站 Span');
            assert.strictEqual(outbound.parentId, parentSpan.id, 'fetch 出站 Span 的父 ID 应正确');
            assert.strictEqual(outbound.traceId, parentSpan.traceId, 'fetch 出站 Span 应复用 TraceId');
            assert.strictEqual(parsed.spanId, outbound.id, 'traceparent 的 spanId 应为 fetch 出站 Span ID');
        } finally {
            await close(target.server);
        }
    });

    await test('入站 traceparent 复用 TraceId 与上游 SpanId', async () => {
        const tracer = createAutoTracer();
        const target = await listen((req, res) => res.end('ok'));
        const traceId = generateTraceId();
        const spanId = generateSpanId();

        try {
            await httpGet(target.url + '/inbound?x=1', {
                traceparent: formatTraceparent(traceId, spanId),
            });

            const serverSpan = findSpan(tracer.spans, 'GET /inbound');
            assert.ok(serverSpan, '应生成服务端 Span');
            assert.strictEqual(serverSpan.traceId, traceId, 'TraceId 应复用上游');
            assert.strictEqual(serverSpan.parentId, spanId, 'ParentId 应为上游 SpanId');
            assert.strictEqual(serverSpan.tag, 'GET /inbound?x=1', 'Tag 应为 METHOD + 原始 URL');

            const outbound = findSpan(tracer.spans, 'GET 127.0.0.1:' + target.port + '/inbound');
            assert.ok(outbound, '应生成出站 Span');
            assert.ok(outbound.tag.indexOf('/inbound?x=1') !== -1, '出站 Tag 应包含完整 URL');
        } finally {
            await close(target.server);
        }
    });

    await test('4xx/5xx 与网络异常记录错误', async () => {
        const tracer = createAutoTracer();
        const target = await listen((req, res) => {
            res.statusCode = 503;
            res.end('unavailable');
        });

        try {
            await httpGet(target.url + '/fail');
            const serverSpan = findSpan(tracer.spans, 'GET /fail');
            assert.ok(serverSpan, '应生成服务端 Span');
            assert.ok(serverSpan.error.indexOf('503') !== -1, '服务端 5xx 应记录错误');

            const outbound = findSpan(tracer.spans, 'GET 127.0.0.1:' + target.port + '/fail');
            assert.ok(outbound, '应生成出站 Span');
            assert.ok(outbound.error.indexOf('503') !== -1, '出站 5xx 应记录错误');

            let networkError = null;
            try {
                await httpGet('http://127.0.0.1:1/network-fail');
            } catch (err) {
                networkError = err;
            }
            assert.ok(networkError, '请求应失败');
            const failed = findSpan(tracer.spans, 'GET 127.0.0.1:1/network-fail');
            assert.ok(failed, '网络错误也应生成 Span');
            assert.ok(failed.error.length > 0, '网络错误应记录错误信息');
        } finally {
            await close(target.server);
        }
    });

    await test('排除 /App/、/Trace/ 与 SDK 自身请求', async () => {
        const tracer = createAutoTracer();
        const target = await listen((req, res) => {
            if (req.url.indexOf('/internal-check') === 0) res.end(JSON.stringify({ code: 0, data: {} }));
            else res.end('ok');
        });

        try {
            const before = tracer.spans.length;
            await httpGet(target.url + '/App/Ping');
            await httpGet(target.url + '/Trace/Report?Token=x');
            await postJson(target.url + '/internal-check', {});

            const created = tracer.spans.slice(before);
            for (const span of created) {
                assert.ok(span.name.indexOf('/App/') === -1, '不应埋点 /App/ 接口：' + span.name);
                assert.ok(span.name.indexOf('/Trace/') === -1, '不应埋点 /Trace/ 接口：' + span.name);
            }
            assert.ok(
                !created.some((span) => span.name.indexOf('127.0.0.1') !== -1 && span.name.indexOf('/internal-check') !== -1),
                'SDK 内部请求不应生成出站 Span'
            );
        } finally {
            await close(target.server);
        }
    });

    await test('Express 中间件复用自动入口 Span 并升级为路由模板', async () => {
        const tracer = createAutoTracer();
        const middleware = createExpressMiddleware(tracer);
        const target = await listen((req, res) => {
            middleware(req, res, () => {
                req.route = { path: '/users/:id' };
                res.end('ok');
            });
        });

        try {
            await httpGet(target.url + '/users/123?x=1');

            const span = findSpan(tracer.spans, 'GET /users/:id');
            assert.ok(span, 'Span 名称应升级为路由模板');
            assert.strictEqual(tracer.spans.filter((item) => item.name === 'GET /users/123').length, 0, '不应产生未升级的重复 Span');
            assert.strictEqual(tracer.spans.filter((item) => item.name === 'GET /users/:id').length, 1, '同一请求只应有一个入口 Span');
        } finally {
            await close(target.server);
        }
    });

    await test('中间件先创建 Span 时 HTTP 包装不重复创建', async () => {
        const tracer = createAutoTracer();
        const existing = tracer.newSpan('GET /lazy');
        const before = tracer.spans.length;

        const req = { method: 'GET', url: '/lazy?x=1', headers: {} };
        req.__stardustSpan = existing;
        const res = new EventEmitter();
        res.statusCode = 200;

        let invoked = false;
        const listener = httpInstrument.wrapRequestListener((request, response) => {
            invoked = true;
            response.emit('finish');
        });
        listener(req, res);

        assert.strictEqual(invoked, true, '监听器应被执行');
        assert.strictEqual(tracer.spans.length, before + 1, '只应结算既有 Span，不应重复创建');
        assert.strictEqual(existing._finished, true, '既有 Span 应被结算');

        const lazyReq = { method: 'GET', url: '/lazy?x=1', headers: {} };
        const lazyRes = new EventEmitter();
        lazyRes.statusCode = 200;
        listener(lazyReq, lazyRes);

        assert.ok(lazyReq.__stardustSpan, '无既有 Span 时应创建并挂载到 req');
        assert.strictEqual(tracer.spans.length, before + 2, '应新增一个入口 Span');
        assert.strictEqual(tracer.spans[tracer.spans.length - 1].name, 'GET /lazy', '延迟创建 Span 名称应为 METHOD path');
    });

    await test('HTTP 包装幂等，不产生双份 Span', async () => {
        const first = createAutoTracer();
        const second = createAutoTracer();
        const target = await listen((req, res) => res.end('ok'));

        try {
            await httpGet(target.url + '/idempotent');

            const outboundName = 'GET 127.0.0.1:' + target.port + '/idempotent';
            assert.strictEqual(second.spans.filter((span) => span.name === outboundName).length, 1, '出站 Span 只应产生一次');
            assert.strictEqual(first.spans.filter((span) => span.name === outboundName).length, 0, '旧追踪器不应再次结算');
            assert.strictEqual(http.request.__stardustWrapped, true, 'http.request 应只包装一次');
        } finally {
            await close(target.server);
        }
    });

    await test('pg 自动埋点：名称、Tag、回调/Promise 与错误', async () => {
        const tracer = createAutoTracer();

        class FakeClient {
            query(sql, values, callback) {
                if (typeof values === 'function') {
                    callback = values;
                    values = null;
                }

                if (callback) {
                    if (String(sql).indexOf('boom') !== -1) return callback(new Error('pg boom'));
                    return callback(null, { rows: [] });
                }

                if (String(sql).indexOf('boom') !== -1) return Promise.reject(new Error('pg boom'));
                return Promise.resolve({ rows: [] });
            }
        }

        const pgModule = { Client: FakeClient };
        assert.strictEqual(dbInstrument.instrumentPg(tracer, pgModule), true, '应识别 pg 模块');
        dbInstrument.instrumentPg(tracer, pgModule);

        const client = new FakeClient();

        await new Promise((resolve) => client.query('select * from users where id = 1', (err) => {
            assert.strictEqual(err, null, '回调应无错误');
            resolve();
        }));

        const selectSpan = findSpan(tracer.spans, 'db:postgresql:select:users');
        assert.ok(selectSpan, 'SELECT 名称应为 db:postgresql:select:users');
        assert.strictEqual(selectSpan.tag, 'select * from users where id = 1', 'Tag 应为 SQL 文本');
        assert.strictEqual(tracer.spans.filter((span) => span.name === 'db:postgresql:select:users').length, 1, '重复安装不应重复计数');

        await client.query('insert into orders (id) values (1)');
        assert.ok(findSpan(tracer.spans, 'db:postgresql:insert:orders'), 'INSERT 应识别表名');

        let rejection = null;
        try {
            await client.query('select * from boom');
        } catch (err) {
            rejection = err;
        }
        assert.ok(rejection, 'Promise 应拒绝');
        const errorSpan = findSpan(tracer.spans, 'db:postgresql:select:boom');
        assert.ok(errorSpan, '错误请求也应生成 Span');
        assert.ok(errorSpan.error.indexOf('pg boom') !== -1, '应记录数据库错误');
    });

    await test('mysql2 自动埋点：query/execute 不重复计数', async () => {
        const tracer = createAutoTracer();

        class FakeConnection {
            query(sql, values, callback) {
                if (typeof values === 'function') callback = values;
                if (callback) return callback(null, []);
                return Promise.resolve([]);
            }

            execute(sql, values, callback) {
                return this.query(sql, values, callback);
            }
        }

        const mysqlModule = { Connection: FakeConnection };
        assert.strictEqual(dbInstrument.instrumentMysql2(tracer, mysqlModule), true, '应识别 mysql2 模块');

        const connection = new FakeConnection();
        await new Promise((resolve) => connection.query('update users set name = ?', ['a'], () => resolve()));
        await connection.execute('delete from sessions where id = 1');

        assert.strictEqual(findSpan(tracer.spans, 'db:mysql:update:users').tag, 'update users set name = ?', 'UPDATE Tag 应为 SQL 文本');
        assert.strictEqual(tracer.spans.filter((span) => span.name === 'db:mysql:delete:sessions').length, 1, 'execute 内部调用 query 不应重复计数');
    });

    await test('数据库模块缺失时静默跳过', async () => {
        const tracer = createAutoTracer();
        assert.doesNotThrow(() => dbInstrument.install(tracer), '缺少 pg/mysql2 时不应抛出异常');
    });

    await test('redis 自动埋点：命令名称、参数摘要与错误', async () => {
        const tracer = createAutoTracer();

        class FakeRedisClient {
            sendCommand(args) {
                return Promise.resolve('OK');
            }
        }

        const redisModule = { RedisClient: FakeRedisClient };
        assert.strictEqual(redisInstrument.instrumentRedis(tracer, redisModule), true, '应识别 redis 模块');
        redisInstrument.instrumentRedis(tracer, redisModule);

        const client = new FakeRedisClient();
        await client.sendCommand(['GET', 'user:1']);

        const getSpan = findSpan(tracer.spans, 'redis:GET');
        assert.ok(getSpan, '名称应为 redis:GET');
        assert.strictEqual(getSpan.tag, 'GET user:1', 'Tag 应包含命令与参数摘要');
        assert.strictEqual(tracer.spans.filter((span) => span.name === 'redis:GET').length, 1, '重复安装不应重复计数');

        class FakeIoRedis {
            sendCommand(command) {
                return Promise.reject(new Error('redis down'));
            }
        }

        assert.strictEqual(redisInstrument.instrumentIoredis(tracer, { Redis: FakeIoRedis }), true, '应识别 ioredis 模块');

        let rejection = null;
        try {
            await new FakeIoRedis().sendCommand({ name: 'get', args: ['user:2'] });
        } catch (err) {
            rejection = err;
        }
        assert.ok(rejection, 'Promise 应拒绝');

        const spans = tracer.spans.filter((span) => span.name === 'redis:GET');
        assert.strictEqual(spans.length, 2, '应记录 ioredis 调用');
        const failedSpan = spans[spans.length - 1];
        assert.strictEqual(failedSpan.tag, 'GET user:2', 'ioredis Tag 应包含参数摘要');
        assert.ok(failedSpan.error.indexOf('redis down') !== -1, '应记录 Redis 错误');
    });
}

const guard = setTimeout(() => {
    console.error('❌ 自动埋点测试超时');
    process.exit(1);
}, 30000);

main().then(() => {
    clearTimeout(guard);
    console.log('\n' + '='.repeat(50));
    console.log(`自动埋点测试完成: ${passed} 通过, ${failed} 失败`);
    console.log('='.repeat(50));
    if (failed > 0) process.exit(1);
}, (err) => {
    clearTimeout(guard);
    console.error(err && err.stack ? err.stack : err);
    process.exit(1);
});
