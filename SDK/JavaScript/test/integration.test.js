/**
 * Node.js 集成测试：使用本地 HTTP 服务器验证协议、gzip、令牌续期与停止刷新。
 */

'use strict';

const http = require('http');
const zlib = require('zlib');
const os = require('os');
const assert = require('assert');
const { StardustTracer } = require('../src');

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

function findRequest(requests, path) {
    const list = requests.filter((item) => item.path === path);
    return list.length > 0 ? list[list.length - 1] : null;
}

function findRequests(requests, path) {
    return requests.filter((item) => item.path === path);
}

/**
 * 创建星尘 Mock 服务器。
 * @returns {Promise<Object>} { server, state, url, close }
 */
function createMockServer() {
    return new Promise((resolve, reject) => {
        const state = {
            requests: [],
            loginCount: 0,
            reportFailOnce: false,
            reportHttp401Once: false,
            reportResponse: {
                Period: 17,
                MaxSamples: 2,
                MaxErrors: 5,
                Timeout: 3000,
                MaxTagLength: 512,
                RequestTagLength: 256,
                EnableMeter: true,
                Excludes: ['ignore-me'],
            },
        };

        const server = http.createServer((req, res) => {
            const chunks = [];
            req.on('data', (chunk) => chunks.push(chunk));
            req.on('end', () => {
                const raw = Buffer.concat(chunks);
                const contentType = String(req.headers['content-type'] || '');
                let json = null;
                if (contentType.indexOf('application/x-gzip') !== -1) {
                    try {
                        json = JSON.parse(zlib.gunzipSync(raw).toString('utf8'));
                    } catch (err) {
                        json = null;
                    }
                } else if (raw.length > 0) {
                    try {
                        json = JSON.parse(raw.toString('utf8'));
                    } catch (err) {
                        json = null;
                    }
                }

                let url = null;
                try {
                    url = new URL(req.url, 'http://127.0.0.1');
                } catch (err) {
                    url = { pathname: req.url, searchParams: new URLSearchParams() };
                }

                const record = {
                    method: req.method,
                    path: url.pathname,
                    query: {},
                    headers: req.headers,
                    contentType: contentType,
                    raw: raw,
                    json: json,
                };
                url.searchParams.forEach((value, key) => {
                    record.query[key] = value;
                });
                state.requests.push(record);

                const respond = function (body, status) {
                    const text = JSON.stringify(body);
                    res.writeHead(status || 200, {
                        'Content-Type': 'application/json',
                        'Content-Length': Buffer.byteLength(text),
                    });
                    res.end(text);
                };

                if (url.pathname === '/App/Login') {
                    state.loginCount++;
                    const appId = json && json.AppId ? json.AppId : 'Unknown';
                    respond({
                        code: 0,
                        data: {
                            Token: 'token-' + state.loginCount,
                            Expire: 7200,
                            Code: appId,
                            Secret: json && json.Secret ? json.Secret : '',
                        },
                    });
                    return;
                }

                if (url.pathname === '/App/Ping') {
                    respond({
                        code: 0,
                        data: {
                            Token: 'renewed-' + state.loginCount,
                            Expire: 7200,
                        },
                    });
                    return;
                }

                if (url.pathname === '/Trace/Report' || url.pathname === '/Trace/ReportRaw') {
                    if (state.reportFailOnce) {
                        state.reportFailOnce = false;
                        respond({ code: 401, message: 'token expired' });
                        return;
                    }
                    if (state.reportHttp401Once) {
                        state.reportHttp401Once = false;
                        respond({ code: 401, message: 'unauthorized' }, 401);
                        return;
                    }
                    respond({ code: 0, data: state.reportResponse });
                    return;
                }

                respond({ code: 404, message: 'not found' }, 404);
            });
        });

        server.once('error', reject);
        server.listen(0, '127.0.0.1', () => {
            const port = server.address().port;
            resolve({
                server,
                state,
                url: 'http://127.0.0.1:' + port,
                close: function () {
                    return new Promise((done) => {
                        try {
                            server.close(() => done());
                        } catch (err) {
                            done();
                        }
                        if (typeof server.closeAllConnections === 'function') {
                            try {
                                server.closeAllConnections();
                            } catch (err) {
                                // 忽略
                            }
                        }
                    });
                },
            });
        });
    });
}

