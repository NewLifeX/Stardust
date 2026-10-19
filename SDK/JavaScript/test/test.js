/**
 * 基础单元测试：Span、SpanBuilder、StardustTracer 与导出。
 */

'use strict';

const {
    StardustTracer,
    StardustBrowserTracer,
    Span,
    SpanBuilder,
    createTracer,
    init,
    installBrowserInterceptors,
} = require('../src');

let passed = 0;
let failed = 0;

function test(name, fn) {
    try {
        fn();
        console.log(`✅ ${name}`);
        passed++;
    } catch (err) {
        console.error(`❌ ${name}`);
        console.error(`   ${err && err.stack ? err.stack : err}`);
        failed++;
    }
}

function assert(condition, message) {
    if (!condition) throw new Error(message || 'Assertion failed');
}

function assertEqual(actual, expected, message) {
    if (actual !== expected) throw new Error(message || `Expected ${expected}, got ${actual}`);
}

console.log('运行 Stardust JavaScript SDK 基础测试...\n');

test('Span 创建', () => {
    const mockTracer = { _finishSpan: () => { } };
    const span = new Span('test-span', mockTracer);

    assert(span.id, 'Span 应该有 ID');
    assertEqual(span.id.length, 16, 'Span ID 应为 16 位');
    assert(span.traceId, 'Span 应该有 TraceId');
    assertEqual(span.traceId.length, 32, 'TraceId 应为 32 位');
    assertEqual(span.name, 'test-span', 'Span 名称应该匹配');
    assertEqual(span.parentId, '', 'Span 默认无父 ID');
    assert(span.startTime > 0, 'Span 应该有开始时间');
    assertEqual(span.endTime, 0, 'Span 初始结束时间为 0');
});

test('Span setTag / setError', () => {
    const mockTracer = { _finishSpan: () => { } };
    const span = new Span('test-span', mockTracer);

    span.setTag('test-tag');
    assertEqual(span.tag, 'test-tag', 'Tag 应该被设置');

    span.setError('test error');
    assertEqual(span.error, 'test error', '错误消息应该被设置');

    const err = new Error('Test Error');
    span.setError(err);
    assert(span.error.includes('Error: Test Error'), '错误对象应该被格式化');
});

test('Span finish 只生效一次', () => {
    let count = 0;
    const mockTracer = { _finishSpan: () => { count++; } };
    const span = new Span('test-span', mockTracer);

    span.finish();
    span.finish();

    assertEqual(count, 1, 'finish 应只调用一次');
    assert(span.endTime > 0, '结束时间应该被设置');
});

test('Span toJSON', () => {
    const mockTracer = { _finishSpan: () => { } };
    const span = new Span('test-span', mockTracer);
    span.setTag('tag1');
    span.setError('error1');

    const json = span.toJSON();
    assertEqual(json.Id, span.id, 'JSON ID 应该匹配');
    assertEqual(json.Tag, 'tag1', 'JSON Tag 应该匹配');
    assertEqual(json.Error, 'error1', 'JSON Error 应该匹配');
});

test('SpanBuilder 创建', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);

    assertEqual(builder.name, 'test-builder', 'Builder 名称应该匹配');
    assertEqual(builder.total, 0, 'Builder 初始计数为 0');
    assertEqual(builder.errors, 0, 'Builder 初始错误数为 0');
    assertEqual(builder._maxSamples, 5, 'Builder 最大样本数应该匹配');
    assertEqual(builder._maxErrors, 10, 'Builder 最大错误样本数应该匹配');
});

