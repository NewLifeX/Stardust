/**
 * Koa 中间件
 * 自动追踪HTTP请求
 */

/**
 * 创建Koa中间件
 * @param {StardustTracer} tracer 追踪器实例
 * @returns {Function} Koa中间件函数
 */
function createKoaMiddleware(tracer) {
    return async function stardustMiddleware(ctx, next) {
        const name = `${ctx.method} ${ctx.path}`;
        const span = tracer.newSpan(name);
        span.setTag(`${ctx.method} ${ctx.url}`);

        try {
            await next();
            
            // 检查响应状态码
            if (ctx.status >= 400) {
                span.setError(`HTTP ${ctx.status}`);
            }
        } catch (err) {
            span.setError(err);
            throw err;
        } finally {
            span.finish();
        }
    };
}

module.exports = createKoaMiddleware;
