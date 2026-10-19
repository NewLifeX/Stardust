/**
 * Node.js HTTP 自动埋点。
 *
 * 出站：包装 http/https.request/get 与全局 fetch，创建 Span 并注入 traceparent。
 * 入站：包装 http/https.createServer，在服务端 Span 上下文中执行监听器。
 */

'use strict';

const http = require('http');
const https = require('https');
const { getCurrentSpan, formatTraceparent, parseTraceparent, runWithSpan } = require('../trace-context');
const { getTracer } = require('./registry');
const { isMarkedInternal, isInternalPath, isInternalUrl } = require('./marker');

/**
 * 截断文本。
 * @param {*} value 原值
 * @param {Number} limit 最大长度
 * @returns {String} 结果
 */
function truncate(value, limit) {
    const text = value === null || value === undefined ? '' : String(value);
    if (limit > 0 && text.length > limit) return text.substring(0, limit);
    return text;
}

/**
 * 读取请求头（不区分大小写，忽略数组细节）。
 * @param {Object} headers 头集合
 * @param {String} name 头名
 * @returns {String} 头值
 */
function getHeader(headers, name) {
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
 * 归一化 request/get 参数。
 * @param {Array} args 原始参数
 * @returns {Object} { urlArg, options, optionsIndex }
 */
function normalizeArgs(args) {
    const first = args[0];
    const isUrl = typeof first === 'string' || (typeof URL !== 'undefined' && first instanceof URL);
    if (!isUrl) return { urlArg: null, options: first && typeof first === 'object' ? first : {}, optionsIndex: 0 };

    const second = args[1];
    if (second && typeof second === 'object' && typeof second !== 'function') {
        return { urlArg: first, options: second, optionsIndex: 1 };
    }
    return { urlArg: first, options: {}, optionsIndex: 1 };
}

/**
 * 解析出站请求 URL。
 * @param {String|URL} urlArg URL 参数
 * @param {Object} options 请求参数
 * @returns {URL|null} URL
 */
function resolveUrl(urlArg, options) {
    try {
        if (urlArg) return new URL(typeof urlArg === 'string' ? urlArg : String(urlArg), 'http://localhost');
    } catch (err) {
        // 继续尝试 options
    }

    try {
        const protocol = options.protocol || 'http:';
        let hostname = options.hostname || options.host || 'localhost';
        let port = options.port ? String(options.port) : '';
        const hostText = String(hostname);
        const colon = hostText.lastIndexOf(':');
        if (colon > 0 && hostText.indexOf(']') === -1) {
            if (!port) port = hostText.substring(colon + 1);
            hostname = hostText.substring(0, colon);
        }
        return new URL(protocol + '//' + hostname + (port ? ':' + port : '') + (options.path || '/'));
    } catch (err) {
        return null;
    }
}

/**
 * 复制请求头为普通对象。
 * @param {*} source 原头集合
 * @returns {Object} 头对象
 */
function cloneHeaders(source) {
    const headers = {};
    if (!source) return headers;

    try {
        if (Array.isArray(source)) {
            for (let i = 0; i + 1 < source.length; i += 2) headers[source[i]] = source[i + 1];
        } else if (typeof source.forEach === 'function' && typeof source.get === 'function') {
            source.forEach(function (value, key) {
                headers[key] = value;
            });
        } else {
            for (const key of Object.keys(source)) headers[key] = source[key];
        }
    } catch (err) {
        // 头集合取值异常时忽略
    }
    return headers;
}

/**
 * 判断头集合中是否已显式设置 traceparent。
 * @param {Object} headers 头对象
 * @returns {Boolean} 是否存在
 */
function hasTraceparent(headers) {
    for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === 'traceparent') return true;
    }
    return false;
}

/**
 * 在出站请求参数中注入 traceparent，返回新的参数对象。
 * @param {Object} options 原请求参数
 * @param {Object} span 出站 Span
 * @returns {Object} 新请求参数
 */
function injectTraceparent(options, span) {
    const cloned = {};
    for (const key of Object.keys(options)) cloned[key] = options[key];

    const headers = cloneHeaders(options.headers);
    if (!hasTraceparent(headers)) headers.traceparent = formatTraceparent(span.traceId, span.id, true);
    cloned.headers = headers;

    return cloned;
}