function createTracer(harness, extra) {
    const options = Object.assign({
        server: harness.url,
        appId: 'IntegrationApp',
        appName: 'Integration App',
        secret: 's3cret',
        version: '9.9.9',
        period: 60,
        debug: false,
    }, extra || {});
    return new StardustTracer(options);
}

async function main() {
    await test('登录请求包含 Version / IP / ClientId', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness);
        try {
            await tracer.start();

            const login = findRequest(harness.state.requests, '/App/Login');
            assert.ok(login, '应收到登录请求');
            assert.strictEqual(login.json.Version, '9.9.9', '登录应携带 Version');
            assert.ok(typeof login.json.IP === 'string' && login.json.IP.length > 0, '登录应携带 IP');
            assert.strictEqual(login.json.ClientId, tracer.clientId, 'ClientId 应匹配');
            assert.strictEqual(login.json.AppName, 'Integration App', 'AppName 应匹配');
            assert.strictEqual(tracer._token, 'token-1', '应保存令牌');
            assert.strictEqual(findRequests(harness.state.requests, '/App/Ping').length, 0, 'start 不应立即发送心跳');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('心跳携带完整 AppInfo 性能字段', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness);
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            await tracer._ping();

            const ping = findRequest(harness.state.requests, '/App/Ping');
            assert.ok(ping, '应收到心跳请求');
            assert.strictEqual(ping.query.Token, 'token-1', '心跳应携带令牌');
            assert.strictEqual(ping.json.Id, process.pid, 'Id 应为进程号');
            assert.strictEqual(ping.json.Name, 'Integration App', 'Name 应匹配');
            assert.strictEqual(ping.json.Version, '9.9.9', '心跳应携带 Version');
            assert.strictEqual(ping.json.CommandLine, process.argv.join(' '), 'CommandLine 应匹配');
            assert.ok(typeof ping.json.UserName === 'string' && ping.json.UserName.length > 0, 'UserName 应存在');
            assert.strictEqual(ping.json.MachineName, os.hostname(), 'MachineName 应匹配');
            assert.ok(typeof ping.json.IP === 'string' && ping.json.IP.length > 0, 'IP 应存在');
            assert.ok(!isNaN(Date.parse(ping.json.StartTime)), 'StartTime 应为 ISO 时间');
            assert.ok(typeof ping.json.ProcessorTime === 'number' && ping.json.ProcessorTime >= 0, 'ProcessorTime 应有效');
            assert.ok(typeof ping.json.CpuUsage === 'number' && ping.json.CpuUsage >= 0, 'CpuUsage 应有效');
            assert.ok(ping.json.WorkingSet > 0, 'WorkingSet 应大于 0');
            assert.ok(ping.json.HeapSize > 0, 'HeapSize 应大于 0');
            assert.strictEqual(ping.json.Threads, 0, 'Threads 应为 0');
            assert.ok(typeof ping.json.Handles === 'number' && ping.json.Handles >= 0, 'Handles 应有效');
            assert.strictEqual(ping.json.Connections, 0, 'Connections 应为 0');
            assert.ok(typeof ping.json.GCCount === 'number' && ping.json.GCCount >= 0, 'GCCount 应有效');
            assert.strictEqual(ping.json.Listens, '', 'Listens 应为空');
            assert.ok(Math.abs(ping.json.Time - Date.now()) < 10000, 'Time 应接近当前时间');
            assert.strictEqual(tracer._token, 'renewed-1', '心跳应更新令牌');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('上报包含 Version / Builders 并应用 Period 等参数', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            const span = tracer.newSpan('manual.op');
            span.setTag('hello');
            span.finish();
            await tracer.flush();

            const report = findRequest(harness.state.requests, '/Trace/Report');
            assert.ok(report, '应收到普通上报请求');
            assert.strictEqual(report.query.Token, 'token-1', '上报应携带令牌');
            assert.strictEqual(report.json.Version, '9.9.9', '上报应携带 Version');
            assert.strictEqual(report.json.AppId, 'IntegrationApp', 'AppId 应匹配');
            assert.strictEqual(report.json.ClientId, tracer.clientId, 'ClientId 应匹配');
            assert.strictEqual(report.json.Builders.length, 1, '应包含 1 个构建器');
            assert.strictEqual(report.json.Builders[0].Name, 'manual.op', '构建器名称应匹配');
            assert.strictEqual(report.json.Builders[0].Total, 1, '构建器计数应匹配');
            assert.strictEqual(report.json.Builders[0].Samples[0].Tag, 'hello', '样本 Tag 应匹配');

            assert.strictEqual(tracer.period, 17, 'Period 应被应用');
            assert.strictEqual(tracer.maxSamples, 2, 'MaxSamples 应被应用');
            assert.strictEqual(tracer.maxErrors, 5, 'MaxErrors 应被应用');
            assert.strictEqual(tracer.timeout, 3000, 'Timeout 应被应用');
            assert.strictEqual(tracer.maxTagLength, 512, 'MaxTagLength 应被应用');
            assert.strictEqual(tracer.requestTagLength, 256, 'RequestTagLength 应被应用');
            assert.strictEqual(tracer.enableMeter, true, 'EnableMeter 应被应用');
            assert.ok(tracer.excludes.indexOf('ignore-me') >= 0, 'Excludes 应被应用');
            assert.ok(tracer._reportTimer, '上报定时器应已按新周期重建');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('enableMeter 时上报包含 Info 性能字段', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: true });
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            const span = tracer.newSpan('meter.op');
            span.finish();
            await tracer.flush();

            const reports = findRequests(harness.state.requests, '/Trace/Report')
                .concat(findRequests(harness.state.requests, '/Trace/ReportRaw'));
            assert.strictEqual(reports.length, 1, '应收到一次上报');
            const body = reports[0].json;
            assert.strictEqual(body.Version, '9.9.9', '上报应携带 Version');
            assert.ok(body.Info, '上报应包含 Info');
            assert.strictEqual(body.Info.Name, 'Integration App', 'Info.Name 应匹配');
            assert.strictEqual(body.Info.Version, '9.9.9', 'Info.Version 应匹配');
            assert.ok(body.Info.WorkingSet > 0, 'Info.WorkingSet 应大于 0');
            assert.ok(body.Info.HeapSize > 0, 'Info.HeapSize 应大于 0');
            assert.ok(Array.isArray(body.Builders) && body.Builders.length === 1, '应包含 Builders');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('大报文使用 ReportRaw + application/x-gzip', async () => {
        const harness = await createMockServer();
        harness.state.reportResponse.Period = 13;
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            const longTag = 'x'.repeat(400);
            for (let i = 0; i < 20; i++) {
                const span = tracer.newSpan('big.op.' + i);
                span.setTag(longTag);
                span.finish();
            }
            await tracer.flush();

            const report = findRequest(harness.state.requests, '/Trace/ReportRaw');
            assert.ok(report, '大报文应使用 ReportRaw');
            assert.strictEqual(report.contentType, 'application/x-gzip', 'Content-Type 应为 application/x-gzip');
            assert.strictEqual(report.query.Token, 'token-1', '应携带令牌');

            const decompressed = JSON.parse(zlib.gunzipSync(report.raw).toString('utf8'));
            assert.strictEqual(decompressed.Version, '9.9.9', '压缩负载应包含 Version');
            assert.strictEqual(decompressed.Builders.length, 20, '压缩负载应包含全部构建器');
            assert.strictEqual(decompressed.Builders[0].Samples[0].Tag.length, 400, '标签应完整');
            assert.strictEqual(tracer.period, 13, '压缩路径响应 Period 应被应用');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('业务码 401 触发重新登录并重试一次', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            harness.state.requests.length = 0;
            harness.state.reportFailOnce = true;

            const span = tracer.newSpan('auth.op');
            span.finish();
            await tracer.flush();

            const reports = findRequests(harness.state.requests, '/Trace/Report');
            const logins = findRequests(harness.state.requests, '/App/Login');
            assert.strictEqual(reports.length, 2, '401 后应重试一次上报');
            assert.strictEqual(logins.length, 1, '401 后应重新登录一次');
            assert.strictEqual(reports[0].query.Token, 'token-1', '首次上报使用旧令牌');
            assert.strictEqual(reports[1].query.Token, 'token-2', '重试应使用新令牌');
            assert.strictEqual(tracer._token, 'token-2', '追踪器应保存新令牌');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('HTTP 401 触发重新登录并重试一次', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            harness.state.requests.length = 0;
            harness.state.reportHttp401Once = true;

            const span = tracer.newSpan('http401.op');
            span.finish();
            await tracer.flush();

            const reports = findRequests(harness.state.requests, '/Trace/Report');
            const logins = findRequests(harness.state.requests, '/App/Login');
            assert.strictEqual(reports.length, 2, 'HTTP 401 后应重试一次上报');
            assert.strictEqual(logins.length, 1, 'HTTP 401 后应重新登录一次');
            assert.strictEqual(reports[1].query.Token, 'token-2', '重试应使用新令牌');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('令牌临近过期时主动重新登录', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            tracer._tokenExpire = Date.now() + 60 * 1000;
            harness.state.requests.length = 0;

            const span = tracer.newSpan('refresh.op');
            span.finish();
            await tracer.flush();

            const logins = findRequests(harness.state.requests, '/App/Login');
            assert.strictEqual(logins.length, 1, '应主动重新登录');
            const report = findRequest(harness.state.requests, '/Trace/Report');
            assert.strictEqual(report.query.Token, 'token-2', '上报应使用刷新后的令牌');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('tokenProvider 替代 AppId/Secret 登录', async () => {
        const harness = await createMockServer();
        let providerCalls = 0;
        const tracer = createTracer(harness, {
            secret: '',
            tokenProvider: async function () {
                providerCalls++;
                return { token: 'provider-token', expire: 3600 };
            },
            enableMeter: false,
        });
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            const span = tracer.newSpan('provider.op');
            span.finish();
            await tracer.flush();

            assert.strictEqual(providerCalls, 1, 'tokenProvider 应被调用一次');
            assert.strictEqual(findRequests(harness.state.requests, '/App/Login').length, 0, '不应调用登录接口');
            const report = findRequest(harness.state.requests, '/Trace/Report');
            assert.strictEqual(report.query.Token, 'provider-token', '上报应使用 provider 令牌');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });

    await test('stop/flush 停止定时器并上报剩余数据', async () => {
        const harness = await createMockServer();
        const tracer = createTracer(harness, { enableMeter: false });
        try {
            await tracer.start();
            harness.state.requests.length = 0;

            const span = tracer.newSpan('stop.op');
            span.finish();

            await tracer.stop();

            assert.strictEqual(tracer._running, false, '停止后 running 应为 false');
            assert.strictEqual(tracer._reportTimer, null, '上报定时器应停止');
            assert.strictEqual(tracer._pingTimer, null, '心跳定时器应停止');
            assert.strictEqual(findRequests(harness.state.requests, '/Trace/Report').length, 1, 'stop 应上报剩余数据');
            assert.strictEqual(tracer._builders.size, 0, '构建器应已清空');

            await tracer.flush();
            assert.strictEqual(findRequests(harness.state.requests, '/Trace/Report').length, 1, '重复 flush 不应重复上报');
        } finally {
            await tracer.stop();
            await harness.close();
        }
    });
}

const guard = setTimeout(() => {
    console.error('❌ 集成测试超时');
    process.exit(1);
}, 30000);

main().then(() => {
    clearTimeout(guard);
    console.log('\n' + '='.repeat(50));
    console.log(`集成测试完成: ${passed} 通过, ${failed} 失败`);
    console.log('='.repeat(50));
    if (failed > 0) process.exit(1);
}, (err) => {
    clearTimeout(guard);
    console.error(err && err.stack ? err.stack : err);
    process.exit(1);
});
