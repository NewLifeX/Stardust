/**
 * Express.js 中间件：自动追踪 HTTP 请求。
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
 * 取路由模板路径，优先使用 Express 匹配到的路由。
 * @param {Object} req 请求
 * @param {String} fallback 回退路径
 * @returns {String} 路径
 */
function getRoutePath(req, fallback) {
    try {
        const route = req && req.route ? req.route.path : null;
        let path = null;
        if (Array.isArray(route) && route.length > 0) path = route[0];
        else if (typeof route === 'string') path = route;
        if (!path || typeof path !== 'string') return fallback;

        if (path.charAt(0) !== '/') path = '/' + path;
        const baseUrl = req.baseUrl ? String(req.baseUrl).replace(/\/+$/, '') : '';
        if (baseUrl && path.indexOf(baseUrl + '/') !== 0) path = baseUrl + path;
        return path;
    } catch (err) {
        return fallback;
    }
}

/**
 * 创建 Express 中间件。
 * @param {Object} tracer 追踪器实例
 * @returns {Function} Express 中间件
 */
function createExpressMiddleware(tracer) {
    return function stardustMiddleware(req, res, next) {
        let path = '/';
        try {
            path = (req && (req.path || (req.url ? String(req.url).split('?')[0] : ''))) || '/';
            if (isIgnoredPath(path)) return next();
        } catch (err) {
            path = '/';
        }

        const method = req && req.method ? String(req.method).toUpperCase() : 'GET';
        const tag = method + ' ' + ((req && (req.originalUrl || req.url)) || path);
        let span = null;

        try {
            // 复用 HTTP 自动入口创建的 Span，避免重复
            span = req && req.__stardustSpan ? req.__stardustSpan : null;
            if (span) {
                span.setTag(tag);
            } else {
                const inbound = parseTraceparent(getHeader(req, 'traceparent'));
                if (inbound) span = tracer.newSpan(method + ' ' + getRoutePath(req, path), { parentId: inbound.spanId, traceId: inbound.traceId });
                else span = tracer.newSpan(method + ' ' + getRoutePath(req, path));
                span.setTag(tag);
                if (req) req.__stardustSpan = span;
            }
        } catch (err) {
            span = null;
        }

        let finished = false;
        const finish = function () {
            if (finished) return;
            finished = true;
            if (!span) return;

            try {
                const routePath = getRoutePath(req, path);
                span.name = method + ' ' + routePath;
                if (res && res.statusCode >= 400) span.setError('HTTP ' + res.statusCode);
                span.finish();
            } catch (err) {
                // 监控异常不得影响业务
            }
        };

        try {
            if (res && typeof res.end === 'function') {
                const originalEnd = res.end;
                res.end = function () {
                    finish();
                    return originalEnd.apply(this, arguments);
                };
            }
            if (res && typeof res.once === 'function') {
                res.once('finish', finish);
                res.once('close', finish);
            }
        } catch (err) {
            // 监控异常不得影响业务
        }

        if (!span) return next();

        try {
            return runWithSpan(span, function () {
                return next();
            });
        } catch (err) {
            try {
                span.setError(err);
            } catch (ignore) {
                // 忽略
            }
            finish();
            throw err;
        }
    };
}

module.exports = createExpressMiddleware;
module.exports.isIgnoredPath = isIgnoredPath;
module.exports.getRoutePath = getRoutePath;
