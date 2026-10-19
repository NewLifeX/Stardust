/**
 * StardustTracer - Node.js 环境的星尘 APM 追踪器。
 *
 * 支持旧版位置参数 (server, appId, secret) 与新版配置对象。
 * 零外部依赖，仅使用 Node 内置模块。
 */

'use strict';

const Span = require('./span');
const SpanBuilder = require('./span-builder');
const { AppInfoCollector, getLocalIP } = require('./app-info');
const { postJson, postGzip, isAuthError } = require('./http');
const { runWithSpan } = require('./trace-context');
const { installAutoInstrumentation, unregister } = require('./instrument');

const DEFAULT_VERSION = '1.1.0';
const TOKEN_REFRESH_WINDOW_MS = 5 * 60 * 1000;

/**
 * 归一化构造函数参数。
 * @param {String|Object} serverOrOptions 服务器地址或配置对象
 * @param {String} [appId] 应用ID
 * @param {String} [secret] 应用密钥
 * @returns {Object} 配置对象
 */
function normalizeOptions(serverOrOptions, appId, secret) {
    if (typeof serverOrOptions === 'string' || serverOrOptions instanceof String) {
        return { server: String(serverOrOptions), appId: appId || '', secret: secret || '' };
    }
    if (serverOrOptions && typeof serverOrOptions === 'object') return Object.assign({}, serverOrOptions);
    return {};
}

/**
 * 转为正数，非法值使用默认值。
 * @param {*} value 原值
 * @param {Number} fallback 默认值
 * @returns {Number} 结果
 */
function positiveNumber(value, fallback) {
    const num = Number(value);
    return isFinite(num) && num > 0 ? num : fallback;
}

/**
 * 归一化排除项。
 * @param {*} value 数组或分隔字符串
 * @returns {String[]} 排除项
 */
function normalizeExcludes(value) {
    if (Array.isArray(value)) return value.map((item) => String(item)).filter((item) => item.length > 0);
    if (typeof value === 'string') {
        return value.split(/[,;]/).map((item) => item.trim()).filter((item) => item.length > 0);
    }
    return [];
}

/**
 * 把 tokenProvider 返回的 expire 归一化为时间戳（毫秒）。
 * 支持相对秒数、绝对秒数与绝对毫秒。
 * @param {*} value expire 值
 * @param {Number} fallbackSeconds 默认相对秒数
 * @returns {Number} 过期时间戳
 */
function expireToTimestamp(value, fallbackSeconds) {
    const num = Number(value);
    if (!isFinite(num) || num <= 0) return Date.now() + fallbackSeconds * 1000;
    if (num > 1e12) return num;
    if (num > 1e9) return num * 1000;
    return Date.now() + num * 1000;
}

/**
 * Node.js 星尘追踪器。
 */
