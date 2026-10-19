/**
 * Express.js 应用示例（新版配置对象 API）。
 */

'use strict';

const express = require('express');
const { StardustTracer } = require('../src');
const createExpressMiddleware = require('../src/middleware/express');

const app = express();
const port = process.env.PORT || 3000;

const tracer = new StardustTracer({
    server: process.env.STARDUST_SERVER || 'http://localhost:6600',
    appId: process.env.STARDUST_APP_ID || 'ExpressDemo',
    appName: 'Express 示例',
    secret: process.env.STARDUST_SECRET || '',
    version: '1.1.0',
    period: 60,
    heartbeatPeriod: 30,
    enableMeter: true,
    debug: true,
});

tracer.start().then(() => {
    console.log('Stardust tracer started');
}).catch(err => {
    console.error('Stardust tracer failed:', err.message);
});

// 使用中间件（自动追踪所有 HTTP 请求）
app.use(createExpressMiddleware(tracer));

app.get('/', (req, res) => {
    res.send('Hello from Express + Stardust!');
});

app.get('/api/users', async (req, res) => {
    const span = tracer.newSpan('查询用户列表');
    span.setTag('method=GET, path=/api/users');

    try {
        await new Promise(resolve => setTimeout(resolve, 50));

        const users = [
            { id: 1, name: 'Alice' },
            { id: 2, name: 'Bob' },
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
    res.status(500).json({ error: 'Simulated error' });
});

app.post('/api/data', express.json(), (req, res) => {
    const span = tracer.newSpan('处理数据');
    span.setTag(`body=${JSON.stringify(req.body)}`);

    try {
        res.json({ success: true, data: req.body });
    } catch (err) {
        span.setError(err);
        res.status(500).json({ error: err.message });
    } finally {
        span.finish();
    }
});

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
    server.close(async () => {
        await tracer.stop();
        console.log('Server closed');
        process.exit();
    });
});