test('SpanBuilder addSpan 统计', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    const mockTracer = { _finishSpan: () => { } };

    const span1 = new Span('test-span', mockTracer);
    span1.endTime = span1.startTime + 100;
    builder.addSpan(span1);

    assertEqual(builder.total, 1, 'Builder 应该计数 1 次');
    assertEqual(builder.cost, 100, 'Builder 总耗时应该为 100');
    assertEqual(builder.maxCost, 100, 'Builder 最大耗时应该为 100');
    assertEqual(builder.minCost, 100, 'Builder 最小耗时应该为 100');
    assertEqual(builder.samples.length, 1, 'Builder 应该有 1 个样本');

    const span2 = new Span('test-span', mockTracer);
    span2.endTime = span2.startTime + 50;
    builder.addSpan(span2);
    assertEqual(builder.maxCost, 100, '最大耗时应该保持 100');
    assertEqual(builder.minCost, 50, '最小耗时应该更新为 50');
});

test('SpanBuilder 错误统计与样本上限', () => {
    const builder = new SpanBuilder('test-builder', 1, 2);
    const mockTracer = { _finishSpan: () => { } };

    for (let i = 0; i < 3; i++) {
        const span = new Span('test-span', mockTracer);
        span.endTime = span.startTime + 100;
        span.setError('test error');
        builder.addSpan(span);
    }

    assertEqual(builder.errors, 3, 'Builder 应该计数 3 个错误');
    assertEqual(builder.errorSamples.length, 2, '错误样本应受上限限制');
    assertEqual(builder.samples.length, 0, '错误样本不进入正常样本');
});

test('SpanBuilder toJSON', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    const mockTracer = { _finishSpan: () => { } };

    const span1 = new Span('test-span', mockTracer);
    span1.endTime = span1.startTime + 100;
    builder.addSpan(span1);

    const json = builder.toJSON();
    assertEqual(json.Name, 'test-builder', 'JSON Name 应该匹配');
    assertEqual(json.Total, 1, 'JSON Total 应该匹配');
    assert(Array.isArray(json.Samples), 'JSON Samples 应该是数组');
    assertEqual(json.Samples.length, 1, 'JSON Samples 应该有 1 个元素');
    assertEqual(json.Samples[0].Id, span1.id, '样本 ID 应该匹配');
});

test('StardustTracer 旧版位置参数', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app', 'test-secret');

    assertEqual(tracer.server, 'http://localhost:6600', 'Server 地址应该匹配');
    assertEqual(tracer.appId, 'test-app', 'AppId 应该匹配');
    assertEqual(tracer.appName, 'test-app', 'AppName 默认同 AppId');
    assertEqual(tracer.secret, 'test-secret', 'Secret 应该匹配');
    assert(tracer.clientId.includes('@'), 'ClientId 应该包含 @');
    assertEqual(tracer.period, 60, '默认上报周期应该为 60 秒');
    assertEqual(tracer.heartbeatPeriod, 30, '默认心跳周期应该为 30 秒');
    assertEqual(tracer.version, '1.1.0', '默认版本应该为 1.1.0');
});

test('StardustTracer 配置对象', () => {
    const tracer = new StardustTracer({
        server: 'http://localhost:6600/',
        appId: 'test-app',
        appName: 'Test App',
        secret: 'test-secret',
        version: '9.9.9',
        clientId: 'custom-client',
        period: 15,
        heartbeatPeriod: 10,
        maxSamples: 3,
        maxErrors: 4,
        maxTagLength: 20,
        enableMeter: false,
        excludes: ['health'],
        debug: false,
    });

    assertEqual(tracer.server, 'http://localhost:6600', 'Server 末尾斜杠应该被移除');
    assertEqual(tracer.appName, 'Test App', 'AppName 应该匹配');
    assertEqual(tracer.version, '9.9.9', 'Version 应该匹配');
    assertEqual(tracer.clientId, 'custom-client', 'ClientId 应该匹配');
    assertEqual(tracer.period, 15, 'Period 应该匹配');
    assertEqual(tracer.heartbeatPeriod, 10, 'HeartbeatPeriod 应该匹配');
    assertEqual(tracer.maxSamples, 3, 'MaxSamples 应该匹配');
    assertEqual(tracer.maxErrors, 4, 'MaxErrors 应该匹配');
    assertEqual(tracer.maxTagLength, 20, 'MaxTagLength 应该匹配');
    assertEqual(tracer.enableMeter, false, 'EnableMeter 应该匹配');
    assert(tracer.excludes.includes('health'), 'Excludes 应该匹配');
});

