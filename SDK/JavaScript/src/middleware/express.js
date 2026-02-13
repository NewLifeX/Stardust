/**
 * Express.js 中间件
 * 自动追踪HTTP请求
 */

/**
 * 创建Express中间件
 * @param {StardustTracer} tracer 追踪器实例
 * @returns {Function} Express中间件函数
 */
function createExpressMiddleware(tracer) {
    return function stardustMiddleware(req, res, next) {
        const name = `${req.method} ${req.path}`;
        const span = tracer.newSpan(name);
        span.setTag(`${req.method} ${req.originalUrl || req.url}`);

        // 在请求开始时创建 span，内部会记录开始时间

        // 拦截响应结束事件
        const originalEnd = res.end;
        res.end = function (...args) {
            // 检查响应状态码
            if (res.statusCode >= 400) {
                span.setError(`HTTP ${res.statusCode}`);
            }
            
            span.finish();
            originalEnd.apply(res, args);
        };

        next();
    };
}

module.exports = createExpressMiddleware;
