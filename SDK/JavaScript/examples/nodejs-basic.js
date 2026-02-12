/**
 * Node.js 基础使用示例
 */

const { StardustTracer } = require('../src');

// 创建追踪器实例
const tracer = new StardustTracer(
    process.env.STARDUST_SERVER || 'http://localhost:6600',
    process.env.STARDUST_APP_ID || 'NodeJsDemo',
    process.env.STARDUST_SECRET || ''
);

// 启动追踪器
async function main() {
    await tracer.start();
    console.log('Tracer started');

    // 模拟一些操作
    for (let i = 0; i < 10; i++) {
        await simulateOperation(i);
        await sleep(1000);
    }

    // 停止追踪器
    tracer.stop();
    console.log('Tracer stopped');
}

// 模拟业务操作
async function simulateOperation(index) {
    const span = tracer.newSpan('业务操作');
    span.setTag(`index=${index}, timestamp=${Date.now()}`);

    try {
        // 模拟一些处理时间
        await sleep(Math.random() * 100);

        // 模拟偶尔出错
        if (Math.random() < 0.1) {
            throw new Error('随机错误');
        }

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
process.on('SIGINT', () => {
    console.log('\n收到退出信号，正在停止...');
    tracer.stop();
    process.exit();
});

// 运行
main().catch(err => {
    console.error('Error:', err);
    tracer.stop();
    process.exit(1);
});
