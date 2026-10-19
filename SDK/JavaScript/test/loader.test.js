/**
 * Stardust Browser Loader 测试。
 */

'use strict';

const assert = require('assert');
const loader = require('../src/loader');

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

function fakeScript(attributes, src) {
    return {
        src: src || '',
        getAttribute(name) {
            return Object.prototype.hasOwnProperty.call(attributes, name) ? attributes[name] : null;
        }
    };
}

function fakeDocument(meta) {
    return {
        querySelector(selector) {
            if (meta && selector.indexOf('stardust-traceparent') !== -1) {
                return { getAttribute: () => meta };
            }
            return null;
        }
    };
}

async function main() {
    await test('readConfig 读取 data-* 与 meta traceparent', () => {
        const script = fakeScript(
            {
                'data-stardust-app': 'MyWeb',
                'data-stardust-app-name': '我的网站',
                'data-stardust-server': 'http://star.example.com:6600',
                'data-stardust-config': '/stardust/browser-config',
                'data-stardust-token': '/stardust/token',
                'data-stardust-release': '1.2.3',
                'data-stardust-debug': 'true',
                'data-stardust-auto-instrument': 'false'
            },
            'https://cdn.example.com/stardust/loader.js'
        );
        const config = loader.readConfig(script, fakeDocument('00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01'));

        assert.strictEqual(config.appId, 'MyWeb');
        assert.strictEqual(config.appName, '我的网站');
        assert.strictEqual(config.server, 'http://star.example.com:6600');
        assert.strictEqual(config.configUrl, '/stardust/browser-config');
        assert.strictEqual(config.tokenUrl, '/stardust/token');
        assert.strictEqual(config.version, '1.2.3');
        assert.strictEqual(config.debug, true);
        assert.strictEqual(config.autoInstrument, false);
        assert.strictEqual(config.traceparent, '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01');

        const legacy = loader.readConfig(fakeScript({ 'data-app-id': 'Legacy' }), fakeDocument(null));
        assert.strictEqual(legacy.appId, 'Legacy', '应兼容旧 data-app-id 属性');

        const direct = loader.readConfig(
            fakeScript({
                'data-stardust-app': 'MyWeb',
                'data-stardust-token-value': 'short-token',
                'data-stardust-expire': '300'
            }),
            fakeDocument(null)
        );
        assert.strictEqual(direct.token, 'short-token', '应支持直接配置短期 Token');
        assert.strictEqual(direct.expire, '300', '应支持配置令牌有效期');
    });

    await test('resolveSrc 默认替换为同目录 browser.js', () => {
        assert.strictEqual(
            loader.resolveSrc({ scriptSrc: 'https://cdn.example.com/stardust/loader.js?v=1' }),
            'https://cdn.example.com/stardust/browser.js?v=1'
        );
        assert.strictEqual(loader.resolveSrc({ src: '/custom/browser.js' }), '/custom/browser.js');
    });

    await test('mergeConfig 后端配置覆盖 data-* 配置', () => {
        const merged = loader.mergeConfig(
            { appId: 'A', server: 'http://old', debug: true },
            { server: 'http://new', version: '2.0.0' }
        );
        assert.strictEqual(merged.appId, 'A');
        assert.strictEqual(merged.server, 'http://new');
        assert.strictEqual(merged.version, '2.0.0');
        assert.strictEqual(merged.debug, true);
    });

    await test('start 创建 tracer、注入初始 traceparent 与 tokenProvider', async () => {
        let captured = null;
        const fakeTracer = {
            started: false,
            start() { this.started = true; }
        };
        const root = {
            fetch() {
                return Promise.resolve({
                    ok: true,
                    json() { return Promise.resolve({ code: 0, data: { token: 'tk', expire: 7200 } }); }
                });
            },
            Stardust: {
                createTracer(options) {
                    captured = options;
                    return fakeTracer;
                }
            }
        };

        const tracer = loader.start(root, {
            appId: 'MyWeb',
            server: 'http://star.example.com:6600',
            version: '1.2.3',
            traceparent: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01',
            tokenUrl: '/stardust/token'
        });

        assert.strictEqual(tracer, fakeTracer);
        assert.strictEqual(root.Stardust.tracer, fakeTracer);
        assert.strictEqual(fakeTracer.started, true);
        assert.strictEqual(captured.appId, 'MyWeb');
        assert.strictEqual(captured.initialTraceparent, '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01');
        const token = await captured.tokenProvider();
        assert.deepStrictEqual(token, { token: 'tk', expire: 7200 });
    });

    await test('bootstrap 使用配置端点并只启动一次', async () => {
        let creates = 0;
        const fakeTracer = { start() {} };
        const root = {
            fetch(url) {
                assert.strictEqual(url, '/stardust/browser-config');
                return Promise.resolve({
                    ok: true,
                    json() {
                        return Promise.resolve({
                            code: 0,
                            data: { appId: 'MyWeb', server: 'http://star.example.com:6600', token: 'tk', expire: 7200 }
                        });
                    }
                });
            },
            Stardust: {
                createTracer() {
                    creates++;
                    return fakeTracer;
                }
            }
        };
        const doc = {
            currentScript: fakeScript({ 'data-config-url': '/stardust/browser-config' }),
            querySelector() { return null; }
        };

        const first = await loader.bootstrap(root, doc);
        const second = await loader.bootstrap(root, doc);

        assert.strictEqual(first, fakeTracer);
        assert.strictEqual(second, fakeTracer);
        assert.strictEqual(creates, 1);
    });

    await test('配置端点失败时回退到 data-stardust-*', async () => {
        let captured = null;
        const fakeTracer = { start() {} };
        const root = {
            fetch() { return Promise.reject(new Error('offline')); },
            Stardust: {
                createTracer(options) {
                    captured = options;
                    return fakeTracer;
                }
            }
        };
        const doc = {
            currentScript: fakeScript({
                'data-stardust-app': 'FallbackApp',
                'data-stardust-config': '/stardust/browser-config',
                'data-stardust-server': 'http://star.example.com:6600'
            }),
            querySelector() { return null; }
        };

        const tracer = await loader.bootstrap(root, doc);
        assert.strictEqual(tracer, fakeTracer);
        assert.strictEqual(captured.appId, 'FallbackApp');
        assert.strictEqual(captured.server, 'http://star.example.com:6600');
    });

    console.log('\n==================================================');
    console.log(`Loader 测试完成: ${passed} 通过, ${failed} 失败`);
    console.log('==================================================');

    if (failed > 0) process.exit(1);
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
