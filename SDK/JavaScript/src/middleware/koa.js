/**
 * Koa 中间件：自动追踪 HTTP 请求。
 */

'use strict';

const { parseTraceparent, runWithSpan } = require('../trace-context');

/**
 * 是否需要忽略此路径。
 * @param {String} path 请求路径
 * @returns {Boolean} 是否忽略
 */
function isIgnoredPath(path) {
    const text = String(path || '');
    return /^\/(App|Trace)(\/|$)/i.test(text) || text.indexOf('/App/') !== -1 || text.indexOf('/Trace/') !== -1;
}

/**
 * 读取请求头（不区分大小写）。
 * @param {Object} req 请求
 * @param {String} name 头名
 * @returns {String} 头值
 */
function getHeader(req, name) {
    const headers = req && req.headers;
    if (!headers) return '';
    const lower = name.toLowerCase();
    let value = headers[lower];
    if (value === undefined) value = headers[name];
    if (value === undefined) {
        for (const key of Object.keys(headers)) {
            if (key.toLowerCase() === lower) {
                value = headers[key];
                break;
            }
        }
    }
    if (Array.isArray(value)) return value.length > 0 ? String(value[0]) : '';
    return value === undefined || value === null ? '' : String(value);
}

/**
 * 取路由模板路径。
 * @param {Object} ctx Koa 上下文
 * @param {String} fallback 回退路径
 * @returns {String} 路径
 */
function getRoutePath(ctx, fallback) {
    try {
        let route = ctx ? (ctx.routePath || ctx._matchedRoute) : null;
        if (!route && ctx && ctx.route) route = ctx.route.path;
        if (Array.isArray(route) && route.length > 0) route = route[0];
        if (!route || typeof route !== 'string') return fallback;
        return route.charAt(0) === '/' ? route : '/' + route;
    } catch (err) {
        return fallback;
    }
}

/**
 * 创建 Koa 中间件。
 * @param {Object} tracer 追踪器实例
 * @returns {Function} Koa 中间件
 */
function createKoaMiddleware(tracer) {
    return async function stardustMiddleware(ctx, next) {
        let path = '/';
        try {
            path = (ctx && (ctx.path || (ctx.url ? String(ctx.url).split('?')[0] : ''))) || '/';
            if (isIgnoredPath(path)) return next();
        } catch (err) {
            path = '/';
        }

        const method = ctx && ctx.method ? String(ctx.method).toUpperCase() : 'GET';
        const tag = method + ' ' + ((ctx && (ctx.originalUrl || ctx.url)) || path);
        let span = null;
        try {
            // 复用 HTTP 自动入口创建的 Span，避免重复
            span = ctx && ctx.__stardustSpan ? ctx.__stardustSpan : null;
            if (!span && ctx && ctx.req && ctx.req.__stardustSpan) span = ctx.req.__stardustSpan;
            if (span) {
                span.setTag(tag);
            } else {
                const inbound = parseTraceparent(getHeader(ctx && ctx.req, 'traceparent'));
                if (inbound) span = tracer.newSpan(method + ' ' + getRoutePath(ctx, path), { parentId: inbound.spanId, traceId: inbound.traceId });
                else span = tracer.newSpan(method + ' ' + getRoutePath(ctx, path));
                span.setTag(tag);
                if (ctx) ctx.__stardustSpan = span;
            }
        } catch (err) {
            span = null;
        }

        let finished = false;
        try {
            if (span) {
                await runWithSpan(span, function () {
                    return next();
                });
            } else {
                await next();
            }

            if (span && ctx && ctx.status >= 400) span.setError('HTTP ' + ctx.status);
        } catch (err) {
            if (span) {
                try {
                    span.setError(err);
                } catch (ignore) {
                    // 忽略
                }
            }
            throw err;
        } finally {
            if (span && !finished) {
                finished = true;
                try {
                    span.name = method + ' ' + getRoutePath(ctx, path);
                    span.finish();
                } catch (err) {
                    // 监控异常不得影响业务
                }
            }
        }
    };
}

module.exports = createKoaMiddleware;
module.exports.isIgnoredPath = isIgnoredPath;
module.exports.getRoutePath = getRoutePath;