class StardustTracer {
    /**
     * @param {String|Object} serverOrOptions 服务器地址或配置对象
     * @param {String} [appId] 应用ID（位置参数模式）
     * @param {String} [secret] 应用密钥（位置参数模式）
     */
    constructor(serverOrOptions, appId, secret) {
        const opts = normalizeOptions(serverOrOptions, appId, secret);

        this.server = String(opts.server || '').replace(/\/+$/, '');
        this.appId = opts.appId === undefined || opts.appId === null ? '' : String(opts.appId);
        this.appName = opts.appName === undefined || opts.appName === null ? this.appId : String(opts.appName);
        this.secret = opts.secret === undefined || opts.secret === null ? '' : String(opts.secret);
        this.version = opts.version ? String(opts.version) : DEFAULT_VERSION;

        this._localIP = getLocalIP();
        this.clientId = opts.clientId ? String(opts.clientId) : this._localIP + '@' + process.pid;

        this.period = positiveNumber(opts.period, 60);
        this.heartbeatPeriod = positiveNumber(opts.heartbeatPeriod, 30);
        this.maxSamples = positiveNumber(opts.maxSamples, 1);
        this.maxErrors = positiveNumber(opts.maxErrors, 10);
        this.maxTagLength = positiveNumber(opts.maxTagLength, 1024);
        this.requestTagLength = positiveNumber(opts.requestTagLength, 1024);
        this.timeout = positiveNumber(opts.timeout, 5000);
        this.enableMeter = opts.enableMeter !== false;
        this.excludes = normalizeExcludes(opts.excludes);
        this.debug = opts.debug === true;
        this.autoFlushOnExit = opts.autoFlushOnExit !== false;
        this.autoInstrument = opts.autoInstrument !== false;

        this._httpTimeout = positiveNumber(opts.httpTimeout, 10000);
        this._tokenProvider = typeof opts.tokenProvider === 'function' ? opts.tokenProvider : null;
        this._token = '';
        this._tokenExpire = 0;

        this._builders = new Map();
        this._running = false;
        this._reportTimer = null;
        this._pingTimer = null;
        this._flushPromise = null;
        this._loginPromise = null;
        this._pingPromise = null;
        this._stopPromise = null;
        this._exitHooks = null;

        this._info = new AppInfoCollector({
            appName: this.appName,
            version: this.version,
            ip: this._localIP,
        });

        // 自动埋点在构造阶段安装，start() 只负责登录与上报
        if (this.autoInstrument) {
            try {
                installAutoInstrumentation(this);
            } catch (err) {
                // 监控异常不得影响业务
            }
        }
    }

    /**
     * 启动追踪器：登录并开启心跳与上报定时器。
     * @returns {Promise<StardustTracer>} 当前实例
     */
    async start() {
        if (this._running) return this;
        this._running = true;

        if (this.autoInstrument) {
            try {
                installAutoInstrumentation(this);
            } catch (err) {
                // 监控异常不得影响业务
            }
        }

        await this._ensureToken(false);
        this._scheduleTimers();
        this._installExitHooks();
        this._log('started for ' + this.appId);

        return this;
    }

    /**
     * 停止追踪器：停止定时器并刷新剩余数据。
     * @returns {Promise<void>} 完成
     */
    async stop() {
        if (this._stopPromise) return this._stopPromise;

        this._running = false;
        this._clearTimers();
        this._removeExitHooks();
        if (this.autoInstrument) {
            try {
                unregister(this);
            } catch (err) {
                // 忽略
            }
        }

        const self = this;
        this._stopPromise = this.flush().then(function () {
            self._stopPromise = null;
        }, function () {
            self._stopPromise = null;
        });

        return this._stopPromise;
    }

    /**
     * 创建新的 Span。
     * @param {String} name Span 名称
     * @param {String|Object} [parentId] 父 Span ID，或 { parentId, traceId } 配置对象
     * @returns {Span} Span 对象
     */
    newSpan(name, parentId) {
        return new Span(name, this, parentId);
    }