/**
 * 创建出站 Span 并执行原始 request/get。
 * @param {Object} original 原始方法
 * @param {Object} self this
 * @param {Array} args 原始参数
 * @returns {*} 原始返回值
 */
function traceRequest(original, self, args) {
    let tracer = null;
    let span = null;
    let invoked = false;

    try {
        tracer = getTracer();
        if (!tracer) return original.apply(self, args);

        const first = args[0];
        if (!first || (typeof first !== 'string' && typeof first !== 'object')) return original.apply(self, args);

        const normalized = normalizeArgs(args);
        if (isMarkedInternal(normalized.options)) return original.apply(self, args);

        const url = resolveUrl(normalized.urlArg, normalized.options);
        const path = url ? url.pathname : String(normalized.options.path || '/').split('?')[0];
        if (isInternalPath(path) || isInternalUrl(url ? url.href : path)) return original.apply(self, args);

        const method = String(normalized.options.method || 'GET').toUpperCase();
        const name = url ? method + ' ' + url.host + (url.pathname || '/') : method + ' ' + path;

        const current = getCurrentSpan();
        if (current) span = tracer.newSpan(name, { parentId: current.id, traceId: current.traceId });
        else span = tracer.newSpan(name);
        span.setTag(truncate(url ? url.href : path, tracer.maxTagLength));

        const injected = injectTraceparent(normalized.options, span);
        const callArgs = Array.prototype.slice.call(args);
        if (normalized.optionsIndex === 1 && typeof args[1] === 'function') {
            // request(url, callback)：在 URL 与回调之间插入 options
            callArgs.splice(1, 0, injected);
        } else {
            // request(url, options|null|undefined, callback) 或 request(options, callback)
            callArgs[normalized.optionsIndex] = injected;
        }

        let settled = false;
        const finish = function (err) {
            if (settled) return;
            settled = true;
            try {
                if (err) span.setError(err);
            } catch (ignore) {
                // 忽略
            }
            try {
                span.finish();
            } catch (ignore) {
                // 忽略
            }
        };

        let req;
        try {
            invoked = true;
            req = original.apply(self, callArgs);
        } catch (err) {
            finish(err);
            throw err;
        }

        try {
            if (req && typeof req.once === 'function') {
                req.once('error', function (err) {
                    finish(err);

                    // 没有业务错误监听器时保持 Node 默认的未捕获异常行为
                    try {
                        const count = typeof req.listenerCount === 'function' ? req.listenerCount('error') : 0;
                        if (count === 0 && typeof process !== 'undefined' && typeof process.nextTick === 'function') {
                            process.nextTick(function () {
                                throw err;
                            });
                        }
                    } catch (ignore) {
                        // 忽略
                    }
                });
                req.once('abort', function () {
                    finish('Request aborted');
                });
                req.on('response', function (res) {
                    try {
                        if (res && res.statusCode >= 400) span.setError('HTTP ' + res.statusCode);
                        if (typeof res.once === 'function') {
                            res.once('end', function () {
                                finish();
                            });
                            res.once('close', function () {
                                finish();
                            });
                            res.once('aborted', function () {
                                finish('Request aborted');
                            });
                            res.once('error', finish);
                        } else {
                            finish();
                        }
                    } catch (err) {
                        finish();
                    }
                });
            } else if (span) {
                finish();
            }
        } catch (err) {
            finish();
        }

        return req;
    } catch (err) {
        // 监控异常不得影响业务
        try {
            if (span) span.finish();
        } catch (ignore) {
            // 忽略
        }
        if (invoked) throw err;
        return original.apply(self, args);
    }
}

/**
 * 包装模块的 request/get。
 * @param {Object} mod http 或 https 模块
 * @returns {void}
 */
function wrapModule(mod) {
    const methods = ['request', 'get'];
    for (const method of methods) {
        const original = mod[method];
        if (typeof original !== 'function' || original.__stardustWrapped) continue;

        const wrapped = function () {
            return traceRequest(original, this, arguments);
        };
        wrapped.__stardustWrapped = true;
        mod[method] = wrapped;
    }
}

/**
 * 包装全局 fetch。
 * @returns {void}
 */