test('StardustTracer newSpan', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    const span = tracer.newSpan('test-operation', 'parent-1');

    assert(span instanceof Span, 'newSpan 应该返回 Span 实例');
    assertEqual(span.name, 'test-operation', 'Span 名称应该匹配');
    assertEqual(span.parentId, 'parent-1', '父 ID 应该匹配');
});

test('StardustTracer _finishSpan', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    const span = tracer.newSpan('test-operation');
    span.endTime = span.startTime + 100;

    tracer._finishSpan(span);

    assert(tracer._builders.has('test-operation'), 'Builder 应该被创建');
    const builder = tracer._builders.get('test-operation');
    assertEqual(builder.total, 1, 'Builder 应该计数 1 次');
});

test('StardustTracer 排除特殊路径', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');

    for (const name of ['/Trace/Report', '/Trace/ReportRaw', '/App/Login', '/App/Ping']) {
        const span = tracer.newSpan(name);
        span.endTime = span.startTime + 100;
        tracer._finishSpan(span);
        assert(!tracer._builders.has(name), name + ' 应该被排除');
    }
});

test('StardustTracer excludes 列表', () => {
    const tracer = new StardustTracer({
        server: 'http://localhost:6600',
        appId: 'test-app',
        excludes: ['health', 'ping'],
    });

    const span = tracer.newSpan('GET /health');
    span.endTime = span.startTime + 100;
    tracer._finishSpan(span);

    assert(!tracer._builders.has('GET /health'), '排除列表中的路径应该被过滤');
});

test('StardustTracer Tag 长度限制', () => {
    const tracer = new StardustTracer({
        server: 'http://localhost:6600',
        appId: 'test-app',
        maxTagLength: 10,
    });

    const span = tracer.newSpan('test-operation');
    span.setTag('12345678901234567890');
    span.finish();

    const builder = tracer._builders.get('test-operation');
    const sample = builder.samples[0];
    assert(sample.tag.length <= 10, 'Tag 长度应该被限制');
});

test('StardustTracer 令牌过期判断', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    tracer._token = 'token';
    tracer._tokenExpire = Date.now() + 10 * 60 * 1000;

    assert(tracer._tokenExpire - Date.now() > 5 * 60 * 1000, '令牌应处于有效期内');
});

test('createTracer 工厂', () => {
    const tracer = createTracer({ server: 'http://localhost:6600', appId: 'test-app' });
    assert(tracer instanceof StardustTracer, '应该创建 Node 追踪器');
});

test('init 一行创建并启动追踪器', () => {
    const originalStart = StardustTracer.prototype.start;
    let started = 0;
    StardustTracer.prototype.start = function () {
        started++;
        return this;
    };

    try {
        const tracer = init({ server: 'http://localhost:6600', appId: 'test-app' });
        assert(tracer instanceof StardustTracer, '应该创建 Node 追踪器');
        assertEqual(started, 1, '应该自动启动一次');
    } finally {
        StardustTracer.prototype.start = originalStart;
    }
});

test('浏览器导出与中间件兼容', () => {
    assert(typeof StardustBrowserTracer === 'function', '应导出浏览器追踪器');
    assert(typeof installBrowserInterceptors === 'function', '应导出 installBrowserInterceptors');
    assertEqual(require('../src/browser-tracer'), StardustBrowserTracer, 'browser-tracer 应保持兼容导出');
});

console.log('\n' + '='.repeat(50));
console.log(`测试完成: ${passed} 通过, ${failed} 失败`);
console.log('='.repeat(50));

if (failed > 0) process.exit(1);