    /**
     * 在新建 Span 的上下文中执行函数（支持同步与 Promise）。
     * @param {String} name Span 名称
     * @param {Function} fn 业务函数，入参为 Span
     * @returns {*} fn 的返回值
     */
    withSpan(name, fn) {
        const span = this.newSpan(name);

        return runWithSpan(span, function () {
            let result;
            try {
                result = typeof fn === 'function' ? fn(span) : undefined;
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
                return result.then(function (value) {
                    try {
                        span.finish();
                    } catch (ignore) {
                        // 忽略
                    }
                    return value;
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
            } catch (ignore) {
                // 忽略
            }
            return result;
        });
    }

    /**
     * 刷新并上报数据。并发调用会串行执行。
     * @returns {Promise<void>} 完成
     */
    flush() {
        const self = this;
        const run = function () {
            return self._doFlush();
        };
        this._flushPromise = (this._flushPromise || Promise.resolve()).then(run, run);
        return this._flushPromise;
    }

    /**
     * 兼容旧版内部方法。
     * @returns {Promise<void>} 完成
     * @private
     */
    _flush() {
        return this.flush();
    }

    /**
     * 完成 Span 并归入构建器。
     * @param {Span} span Span 对象
     * @private
     */
    _finishSpan(span) {
        if (!span) return;

        const name = String(span.name || '');
        if (this._isIgnoredName(name)) return;

        const limit = this.maxTagLength;
        if (span.tag && span.tag.length > limit) span.tag = span.tag.substring(0, limit);

        let builder = this._builders.get(name);
        if (!builder) {
            builder = new SpanBuilder(name, this.maxSamples, this.maxErrors);
            this._builders.set(name, builder);
        } else {
            builder._maxSamples = this.maxSamples;
            builder._maxErrors = this.maxErrors;
        }

        builder.addSpan(span);
    }

    /**
     * 判断名称是否应忽略。
     * @param {String} name 名称
     * @returns {Boolean} 是否忽略
     * @private
     */
    _isIgnoredName(name) {
        if (!name) return false;
        if (name.indexOf('/Trace/') !== -1) return true;
        if (name.indexOf('/App/Login') !== -1) return true;
        if (name.indexOf('/App/Ping') !== -1) return true;

        const lower = name.toLowerCase();
        for (const item of this.excludes) {
            if (item && lower.indexOf(item.toLowerCase()) !== -1) return true;
        }
        return false;
    }

    /**
     * 确保当前令牌可用。令牌将在 5 分钟内过期时主动重新登录。
     * @param {Boolean} force 是否强制刷新
     * @returns {Promise<String>} 令牌
     * @private
     */
    _ensureToken(force) {
        if (!force && this._token && (!this._tokenExpire || this._tokenExpire - Date.now() > TOKEN_REFRESH_WINDOW_MS)) {
            return Promise.resolve(this._token);
        }

        if (this._loginPromise) return this._loginPromise;

        const self = this;
        const promise = (async function () {
            try {
                return await self._login();
            } finally {
                self._loginPromise = null;
            }
        })();
        this._loginPromise = promise;
        return promise;
    }

    /**
     * 登录获取令牌，或调用 tokenProvider。
     * @returns {Promise<String>} 令牌
     * @private
     */
    async _login() {
        try {
            if (this._tokenProvider) {
                const result = await this._tokenProvider({
                    appId: this.appId,
                    appName: this.appName,
                    server: this.server,
                    version: this.version,
                    clientId: this.clientId,
                });

                let token = '';
                let expire = 0;
                if (typeof result === 'string') {
                    token = result;
                } else if (result && typeof result === 'object') {
                    token = result.token || result.Token || '';
                    expire = result.expire || result.Expire || 0;
                }
                if (token) {
                    this._token = String(token);
                    this._tokenExpire = expireToTimestamp(expire, 7200);
                }
                this._log('token obtained from provider');
                return this._token;
            }

            const payload = {
                AppId: this.appId,
                Secret: this.secret,
                ClientId: this.clientId,
                AppName: this.appName,
                Version: this.version,
                IP: this._localIP,
            };
            const data = await postJson(this.server + '/App/Login', payload, { timeout: this._httpTimeout });
            this._applyLoginResponse(data);
            this._log('login successful');
            return this._token;
        } catch (err) {
            this._log('login failed: ' + (err && err.message ? err.message : String(err)));
            return '';
        }
    }

    /**
     * 应用登录响应。
     * @param {Object} data 登录响应数据
     * @private
     */
    _applyLoginResponse(data) {
        if (!data) return;

        const token = data.Token || data.token || '';
        if (token) {
            const expire = Number(data.Expire || data.expire);
            this._token = String(token);
            this._tokenExpire = expireToTimestamp(expire, 7200);
        }
        if (data.Code) {
            const oldAppId = this.appId;
            this.appId = String(data.Code);
            if (this.appName === oldAppId) this.appName = this.appId;
            if (this._info) this._info.appName = this.appName;
        }
        if (data.Secret) this.secret = String(data.Secret);
    }

    /**
     * 发送带鉴权的请求；遇到 401 时重新登录并重试一次。
     * @param {String} path 接口路径
     * @param {*} payload 负载
     * @param {Boolean} [raw] 是否 GZip 压缩
     * @returns {Promise<*>} data
     * @private
     */
    async _requestWithAuth(path, payload, raw) {
        await this._ensureToken(false);

        try {
            return await this._send(path, payload, raw);
        } catch (err) {
            if (!isAuthError(err)) throw err;

            this._log('got 401, re-login and retry');
            await this._ensureToken(true);
            return this._send(path, payload, raw);
        }
    }

    /**
     * 发送单次请求。
     * @param {String} path 接口路径
     * @param {*} payload 负载
     * @param {Boolean} raw 是否 GZip 压缩
     * @returns {Promise<*>} data
     * @private
     */
    _send(path, payload, raw) {
        const url = this.server + path + '?Token=' + encodeURIComponent(this._token || '');
        const options = { timeout: this._httpTimeout };
        return raw ? postGzip(url, payload, options) : postJson(url, payload, options);
    }

    /**
     * 发送心跳。
     * @returns {Promise<void>} 完成
     * @private
     */
    _ping() {
        if (this._pingPromise) return this._pingPromise;

        const self = this;
        this._pingPromise = this._doPing().then(function () {
            self._pingPromise = null;
        }, function () {
            self._pingPromise = null;
        });
        return this._pingPromise;
    }

    /**
     * 执行心跳请求。
     * @returns {Promise<void>} 完成
     * @private
     */
    async _doPing() {
        try {
            const data = await this._requestWithAuth('/App/Ping', this._info.collect());
            if (data) {
                const token = data.Token || data.token;
                if (token) {
                    this._token = String(token);
                    const expire = Number(data.Expire || data.expire);
                    if (expire > 0) this._tokenExpire = Date.now() + expire * 1000;
                }
                this._applyResponse(data);
            }
            this._log('ping sent');
        } catch (err) {
            this._log('ping failed: ' + (err && err.message ? err.message : String(err)));
        }
    }

    /**
     * 执行上报。
     * @returns {Promise<void>} 完成
     * @private
     */
    async _doFlush() {
        try {
            if (this._builders.size === 0) return;

            const list = [];
            for (const builder of this._builders.values()) {
                if (builder.total > 0) list.push(builder.toJSON());
            }
            this._builders.clear();
            if (list.length === 0) return;

            const payload = {
                AppId: this.appId,
                AppName: this.appName,
                ClientId: this.clientId,
                Version: this.version,
                Builders: list,
            };
            if (this.enableMeter) payload.Info = this._info.collect();

            const body = JSON.stringify(payload);
            let data;
            if (Buffer.byteLength(body, 'utf8') > 1024) {
                data = await this._requestWithAuth('/Trace/ReportRaw', body, true);
            } else {
                data = await this._requestWithAuth('/Trace/Report', payload, false);
            }
            if (data) this._applyResponse(data);

            this._log('report sent, builders=' + list.length);
        } catch (err) {
            this._log('report failed: ' + (err && err.message ? err.message : String(err)));
        }
    }

    /**
     * 应用服务端返回的参数。
     * @param {Object} result 响应数据
     * @private
     */
    _applyResponse(result) {
        if (!result || typeof result !== 'object') return;

        const oldPeriod = this.period;

        if (Number(result.Period) > 0) this.period = Number(result.Period);
        if (Number(result.MaxSamples) > 0) this.maxSamples = Number(result.MaxSamples);
        if (Number(result.MaxErrors) > 0) this.maxErrors = Number(result.MaxErrors);
        if (Number(result.Timeout) > 0) this.timeout = Number(result.Timeout);
        if (Number(result.MaxTagLength) > 0) this.maxTagLength = Number(result.MaxTagLength);
        if (Number(result.RequestTagLength) > 0) this.requestTagLength = Number(result.RequestTagLength);
        if (result.EnableMeter !== undefined && result.EnableMeter !== null) this.enableMeter = !!result.EnableMeter;

        if (Array.isArray(result.Excludes)) {
            this.excludes = result.Excludes.map((item) => String(item));
        } else if (typeof result.Excludes === 'string') {
            this.excludes = normalizeExcludes(result.Excludes);
        } else if (Object.prototype.hasOwnProperty.call(result, 'Excludes') && result.Excludes === null) {
            this.excludes = [];
        }

        for (const builder of this._builders.values()) {
            builder._maxSamples = this.maxSamples;
            builder._maxErrors = this.maxErrors;
        }

        if (this._running && oldPeriod !== this.period) {
            this._log('period changed to ' + this.period + 's');
            this._scheduleTimers();
        }
    }

    /**
     * 创建或重置定时器。
     * @private
     */
    _scheduleTimers() {
        this._clearTimers();
        if (!this._running) return;

        const self = this;
        this._reportTimer = setInterval(function () {
            self.flush();
        }, Math.max(1, this.period) * 1000);
        this._pingTimer = setInterval(function () {
            self._ping();
        }, Math.max(1, this.heartbeatPeriod) * 1000);

        if (this._reportTimer && typeof this._reportTimer.unref === 'function') this._reportTimer.unref();
        if (this._pingTimer && typeof this._pingTimer.unref === 'function') this._pingTimer.unref();
    }

    /**
     * 清理定时器。
     * @private
     */
    _clearTimers() {
        if (this._reportTimer) {
            clearInterval(this._reportTimer);
            this._reportTimer = null;
        }
        if (this._pingTimer) {
            clearInterval(this._pingTimer);
            this._pingTimer = null;
        }
    }

    /**
     * 安装退出钩子。定时器已 unref，不会阻止进程正常退出。
     * @private
     */
    _installExitHooks() {
        if (!this.autoFlushOnExit || this._exitHooks) return;
        if (typeof process === 'undefined' || typeof process.once !== 'function') return;

        const self = this;
        const hooks = {};

        hooks.beforeExit = function () {
            try {
                self.flush();
            } catch (err) {
                // 忽略退出刷新异常
            }
        };
        process.once('beforeExit', hooks.beforeExit);

        const installSignal = function (signal) {
            let hadListeners = false;
            try {
                hadListeners = process.listenerCount(signal) > 0;
            } catch (err) {
                hadListeners = false;
            }

            hooks[signal] = function () {
                const done = function () {
                    if (!hadListeners) {
                        try {
                            process.exit(signal === 'SIGINT' ? 130 : 143);
                        } catch (err) {
                            // 忽略退出异常
                        }
                    }
                };
                try {
                    const promise = self.flush();
                    if (promise && typeof promise.then === 'function') promise.then(done, done);
                    else done();
                } catch (err) {
                    done();
                }
            };
            process.once(signal, hooks[signal]);
        };

        installSignal('SIGINT');
        installSignal('SIGTERM');
        this._exitHooks = hooks;
    }

    /**
     * 移除退出钩子。
     * @private
     */
    _removeExitHooks() {
        if (!this._exitHooks || typeof process === 'undefined' || typeof process.removeListener !== 'function') return;

        const hooks = this._exitHooks;
        for (const key of Object.keys(hooks)) {
            try {
                process.removeListener(key, hooks[key]);
            } catch (err) {
                // 忽略移除异常
            }
        }
        this._exitHooks = null;
    }

    /**
     * 调试日志。仅在 debug 为 true 时输出。
     * @param {String} message 消息
     * @private
     */
    _log(message) {
        if (this.debug && typeof console !== 'undefined' && console.log) console.log('[Stardust] ' + message);
    }
}

module.exports = StardustTracer;