function wrapFetch() {
    const globalObj = typeof globalThis !== 'undefined' ? globalThis : (typeof global !== 'undefined' ? global : null);
    if (!globalObj || typeof globalObj.fetch !== 'function') return;

    const original = globalObj.fetch;
    if (original.__stardustWrapped) return;

    const wrapped = function (input, init) {
        let span = null;
        let tracer = null;
        let invoked = false;

        try {
            tracer = getTracer();
            if (!tracer || isMarkedInternal(init)) return original.apply(this, arguments);

            const urlText = typeof input === 'string'
                ? input
                : (input && input.url ? String(input.url) : String(input));
            if (isInternalUrl(urlText)) return original.apply(this, arguments);

            const method = String((init && init.method) || (input && input.method) || 'GET').toUpperCase();
            let parsed = null;
            try {
                parsed = new URL(urlText, 'http://localhost');
            } catch (err) {
                parsed = null;
            }

            const name = parsed
                ? method + ' ' + parsed.host + (parsed.pathname || '/')
                : method + ' ' + urlText.split('?')[0];
            const current = getCurrentSpan();
            if (current) span = tracer.newSpan(name, { parentId: current.id, traceId: current.traceId });
            else span = tracer.newSpan(name);
            span.setTag(truncate(urlText, tracer.maxTagLength));

            const newInit = injectFetchHeaders(input, init, span);
            let result;
            try {
                invoked = true;
                result = original.call(this, input, newInit);
            } catch (err) {
                try {
                    span.setError(err);
                    span.finish();
                } catch (ignore) {
                    // 忽略
                }
                throw err;
            }

            if (result && typeof result.then === 'function') {
                return result.then(function (response) {
                    try {
                        if (response && response.status >= 400) span.setError('HTTP ' + response.status);
                    } catch (err) {
                        // 忽略
                    }
                    try {
                        span.finish();
                    } catch (err) {
                        // 忽略
                    }
                    return response;
                }, function (err) {
                    try {
                        span.setError(err);
                        span.finish();
                    } catch (ignore) {
                        // 忽略
                    }
                    throw err;
                });
            }

            try {
                span.finish();
            } catch (err) {
                // 忽略
            }
            return result;
        } catch (err) {
            // 监控异常不得影响业务
            try {
                if (span) span.finish();
            } catch (ignore) {
                // 忽略
            }
            if (invoked) throw err;
            return original.apply(this, arguments);
        }
    };

    wrapped.__stardustWrapped = true;
    globalObj.fetch = wrapped;
}

/**
 * 为 fetch 请求构造带 traceparent 的 init。
 * @param {*} input fetch 入参（URL 或 Request）
 * @param {Object} init 原始 init
 * @param {Object} span 出站 Span
 * @returns {Object} 新 init
 */
function injectFetchHeaders(input, init, span) {
    const value = formatTraceparent(span.traceId, span.id, true);
    const cloned = {};
    if (init && typeof init === 'object') {
        for (const key of Object.keys(init)) cloned[key] = init[key];
    }

    const globalObj = typeof globalThis !== 'undefined' ? globalThis : null;
    const HeadersCtor = globalObj && typeof globalObj.Headers === 'function' ? globalObj.Headers : null;

    if (HeadersCtor) {
        const headers = new HeadersCtor();
        try {
            if (input && typeof input === 'object' && input.headers && typeof input.headers.forEach === 'function') {
                input.headers.forEach(function (headerValue, headerName) {
                    headers.set(headerName, headerValue);
                });
            }
            if (init && init.headers) {
                const own = new HeadersCtor(init.headers);
                own.forEach(function (headerValue, headerName) {
                    headers.set(headerName, headerValue);
                });
            }
            if (!headers.has('traceparent')) headers.set('traceparent', value);
        } catch (err) {
            // 忽略头构造异常
        }
        cloned.headers = headers;
        return cloned;
    }

    const headers = cloneHeaders(init && init.headers);
    if (!hasTraceparent(headers)) headers.traceparent = value;
    cloned.headers = headers;
    return cloned;
}

/**
 * 包装服务端请求监听器：在服务端 Span 上下文中执行。
 * @param {Function} listener 原始监听器
 * @returns {Function} 包装后的监听器
 */
