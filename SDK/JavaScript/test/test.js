/**
 * 基础测试文件
 */

const { StardustTracer, Span, SpanBuilder } = require('../src');

// 测试计数器
let passed = 0;
let failed = 0;

function test(name, fn) {
    try {
        fn();
        console.log(`✅ ${name}`);
        passed++;
    } catch (err) {
        console.error(`❌ ${name}`);
        console.error(`   ${err.message}`);
        failed++;
    }
}

function assert(condition, message) {
    if (!condition) {
        throw new Error(message || 'Assertion failed');
    }
}

function assertEqual(actual, expected, message) {
    if (actual !== expected) {
        throw new Error(message || `Expected ${expected}, got ${actual}`);
    }
}

console.log('运行 Stardust JavaScript SDK 测试...\n');

// 测试 Span 类
test('Span 创建', () => {
    const mockTracer = { _finishSpan: () => {} };
    const span = new Span('test-span', mockTracer);
    
    assert(span.id, 'Span 应该有 ID');
    assert(span.traceId, 'Span 应该有 TraceId');
    assertEqual(span.name, 'test-span', 'Span 名称应该匹配');
    assertEqual(span.parentId, '', 'Span 默认无父 ID');
    assert(span.startTime > 0, 'Span 应该有开始时间');
    assertEqual(span.endTime, 0, 'Span 初始结束时间为 0');
});

test('Span setTag', () => {
    const mockTracer = { _finishSpan: () => {} };
    const span = new Span('test-span', mockTracer);
    
    span.setTag('test-tag');
    assertEqual(span.tag, 'test-tag', 'Tag 应该被设置');
});

test('Span setError', () => {
    const mockTracer = { _finishSpan: () => {} };
    const span = new Span('test-span', mockTracer);
    
    span.setError('test error');
    assertEqual(span.error, 'test error', '错误消息应该被设置');
    
    const err = new Error('Test Error');
    span.setError(err);
    assert(span.error.includes('Error: Test Error'), '错误对象应该被格式化');
});

test('Span toJSON', () => {
    const mockTracer = { _finishSpan: () => {} };
    const span = new Span('test-span', mockTracer);
    span.setTag('tag1');
    span.setError('error1');
    
    const json = span.toJSON();
    assertEqual(json.Id, span.id, 'JSON ID 应该匹配');
    assertEqual(json.Tag, 'tag1', 'JSON Tag 应该匹配');
    assertEqual(json.Error, 'error1', 'JSON Error 应该匹配');
});

// 测试 SpanBuilder 类
test('SpanBuilder 创建', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    
    assertEqual(builder.name, 'test-builder', 'Builder 名称应该匹配');
    assertEqual(builder.total, 0, 'Builder 初始计数为 0');
    assertEqual(builder.errors, 0, 'Builder 初始错误数为 0');
    assertEqual(builder._maxSamples, 5, 'Builder 最大样本数应该匹配');
    assertEqual(builder._maxErrors, 10, 'Builder 最大错误样本数应该匹配');
});

test('SpanBuilder addSpan', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    const mockTracer = { _finishSpan: () => {} };
    
    const span1 = new Span('test-span', mockTracer);
    span1.endTime = span1.startTime + 100;
    builder.addSpan(span1);
    
    assertEqual(builder.total, 1, 'Builder 应该计数 1 次');
    assertEqual(builder.cost, 100, 'Builder 总耗时应该为 100');
    assertEqual(builder.maxCost, 100, 'Builder 最大耗时应该为 100');
    assertEqual(builder.minCost, 100, 'Builder 最小耗时应该为 100');
    assertEqual(builder.samples.length, 1, 'Builder 应该有 1 个样本');
});

test('SpanBuilder 错误统计', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    const mockTracer = { _finishSpan: () => {} };
    
    const span1 = new Span('test-span', mockTracer);
    span1.endTime = span1.startTime + 100;
    span1.setError('test error');
    builder.addSpan(span1);
    
    assertEqual(builder.errors, 1, 'Builder 应该计数 1 个错误');
    assertEqual(builder.errorSamples.length, 1, 'Builder 应该有 1 个错误样本');
});

test('SpanBuilder toJSON', () => {
    const builder = new SpanBuilder('test-builder', 5, 10);
    const mockTracer = { _finishSpan: () => {} };
    
    const span1 = new Span('test-span', mockTracer);
    span1.endTime = span1.startTime + 100;
    builder.addSpan(span1);
    
    const json = builder.toJSON();
    assertEqual(json.Name, 'test-builder', 'JSON Name 应该匹配');
    assertEqual(json.Total, 1, 'JSON Total 应该匹配');
    assert(Array.isArray(json.Samples), 'JSON Samples 应该是数组');
    assertEqual(json.Samples.length, 1, 'JSON Samples 应该有 1 个元素');
});

// 测试 StardustTracer 类
test('StardustTracer 创建', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app', 'test-secret');
    
    assertEqual(tracer.server, 'http://localhost:6600', 'Server 地址应该匹配');
    assertEqual(tracer.appId, 'test-app', 'AppId 应该匹配');
    assertEqual(tracer.secret, 'test-secret', 'Secret 应该匹配');
    assert(tracer.clientId.includes('@'), 'ClientId 应该包含 @');
    assertEqual(tracer.period, 60, '默认上报周期应该为 60 秒');
});

test('StardustTracer newSpan', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    const span = tracer.newSpan('test-operation');
    
    assert(span instanceof Span, 'newSpan 应该返回 Span 实例');
    assertEqual(span.name, 'test-operation', 'Span 名称应该匹配');
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
    
    const span1 = tracer.newSpan('/Trace/Report');
    span1.endTime = span1.startTime + 100;
    tracer._finishSpan(span1);
    
    assert(!tracer._builders.has('/Trace/Report'), '特殊路径应该被排除');
});

test('StardustTracer excludes 列表', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    tracer.excludes = ['health', 'ping'];
    
    const span1 = tracer.newSpan('GET /health');
    span1.endTime = span1.startTime + 100;
    tracer._finishSpan(span1);
    
    assert(!tracer._builders.has('GET /health'), '排除列表中的路径应该被过滤');
});

test('StardustTracer Tag 长度限制', () => {
    const tracer = new StardustTracer('http://localhost:6600', 'test-app');
    tracer.maxTagLength = 10;
    
    const span = tracer.newSpan('test-operation');
    span.setTag('12345678901234567890');
    span.endTime = span.startTime + 100;
    tracer._finishSpan(span);
    
    const builder = tracer._builders.get('test-operation');
    const sample = builder.samples[0];
    assert(sample.tag.length <= 10, 'Tag 长度应该被限制');
});

// 打印测试结果
console.log('\n' + '='.repeat(50));
console.log(`测试完成: ${passed} 通过, ${failed} 失败`);
console.log('='.repeat(50));

if (failed > 0) {
    process.exit(1);
}
