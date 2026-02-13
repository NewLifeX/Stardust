/**
 * Express.js 应用示例
 */

const express = require('express');
const { StardustTracer } = require('../src');
const createExpressMiddleware = require('../src/middleware/express');

const app = express();
const port = process.env.PORT || 3000;

// 创建追踪器
const tracer = new StardustTracer(
    process.env.STARDUST_SERVER || 'http://localhost:6600',
    process.env.STARDUST_APP_ID || 'ExpressDemo',
    process.env.STARDUST_SECRET || ''
);

// 启动追踪器
tracer.start().then(() => {
    console.log('Stardust tracer started');
});

// 使用中间件
app.use(createExpressMiddleware(tracer));

// 路由
app.get('/', (req, res) => {
    res.send('Hello from Express + Stardust!');
});

app.get('/api/users', async (req, res) => {
    // 手动埋点示例
    const span = tracer.newSpan('查询用户列表');
    span.setTag('method=GET, path=/api/users');

    try {
        // 模拟数据库查询
        await new Promise(resolve => setTimeout(resolve, 50));
        
        const users = [
            { id: 1, name: 'Alice' },
            { id: 2, name: 'Bob' }
        ];
        
        span.finish();
        res.json(users);
    } catch (err) {
        span.setError(err);
        span.finish();
        res.status(500).json({ error: err.message });
    }
});

app.get('/api/error', (req, res) => {
    // 测试错误追踪
    res.status(500).json({ error: 'Simulated error' });
});

app.post('/api/data', express.json(), (req, res) => {
    const span = tracer.newSpan('处理数据');
    span.setTag(`body=${JSON.stringify(req.body)}`);
    
    try {
        // 处理数据
        res.json({ success: true, data: req.body });
    } catch (err) {
        span.setError(err);
        res.status(500).json({ error: err.message });
    } finally {
        span.finish();
    }
});

// 启动服务器
const server = app.listen(port, () => {
    console.log(`Express server running on http://localhost:${port}`);
    console.log('Try these endpoints:');
    console.log(`  GET  http://localhost:${port}/`);
    console.log(`  GET  http://localhost:${port}/api/users`);
    console.log(`  GET  http://localhost:${port}/api/error`);
    console.log(`  POST http://localhost:${port}/api/data`);
});

// 优雅退出
process.on('SIGINT', () => {
    console.log('\nShutting down gracefully...');
    server.close(() => {
        tracer.stop();
        console.log('Server closed');
        process.exit();
    });
});