function wrapRequestListener(listener) {
    if (typeof listener !== 'function') return listener;
    if (listener.__stardustWrapped) return listener;

    const wrapped = function (req, res) {
        let span = null;
        let invoked = false;

        try {
            const tracer = getTracer();
            const rawUrl = String((req && req.url) || '/');
            const path = rawUrl.split('?')[0] || '/';
            if (!tracer || isInternalPath(path)) return listener.call(this, req, res);

            // 中间件已创建 Span 时直接复用，避免重复
            if (req && req.__stardustSpan) {
                const existing = req.__stardustSpan;
                let reusedSettled = false;
                const finishReused = function (err) {
                    if (reusedSettled) return;
                    reusedSettled = true;
                    try {
                        if (err) existing.setError(err);
                        else if (res && res.statusCode >= 500) existing.setError('HTTP ' + res.statusCode);
                    } catch (ignore) {
                        // 忽略
                    }
                    try {
                        existing.finish();
                    } catch (ignore) {
                        // 忽略
                    }
                };

                try {
                    if (res && typeof res.once === 'function') {
                        res.once('finish', function () {
                            finishReused();
                        });
                        res.once('close', function () {
                            finishReused();
                        });
                        res.once('error', finishReused);
                    }
                } catch (err) {
                    // 忽略
                }

                return runWithSpan(existing, function () {
                    invoked = true;
                    return listener.call(this, req, res);
                }.bind(this));
            }

            const method = String((req && req.method) || 'GET').toUpperCase();
            const inbound = parseTraceparent(getHeader(req && req.headers, 'traceparent'));
            if (inbound) span = tracer.newSpan(method + ' ' + path, { parentId: inbound.spanId, traceId: inbound.traceId });
            else span = tracer.newSpan(method + ' ' + path);
            span.setTag(truncate(method + ' ' + rawUrl, tracer.maxTagLength));

            try {
                req.__stardustSpan = span;
            } catch (err) {
                // 忽略
            }

            let settled = false;
            const finish = function (err) {
                if (settled) return;
                settled = true;
                try {
                    if (err) span.setError(err);
                    else if (res && res.statusCode >= 500) span.setError('HTTP ' + res.statusCode);
                } catch (ignore) {
                    // 忽略
                }
                try {
                    span.finish();
                } catch (ignore) {
                    // 忽略
                }
            };

            try {
                if (res && typeof res.once === 'function') {
                    res.once('finish', function () {
                        finish();
                    });
                    res.once('close', function () {
                        finish();
                    });
                    res.once('error', finish);
                }
            } catch (err) {
                // 忽略
            }

            return runWithSpan(span, function () {
                try {
                    invoked = true;
                    return listener.call(this, req, res);
                } catch (err) {
                    finish(err);
                    throw err;
                }
            }.bind(this));
        } catch (err) {
            // 监控异常不得影响业务
            try {
                if (span) span.finish();
            } catch (ignore) {
                // 忽略
            }
            if (req && req.__stardustSpan === span) {
                try {
                    delete req.__stardustSpan;
                } catch (ignore) {
                    // 忽略
                }
            }
            if (invoked) throw err;
            return listener.call(this, req, res);
        }
    };

    wrapped.__stardustWrapped = true;
    return wrapped;
}

/**
 * 包装模块的 createServer。
 * @param {Object} mod http 或 https 模块
 * @returns {void}
 */
function wrapCreateServer(mod) {
    const original = mod.createServer;
    if (typeof original !== 'function' || original.__stardustWrapped) return;

    const wrapped = function createServer(options, listener) {
        let opts = options;
        let fn = listener;
        if (typeof options === 'function') {
            fn = options;
            opts = undefined;
        }
        if (typeof fn !== 'function') return original.apply(this, arguments);

        const wrappedListener = wrapRequestListener(fn);
        if (arguments.length <= 1) return original.call(this, wrappedListener);
        return original.call(this, opts, wrappedListener);
    };

    wrapped.__stardustWrapped = true;
    mod.createServer = wrapped;
}

/**
 * 安装 HTTP 出站与入站自动埋点（幂等）。
 * @param {Object} tracer 追踪器
 * @returns {void}
 */
function install(tracer) {
    try {
        wrapModule(http);
    } catch (err) {
        // 忽略
    }
    try {
        wrapModule(https);
    } catch (err) {
        // 忽略
    }
    try {
        wrapCreateServer(http);
    } catch (err) {
        // 忽略
    }
    try {
        wrapCreateServer(https);
    } catch (err) {
        // 忽略
    }
    try {
        wrapFetch();
    } catch (err) {
        // 忽略
    }
}

module.exports = {
    install,
    wrapRequestListener,
    wrapFetch,
    wrapModule,
    wrapCreateServer,
};
