/**
 * Node.js 基础使用示例（新版配置对象 API）。
 */

'use strict';

const { StardustTracer } = require('../src');

const tracer = new StardustTracer({
    server: process.env.STARDUST_SERVER || 'http://localhost:6600',
    appId: process.env.STARDUST_APP_ID || 'NodeJsDemo',
    appName: 'Node.js 示例',
    secret: process.env.STARDUST_SECRET || '',
    version: '1.1.0',
    period: 60,
    heartbeatPeriod: 30,
    enableMeter: true,
    autoFlushOnExit: true,
    debug: true,
});

async function main() {
    await tracer.start();
    console.log('Tracer started');

    for (let i = 0; i < 10; i++) {
        await simulateOperation(i);
        await sleep(1000);
    }

    await tracer.stop();
    console.log('Tracer stopped');
}

// 模拟业务操作
async function simulateOperation(index) {
    const span = tracer.newSpan('业务操作');
    span.setTag(`index=${index}, timestamp=${Date.now()}`);

    try {
        await sleep(Math.random() * 100);

        if (Math.random() < 0.1) throw new Error('随机错误');

        console.log(`操作 ${index} 完成`);
    } catch (err) {
        span.setError(err);
        console.error(`操作 ${index} 失败:`, err.message);
    } finally {
        span.finish();
    }
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// 处理退出信号
process.on('SIGINT', async () => {
    console.log('\n收到退出信号，正在停止...');
    await tracer.stop();
    process.exit();
});

main().catch(async err => {
    console.error('Error:', err);
    await tracer.stop();
    process.exit(1);
});
