/**
 * Stardust Browser SDK - 星尘 APM 浏览器追踪器。
 *
 * 单文件 UMD，无构建步骤、无运行时依赖，可直接通过 <script> 引入。
 * 同时支持 CommonJS（用于 Node 单元测试）。
 */
(function (root, factory) {
    'use strict';

    var api = factory(root);

    if (typeof module === 'object' && module.exports) {
        module.exports = api.StardustTracer;
        module.exports.StardustTracer = api.StardustTracer;
        module.exports.createTracer = api.createTracer;
        module.exports.init = api.init;
        module.exports.captureException = api.captureException;
        module.exports.flush = api.flush;
        module.exports.version = api.version;
        module.exports.installBrowserInterceptors = api.installBrowserInterceptors;
        module.exports.installFetchInterceptor = api.installFetchInterceptor;
        module.exports.installXHRInterceptor = api.installXHRInterceptor;
        module.exports.parseTraceparent = api.parseTraceparent;
        module.exports.formatTraceparent = api.formatTraceparent;
        module.exports.generateTraceId = api.generateTraceId;
        module.exports.generateSpanId = api.generateSpanId;
    }

    var win = null;
    try {
        win = root && root.window ? root.window : (typeof window !== 'undefined' ? window : null);
    } catch (err) {
        win = null;
    }
    if (win) {
        try {
            if (!win.Stardust) win.Stardust = {};
            win.Stardust.StardustTracer = api.StardustTracer;
            win.Stardust.createTracer = api.createTracer;
            win.Stardust.init = api.init;
            win.Stardust.captureException = api.captureException;
            win.Stardust.flush = api.flush;
            win.Stardust.version = api.version;
            win.Stardust.parseTraceparent = api.parseTraceparent;
            win.Stardust.formatTraceparent = api.formatTraceparent;
            win.Stardust.generateTraceId = api.generateTraceId;
            win.Stardust.generateSpanId = api.generateSpanId;
            win.StardustBrowserTracer = api.StardustTracer;
            win.installBrowserInterceptors = api.installBrowserInterceptors;
        } catch (err) {
            // 全局对象可能被冻结，忽略挂载失败
        }
    }
})(typeof globalThis !== 'undefined' ? globalThis : (typeof window !== 'undefined' ? window : this), function (root) {
    'use strict';

    var VERSION = '1.1.0';
    var TOKEN_REFRESH_WINDOW_MS = 5 * 60 * 1000;
    var memoryClientIds = Object.create(null);

    function hasOwn(obj, key) {
        return Object.prototype.hasOwnProperty.call(obj, key);
    }

    function positiveNumber(value, fallback) {
        var num = Number(value);
        return isFinite(num) && num > 0 ? num : fallback;
    }

    function normalizeExcludes(value) {
        if (Array.isArray(value)) {
            return value.map(function (item) {
                return String(item);
            }).filter(function (item) {
                return item.length > 0;
            });
        }
        if (typeof value === 'string') {
            return value.split(/[,;]/).map(function (item) {
                return item.trim();
            }).filter(function (item) {
                return item.length > 0;
            });
        }
        return [];
    }

    function formatError(err) {
        if (err === null || err === undefined) return 'Unknown error';
        if (typeof err === 'string') return err;
        if (typeof err === 'object' && err.message) {
            var name = err.name ? err.name + ': ' : '';
            return name + err.message;
        }
        try {
            return String(err);
        } catch (ignore) {
            return 'Unknown error';
        }
    }

    function isAuthError(err) {
        if (!err) return false;
        return Number(err.code) === 401 || Number(err.statusCode) === 401;
    }

    function randomHex(length) {
        var chars = '0123456789abcdef';
        var out = '';
        var cryptoObj = null;
        try {
            if (typeof window !== 'undefined' && window && window.crypto) cryptoObj = window.crypto;
            else if (root && root.crypto) cryptoObj = root.crypto;
            else if (typeof crypto !== 'undefined') cryptoObj = crypto;
        } catch (err) {
            cryptoObj = null;
        }

        if (cryptoObj && typeof cryptoObj.getRandomValues === 'function') {
            try {
                var bytes = new Uint8Array(Math.ceil(length / 2));
                cryptoObj.getRandomValues(bytes);
                for (var i = 0; i < bytes.length; i++) {
                    out += chars.charAt(bytes[i] >> 4) + chars.charAt(bytes[i] & 15);
                }
                return out.substring(0, length);
            } catch (err) {
                out = '';
            }
        }

        for (var j = 0; j < length; j++) {
            out += chars.charAt(Math.floor(Math.random() * 16));
        }
        return out;
    }

    var TRACEPARENT_PATTERN = /^([0-9a-f]{2})-([0-9a-f]{1,32})-([0-9a-f]{16})-([0-9a-f]{2})(?:-.*)?$/i;

    /**
     * 生成 32 位 TraceId。
     * @returns {String} TraceId
     */
    function generateTraceId() {
        return randomHex(32);
    }

    /**
     * 生成 16 位 SpanId。
     * @returns {String} SpanId
     */
    function generateSpanId() {
        return randomHex(16);
    }

    /**
     * 解析 traceparent 头。
     * @param {String} value 头值
     * @returns {Object|null} { version, traceId, spanId, flags, sampled }
     */
    function parseTraceparent(value) {
        if (Array.isArray(value)) value = value.length > 0 ? value[0] : '';
        if (value === null || value === undefined) return null;

        var text = String(value).replace(/^\s+|\s+$/g, '');
        if (!text) return null;

        var match = TRACEPARENT_PATTERN.exec(text);
        if (!match) return null;

        var version = match[1].toLowerCase();
        if (version === 'ff') return null;
        if (version === '00' && text.split('-').length !== 4) return null;

        var traceId = match[2].toLowerCase();
        var spanId = match[3].toLowerCase();
        if (/^0+$/.test(traceId) || /^0+$/.test(spanId)) return null;

        var flags = parseInt(match[4], 16);
        if (!isFinite(flags)) return null;

        return {
            version: version,
            traceId: traceId,
            spanId: spanId,
            flags: flags,
            sampled: (flags & 1) === 1,
        };
    }

    /**
     * 格式化 traceparent 头。
     * @param {String} traceId TraceId
     * @param {String} spanId SpanId
     * @param {Boolean} [sampled] 是否采样，默认 true
     * @returns {String} traceparent 值
     */
    function formatTraceparent(traceId, spanId, sampled) {
        var trace = String(traceId || '').toLowerCase();
        var span = String(spanId || '').toLowerCase();
        if (!/^[0-9a-f]{1,32}$/.test(trace) || /^0+$/.test(trace)) return '';
        if (!/^[0-9a-f]{16}$/.test(span) || /^0+$/.test(span)) return '';

        var flags = sampled === false ? '00' : '01';
        return '00-' + trace + '-' + span + '-' + flags;
    }

    /**
     * 复制对象自有属性。
     * @param {Object} target 目标
     * @param {Object} source 来源
     * @returns {Object} 目标
     */
    function assign(target, source) {
        if (!source) return target;
        for (var key in source) {
            if (hasOwn(source, key)) target[key] = source[key];
        }
        return target;
    }

    /**
     * 判断头集合是否已显式设置 traceparent。
     * @param {Object} headers 头对象
     * @returns {Boolean} 是否存在
     */
    function hasTraceparent(headers) {
        if (!headers) return false;
        for (var key in headers) {
            if (hasOwn(headers, key) && String(key).toLowerCase() === 'traceparent') return true;
        }
        return false;
    }

    function getRootWindow() {
        try {
            if (typeof window !== 'undefined' && window) return window;
        } catch (err) {
            // 忽略访问异常
        }
        try {
            if (root && root.window) return root.window;
        } catch (err) {
            // 忽略访问异常
        }
        return root || {};
    }

    function getWindowProperty(win, name) {
        var candidates = [win, root];
        for (var i = 0; i < candidates.length; i++) {
            var obj = candidates[i];
            if (!obj) continue;
            try {
                if (obj[name]) return obj[name];
            } catch (err) {
                // 忽略安全异常
            }
        }
        return null;
    }

    function getStorage(win, name) {
        var candidates = [win, root];
        for (var i = 0; i < candidates.length; i++) {
            var obj = candidates[i];
            if (!obj) continue;
            try {
                var storage = obj[name];
                if (storage && typeof storage.getItem === 'function') return storage;
            } catch (err) {
                // localStorage 可能被禁用
            }
        }
        return null;
    }

    function getBlobConstructor(win) {
        var ctor = getWindowProperty(win, 'Blob');
        return typeof ctor === 'function' ? ctor : null;
    }

    function getEventTarget(win) {
        try {
            if (win && typeof win.addEventListener === 'function') return win;
        } catch (err) {
            // 忽略
        }
        try {
            if (root && typeof root.addEventListener === 'function') return root;
        } catch (err) {
            // 忽略
        }
        return null;
    }

    function getHref(win) {
        var loc = getWindowProperty(win, 'location');
        if (loc && loc.href) return String(loc.href);
        return 'http://localhost/';
    }

    function resolveAbsoluteUrl(url, win) {
        try {
            return new URL(String(url), getHref(win)).href;
        } catch (err) {
            return String(url === null || url === undefined ? '' : url);
        }
    }

    function resolveRequestUrl(input) {
        if (input && typeof input === 'object' && typeof input.url === 'string') return input.url;
        try {
            return String(input);
        } catch (err) {
            return '';
        }
    }

    function isIgnoredUrl(url) {
        var text = String(url || '');
        if (text.indexOf('/Trace/') !== -1) return true;
        if (text.indexOf('/App/Login') !== -1) return true;
        if (text.indexOf('/App/Ping') !== -1) return true;
        return false;
    }

    /**
     * 判断是否向目标 URL 传播 traceparent。默认仅同源，避免跨域请求因自定义头触发 CORS 失败。
     * @param {StardustTracer} tracer 追踪器
     * @param {String} url 目标地址
     * @returns {Boolean} 是否注入
     */
    function shouldPropagateTrace(tracer, url) {
        var text = String(url || '');
        if (!text) return false;

        var targets = tracer ? tracer.tracePropagationTargets : null;
        if (targets === true || targets === '*') return true;
        if (!targets) {
            var loc = tracer && tracer._loc ? tracer._loc : getWindowProperty(getRootWindow(), 'location');
            var origin = loc && loc.origin ? String(loc.origin) : '';
            return !origin || text.indexOf(origin + '/') === 0 || text === origin;
        }

        var list = Array.isArray(targets) ? targets : [targets];
        var parsed = null;
        try {
            parsed = new URL(text, getHref(tracer._win));
        } catch (err) {
            parsed = null;
        }
        for (var i = 0; i < list.length; i++) {
            var target = list[i];
            if (target && typeof target === 'object' && typeof target.test === 'function') {
                if (target.test(text)) return true;
                continue;
            }
            var value = String(target || '');
            if (!value) continue;
            if (value === '*') return true;
            if (text.indexOf(value) === 0) return true;
            if (parsed && (parsed.host === value || parsed.hostname === value)) return true;
        }
        return false;
    }

    function buildSpanName(method, url, win) {
        var parsed = null;
        try {
            parsed = new URL(String(url), getHref(win));
        } catch (err) {
            parsed = null;
        }
        if (!parsed) {
            var clean = String(url || '').split('#')[0].split('?')[0];
            return method + ' ' + (clean || '/');
        }

        var loc = getWindowProperty(win, 'location');
        var sameOrigin = false;
        try {
            if (loc && loc.origin) sameOrigin = parsed.origin === loc.origin;
            else if (loc && loc.host) sameOrigin = parsed.host === loc.host;
            else sameOrigin = parsed.origin === new URL(getHref(win)).origin;
        } catch (err) {
            sameOrigin = false;
        }

        var path = sameOrigin ? (parsed.pathname || '/') : (parsed.host + (parsed.pathname || '/'));
        return method + ' ' + path;
    }

    function truncate(value, limit) {
        var text = value === null || value === undefined ? '' : String(value);
        if (limit > 0 && text.length > limit) return text.substring(0, limit);
        return text;
    }

    function expireToTimestamp(value, fallbackSeconds) {
        var num = Number(value);
        if (!isFinite(num) || num <= 0) return Date.now() + fallbackSeconds * 1000;
        if (num > 1e12) return num;
        if (num > 1e9) return num * 1000;
        return Date.now() + num * 1000;
    }

    function navigationDelta(entry, endName, startName) {
        var end = Number(entry[endName]) || 0;
        var start = Number(entry[startName]) || 0;
        var value = end - start;
        return value > 0 ? Math.round(value) : 0;
    }

    function buildNavigationTag(entry) {
        return 'dns=' + navigationDelta(entry, 'domainLookupEnd', 'domainLookupStart') +
            ';tcp=' + navigationDelta(entry, 'connectEnd', 'connectStart') +
            ';ttfb=' + navigationDelta(entry, 'responseStart', 'requestStart') +
            ';dom=' + navigationDelta(entry, 'domContentLoadedEventEnd', 'domContentLoadedEventStart') +
            ';load=' + navigationDelta(entry, 'loadEventEnd', 'loadEventStart');
    }

    /**
     * 浏览器端 Span 聚合器。
     */
    class BrowserSpanBuilder {
        constructor(name, maxSamples, maxErrors) {
            this.name = name;
            this.startTime = Date.now();
            this.endTime = 0;
            this.total = 0;
            this.errors = 0;
            this.cost = 0;
            this.maxCost = 0;
            this.minCost = 0;
            this.samples = [];
            this.errorSamples = [];
            this._maxSamples = maxSamples > 0 ? maxSamples : 1;
            this._maxErrors = maxErrors > 0 ? maxErrors : 10;
        }

        addSpan(span) {
            var elapsed = Math.max(0, span.endTime - span.startTime);
            this.total++;
            this.cost += elapsed;

            if (this.total === 1 || elapsed > this.maxCost) this.maxCost = elapsed;
            if (this.total === 1 || elapsed < this.minCost) this.minCost = elapsed;

            if (span.error) {
                this.errors++;
                if (this.errorSamples.length < this._maxErrors) this.errorSamples.push(span);
            } else if (this.samples.length < this._maxSamples) {
                this.samples.push(span);
            }

            this.endTime = Date.now();
        }

        toJSON() {
            return {
                Name: this.name,
                StartTime: this.startTime,
                EndTime: this.endTime,
                Total: this.total,
                Errors: this.errors,
                Cost: this.cost,
                MaxCost: this.maxCost,
                MinCost: this.minCost,
                Samples: this.samples.map(function (item) {
                    return item.toJSON();
                }),
                ErrorSamples: this.errorSamples.map(function (item) {
                    return item.toJSON();
                }),
            };
        }
    }

    /**
     * 浏览器星尘追踪器。
     */
    class StardustTracer {
        constructor(serverOrOptions, appId, secret) {
            var opts;
            if (typeof serverOrOptions === 'string') {
                opts = { server: serverOrOptions, appId: appId, secret: secret };
            } else {
                opts = serverOrOptions || {};
            }

            this._win = getRootWindow();
            this._doc = getWindowProperty(this._win, 'document');
            this._loc = getWindowProperty(this._win, 'location');
            this._nav = getWindowProperty(this._win, 'navigator');
            this._perf = getWindowProperty(this._win, 'performance');

            this.server = String(opts.server || '').replace(/\/+$/, '');
            this.appId = opts.appId === undefined || opts.appId === null ? '' : String(opts.appId);
            this.appName = opts.appName === undefined || opts.appName === null ? this.appId : String(opts.appName);
            this.secret = opts.secret === undefined || opts.secret === null ? '' : String(opts.secret);
            this.version = opts.version ? String(opts.version) : VERSION;
            this._tokenProvider = typeof opts.tokenProvider === 'function' ? opts.tokenProvider : null;

            this.clientId = this._resolveClientId(opts);

            this.period = positiveNumber(opts.period, 60);
            this.heartbeatPeriod = positiveNumber(opts.heartbeatPeriod, 30);
            this.maxSamples = positiveNumber(opts.maxSamples, 1);
            this.maxErrors = positiveNumber(opts.maxErrors, 10);
            this.maxTagLength = positiveNumber(opts.maxTagLength, 1024);
            this.requestTagLength = positiveNumber(opts.requestTagLength, 1024);
            this.timeout = positiveNumber(opts.timeout, 5000);
            this.enableMeter = opts.enableMeter !== false;
            this.autoInstrument = opts.autoInstrument !== false;
            this.tracePropagationTargets = opts.tracePropagationTargets === undefined ? null : opts.tracePropagationTargets;
            this.debug = opts.debug === true;
            this.excludes = normalizeExcludes(opts.excludes);

            this._token = '';
            this._tokenExpire = 0;
            this._cacheLoaded = false;

            this._builders = new Map();
            this._running = false;
            this._reportTimer = null;
            this._pingTimer = null;
            this._flushPromise = null;
            this._loginPromise = null;
            this._pingPromise = null;
            this._stopPromise = null;

            this._domListenersInstalled = false;
            this._onPageHide = null;
            this._onVisibilityChange = null;
            this._winEventTarget = null;
            this._lastVisiblePing = 0;

            this._instrumented = false;
            this._pageLoadEmitted = false;
            this._pageLoadListener = null;
            this._lastViewPath = null;
            this._routeListeners = null;
            this._errorListeners = null;
            this._spanStack = [];
            this._initialTraceparent = opts.initialTraceparent ? parseTraceparent(opts.initialTraceparent) : null;
            this._pageTraceId = this._initialTraceparent ? this._initialTraceparent.traceId : generateTraceId();

            var fetchImpl = null;
            try {
                if (this._win && typeof this._win.fetch === 'function') fetchImpl = this._win.fetch;
                else if (root && typeof root.fetch === 'function') fetchImpl = root.fetch;
            } catch (err) {
                fetchImpl = null;
            }
            this._fetchImpl = fetchImpl ? fetchImpl.bind(this._win || root) : null;
        }

        /**
         * 启动追踪器。
         * @returns {Promise<StardustTracer>} 当前实例
         */
        async start() {
            if (this._running) return this;
            this._running = true;

            this._installDomListeners();
            if (this.autoInstrument) installBrowserInterceptors(this);

            await this._ensureToken(false);
            this._scheduleTimers();
            this._log('started for ' + this.appId);

            return this;
        }

        /**
         * 停止追踪器并上报剩余数据。
         * @returns {Promise<void>} 完成
         */
        async stop() {
            if (this._stopPromise) return this._stopPromise;

            this._running = false;
            this._clearTimers();
            this._removeDomListeners();

            var self = this;
            this._stopPromise = this.flush().then(function () {
                self._stopPromise = null;
            }, function () {
                self._stopPromise = null;
            });

            return this._stopPromise;
        }

        /**
         * 创建手动 Span。
         * @param {String} name Span 名称
         * @param {String|Object} [parentId] 父 Span ID，或 { parentId, traceId } 配置对象
         * @returns {Object} Span
         */
        newSpan(name, parentId) {
            var tracer = this;
            var parent = '';
            var trace = '';

            if (typeof parentId === 'string') {
                parent = parentId;
            } else if (parentId && typeof parentId === 'object') {
                parent = parentId.parentId ? String(parentId.parentId) : '';
                trace = parentId.traceId ? String(parentId.traceId) : '';
            }

            if (!trace && this._spanStack.length > 0) {
                var current = this._spanStack[this._spanStack.length - 1];
                if (current) {
                    if (!parent) parent = current.id || '';
                    trace = current.traceId || '';
                }
            }
            if (!trace) trace = this._pageTraceId;

            var span = {
                id: generateSpanId(),
                parentId: parent,
                traceId: trace || generateTraceId(),
                name: String(name),
                startTime: Date.now(),
                endTime: 0,
                tag: '',
                error: '',
                _finished: false,
                setTag: function (tag) {
                    this.tag = tag === null || tag === undefined ? '' : String(tag);
                    return this;
                },
                setError: function (err) {
                    this.error = formatError(err);
                    return this;
                },
                finish: function () {
                    if (this._finished) return this;
                    this._finished = true;
                    this.endTime = Date.now();
                    tracer._finishSpan(this);
                    return this;
                },
                toJSON: function () {
                    return {
                        Id: this.id,
                        ParentId: this.parentId,
                        TraceId: this.traceId,
                        StartTime: this.startTime,
                        EndTime: this.endTime,
                        Tag: this.tag,
                        Error: this.error,
                    };
                },
            };

            Object.defineProperties(span, {
                Id: { get: function () { return this.id; }, enumerable: true },
                ParentId: { get: function () { return this.parentId; }, enumerable: true },
                TraceId: { get: function () { return this.traceId; }, enumerable: true },
                StartTime: { get: function () { return this.startTime; }, enumerable: true },
                EndTime: { get: function () { return this.endTime; }, enumerable: true },
                Tag: { get: function () { return this.tag; }, enumerable: true },
                Error: { get: function () { return this.error; }, enumerable: true },
            });

            return span;
        }

        /**
         * 在新建 Span 的上下文中执行函数（支持同步与 Promise）。
         * @param {String} name Span 名称
         * @param {Function} fn 业务函数，入参为 Span
         * @returns {*} fn 的返回值
         */
        withSpan(name, fn) {
            var self = this;
            var span = this.newSpan(name);
            var finished = false;

            var pop = function () {
                var index = self._spanStack.lastIndexOf(span);
                if (index >= 0) self._spanStack.splice(index, 1);
            };
            var finish = function (err) {
                if (finished) return;
                finished = true;
                pop();
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

            this._spanStack.push(span);

            var result;
            try {
                result = typeof fn === 'function' ? fn(span) : undefined;
            } catch (err) {
                finish(err);
                throw err;
            }

            if (result && typeof result.then === 'function') {
                return result.then(function (value) {
                    finish();
                    return value;
                }, function (err) {
                    finish(err);
                    throw err;
                });
            }

            finish();
            return result;
        }

        /**
         * 刷新并上报数据。并发调用串行执行。
         * @returns {Promise<void>} 完成
         */
        flush() {
            var self = this;
            var run = function () {
                return self._doFlush();
            };
            this._flushPromise = (this._flushPromise || Promise.resolve()).then(run, run);
            return this._flushPromise;
        }

        /**
         * 完成 Span 归入构建器。
         * @param {Object} span Span
         * @private
         */
        _finishSpan(span) {
            if (!span) return;

            var name = String(span.name || '');
            if (this._isIgnoredName(name)) return;

            var limit = this.maxTagLength;
            if (span.tag && span.tag.length > limit) span.tag = span.tag.substring(0, limit);

            var builder = this._builders.get(name);
            if (!builder) {
                builder = new BrowserSpanBuilder(name, this.maxSamples, this.maxErrors);
                this._builders.set(name, builder);
            } else {
                builder._maxSamples = this.maxSamples;
                builder._maxErrors = this.maxErrors;
            }

            builder.addSpan(span);
        }

        /**
         * 判断名称是否忽略。
         * @param {String} name 名称
         * @returns {Boolean} 是否忽略
         * @private
         */
        _isIgnoredName(name) {
            if (!name) return false;
            if (isIgnoredUrl(name)) return true;

            var lower = name.toLowerCase();
            for (var i = 0; i < this.excludes.length; i++) {
                var item = this.excludes[i];
                if (item && lower.indexOf(item.toLowerCase()) !== -1) return true;
            }
            return false;
        }

        /**
         * 解析或生成稳定 ClientId。
         * @param {Object} opts 配置
         * @returns {String} ClientId
         * @private
         */
        _resolveClientId(opts) {
            if (opts.clientId) return String(opts.clientId);

            var key = 'stardust.clientId.' + this.appId;
            var storage = getStorage(this._win, 'localStorage');
            if (storage) {
                try {
                    var stored = storage.getItem(key);
                    if (stored && /^browser@[0-9a-f]{32}$/i.test(String(stored))) {
                        memoryClientIds[this.appId] = String(stored);
                        return String(stored);
                    }
                } catch (err) {
                    // 存储不可用
                }
            }

            if (memoryClientIds[this.appId]) return memoryClientIds[this.appId];

            var id = 'browser@' + randomHex(32);
            memoryClientIds[this.appId] = id;
            if (storage) {
                try {
                    storage.setItem(key, id);
                } catch (err) {
                    // 存储不可用
                }
            }
            return id;
        }

        /**
         * 从 sessionStorage 读取缓存的令牌。
         * @returns {Object|null} { token, expire }
         * @private
         */
        _loadCachedToken() {
            var storage = getStorage(this._win, 'sessionStorage');
            if (!storage) return null;

            var key = 'stardust.token.' + this.appId;
            var raw = null;
            try {
                raw = storage.getItem(key);
            } catch (err) {
                return null;
            }
            if (!raw) return null;

            var token = '';
            var expire = 0;
            try {
                var obj = JSON.parse(raw);
                if (obj && typeof obj === 'object') {
                    token = obj.token || obj.Token || '';
                    expire = Number(obj.expire || obj.Expire || obj.expires || obj.expireAt) || 0;
                } else if (typeof obj === 'string') {
                    token = obj;
                }
            } catch (err) {
                token = String(raw);
            }
            if (!token) return null;

            if (expire > 0) {
                expire = expireToTimestamp(expire, 7200);
                if (expire - Date.now() <= TOKEN_REFRESH_WINDOW_MS) {
                    try {
                        storage.removeItem(key);
                    } catch (err) {
                        // 忽略
                    }
                    return null;
                }
            }

            return { token: String(token), expire: expire };
        }

        /**
         * 保存令牌到 sessionStorage。
         * @private
         */
        _saveCachedToken() {
            if (!this._token) return;
            var storage = getStorage(this._win, 'sessionStorage');
            if (!storage) return;

            try {
                storage.setItem('stardust.token.' + this.appId, JSON.stringify({
                    token: this._token,
                    expire: this._tokenExpire,
                }));
            } catch (err) {
                // 存储不可用
            }
        }

        /**
         * 清理令牌缓存。
         * @private
         */
        _clearCachedToken() {
            var storage = getStorage(this._win, 'sessionStorage');
            if (!storage) return;
            try {
                storage.removeItem('stardust.token.' + this.appId);
            } catch (err) {
                // 忽略
            }
        }

        /**
         * 确保令牌可用，5 分钟内过期时主动刷新。
         * @param {Boolean} force 强制刷新
         * @returns {Promise<String>} 令牌
         * @private
         */
        _ensureToken(force) {
            if (!this._cacheLoaded) {
                this._cacheLoaded = true;
                if (!this._token && !this._tokenProvider) {
                    var cached = this._loadCachedToken();
                    if (cached) {
                        this._token = cached.token;
                        this._tokenExpire = cached.expire;
                    }
                }
            }

            if (!force && this._token && (!this._tokenExpire || this._tokenExpire - Date.now() > TOKEN_REFRESH_WINDOW_MS)) {
                return Promise.resolve(this._token);
            }

            if (this._loginPromise) return this._loginPromise;

            var self = this;
            var promise = (async function () {
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
         * 登录或调用 tokenProvider。
         * @returns {Promise<String>} 令牌
         * @private
         */
        async _login() {
            try {
                if (this._tokenProvider) {
                    var result = await this._tokenProvider({
                        appId: this.appId,
                        appName: this.appName,
                        server: this.server,
                        version: this.version,
                        clientId: this.clientId,
                    });

                    var token = '';
                    var expire = 0;
                    if (typeof result === 'string') {
                        token = result;
                    } else if (result && typeof result === 'object') {
                        token = result.token || result.Token || '';
                        expire = result.expire || result.Expire || 0;
                    }
                    if (token) {
                        this._token = String(token);
                        this._tokenExpire = expireToTimestamp(expire, 7200);
                        this._saveCachedToken();
                    }
                    this._log('token obtained from provider');
                    return this._token;
                }

                var data = await this._post('/App/Login', {
                    AppId: this.appId,
                    Secret: this.secret,
                    ClientId: this.clientId,
                    AppName: this.appName,
                    Version: this.version,
                    IP: '',
                }, false);
                this._applyLoginResponse(data);
                this._log('login successful');
                return this._token;
            } catch (err) {
                this._log('login failed: ' + formatError(err));
                return '';
            }
        }

        /**
         * 应用登录响应。
         * @param {Object} data 响应数据
         * @private
         */
        _applyLoginResponse(data) {
            if (!data) return;

            var token = data.Token || data.token || '';
            if (data.Code) {
                var oldAppId = this.appId;
                this.appId = String(data.Code);
                if (this.appName === oldAppId) this.appName = this.appId;
            }
            if (data.Secret) this.secret = String(data.Secret);
            if (token) {
                this._token = String(token);
                var expire = Number(data.Expire || data.expire);
                this._tokenExpire = expireToTimestamp(expire, 7200);
                this._saveCachedToken();
            }
        }

        /**
         * 发送 POST 请求。
         * @param {String} path 路径
         * @param {*} payload 负载
         * @param {Boolean} withToken 是否附加 Token
         * @returns {Promise<*>} data
         * @private
         */
        async _post(path, payload, withToken) {
            if (!this._fetchImpl) throw new Error('fetch is not available');

            var url = this.server + path;
            if (withToken) url += '?Token=' + encodeURIComponent(this._token || '');

            var body = typeof payload === 'string' ? payload : JSON.stringify(payload);
            var response = await this._fetchImpl(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: body,
            });

            var status = response && typeof response.status === 'number' ? response.status : 0;
            if (status === 401) {
                var authErr = new Error('Unauthorized');
                authErr.code = 401;
                authErr.statusCode = 401;
                throw authErr;
            }

            var json = null;
            if (response && typeof response.json === 'function') {
                try {
                    json = await response.json();
                } catch (err) {
                    json = null;
                }
            } else if (response && typeof response === 'object' &&
                (response.code !== undefined || response.data !== undefined)) {
                json = response;
            }

            if (status >= 400) {
                var statusCode = json && json.code !== undefined ? Number(json.code) : status;
                var statusError = new Error(json && json.message ? json.message : ('HTTP ' + status));
                statusError.code = statusCode;
                statusError.statusCode = status;
                throw statusError;
            }

            if (!json) throw new Error('Invalid response');

            if (json.code !== undefined && Number(json.code) !== 0) {
                var apiError = new Error(json.message || ('Stardust API error ' + json.code));
                apiError.code = Number(json.code);
                apiError.statusCode = status;
                throw apiError;
            }

            return json.data === undefined ? null : json.data;
        }

        /**
         * 发送带鉴权请求，401 时重新登录并重试一次。
         * @param {String} path 路径
         * @param {*} payload 负载
         * @returns {Promise<*>} data
         * @private
         */
        async _requestWithAuth(path, payload) {
            await this._ensureToken(false);

            try {
                return await this._post(path, payload, true);
            } catch (err) {
                if (!isAuthError(err)) throw err;

                this._log('got 401, re-login and retry');
                this._token = '';
                this._tokenExpire = 0;
                this._clearCachedToken();
                await this._ensureToken(true);
                return this._post(path, payload, true);
            }
        }

        /**
         * 发送心跳。
         * @returns {Promise<void>} 完成
         * @private
         */
        _ping() {
            if (this._pingPromise) return this._pingPromise;

            var self = this;
            this._pingPromise = this._doPing().then(function () {
                self._pingPromise = null;
            }, function () {
                self._pingPromise = null;
            });
            return this._pingPromise;
        }

        /**
         * 执行心跳。
         * @returns {Promise<void>} 完成
         * @private
         */
        async _doPing() {
            try {
                var data = await this._requestWithAuth('/App/Ping', this._buildInfo());
                if (data) {
                    var token = data.Token || data.token;
                    if (token) {
                        this._token = String(token);
                        var expire = Number(data.Expire || data.expire);
                        if (expire > 0) this._tokenExpire = Date.now() + expire * 1000;
                        this._saveCachedToken();
                    }
                    this._applyResponse(data);
                }
                this._log('ping sent');
            } catch (err) {
                this._log('ping failed: ' + formatError(err));
            }
        }

        /**
         * 上报数据。
         * @returns {Promise<void>} 完成
         * @private
         */
        async _doFlush() {
            try {
                var builders = this._takeBuilders();
                if (builders.length === 0) return;

                var payload = this._buildReportPayload(builders);
                var data = await this._requestWithAuth('/Trace/Report', payload);

                if (data) this._applyResponse(data);
                this._log('report sent, builders=' + builders.length);
            } catch (err) {
                this._log('report failed: ' + formatError(err));
            }
        }

        /**
         * 取走当前构建器并清空。
         * @returns {Object[]} 构建器 JSON 列表
         * @private
         */
        _takeBuilders() {
            var list = [];
            this._builders.forEach(function (builder) {
                if (builder.total > 0) list.push(builder.toJSON());
            });
            this._builders.clear();
            return list;
        }

        /**
         * 构建上报负载。
         * @param {Object[]} builders 构建器列表
         * @returns {Object} 负载
         * @private
         */
        _buildReportPayload(builders) {
            var payload = {
                AppId: this.appId,
                AppName: this.appName,
                ClientId: this.clientId,
                Version: this.version,
                Builders: builders,
            };
            if (this.enableMeter) payload.Info = this._buildInfo();
            return payload;
        }

        /**
         * 构建浏览器 AppInfo。
         * @returns {Object} AppInfo
         * @private
         */
        _buildInfo() {
            var perf = this._perf;
            var nav = this._nav;
            var loc = this._loc;
            var memory = perf && perf.memory ? perf.memory : null;

            var timeOrigin = perf && typeof perf.timeOrigin === 'number' ? perf.timeOrigin : Date.now();
            var startTime;
            try {
                startTime = new Date(timeOrigin).toISOString();
            } catch (err) {
                startTime = new Date().toISOString();
            }

            var platform = '';
            if (nav) {
                if (nav.userAgentData && nav.userAgentData.platform) platform = String(nav.userAgentData.platform);
                else if (nav.platform) platform = String(nav.platform);
            }

            return {
                Id: 0,
                Name: this.appName,
                Version: this.version,
                CommandLine: nav && nav.userAgent ? String(nav.userAgent) : '',
                UserName: '',
                MachineName: platform,
                IP: '',
                StartTime: startTime,
                ProcessorTime: 0,
                CpuUsage: 0,
                WorkingSet: memory && memory.usedJSHeapSize ? memory.usedJSHeapSize : 0,
                HeapSize: memory && memory.totalJSHeapSize ? memory.totalJSHeapSize : 0,
                Threads: nav && nav.hardwareConcurrency ? nav.hardwareConcurrency : 0,
                Handles: 0,
                Connections: 0,
                GCCount: 0,
                Listens: loc && loc.origin ? String(loc.origin) : '',
                Time: Date.now(),
            };
        }

        /**
         * 应用服务端参数。
         * @param {Object} result 响应数据
         * @private
         */
        _applyResponse(result) {
            if (!result || typeof result !== 'object') return;

            var oldPeriod = this.period;

            if (Number(result.Period) > 0) this.period = Number(result.Period);
            if (Number(result.MaxSamples) > 0) this.maxSamples = Number(result.MaxSamples);
            if (Number(result.MaxErrors) > 0) this.maxErrors = Number(result.MaxErrors);
            if (Number(result.Timeout) > 0) this.timeout = Number(result.Timeout);
            if (Number(result.MaxTagLength) > 0) this.maxTagLength = Number(result.MaxTagLength);
            if (Number(result.RequestTagLength) > 0) this.requestTagLength = Number(result.RequestTagLength);
            if (hasOwn(result, 'EnableMeter') && result.EnableMeter !== null) this.enableMeter = result.EnableMeter !== false;

            if (Array.isArray(result.Excludes)) {
                this.excludes = result.Excludes.map(function (item) {
                    return String(item);
                });
            } else if (typeof result.Excludes === 'string') {
                this.excludes = normalizeExcludes(result.Excludes);
            } else if (hasOwn(result, 'Excludes') && result.Excludes === null) {
                this.excludes = [];
            }

            this._builders.forEach(function (builder) {
                builder._maxSamples = this.maxSamples;
                builder._maxErrors = this.maxErrors;
            }, this);

            if (this._running && oldPeriod !== this.period) {
                this._log('period changed to ' + this.period + 's');
                this._scheduleTimers();
            }
        }

        /**
         * 安装定时器。
         * @private
         */
        _scheduleTimers() {
            this._clearTimers();
            if (!this._running) return;

            var self = this;
            this._reportTimer = setInterval(function () {
                self.flush();
            }, Math.max(1, this.period) * 1000);
            this._pingTimer = setInterval(function () {
                if (!self._isHidden()) self._ping();
            }, Math.max(1, this.heartbeatPeriod) * 1000);
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
         * 页面是否隐藏。
         * @returns {Boolean} 是否隐藏
         * @private
         */
        _isHidden() {
            var doc = this._doc;
            if (!doc) return false;
            if (doc.hidden) return true;
            if (doc.visibilityState === 'hidden') return true;
            return false;
        }

        /**
         * 安装页面生命周期监听。
         * @private
         */
        _installDomListeners() {
            if (this._domListenersInstalled) return;
            this._domListenersInstalled = true;

            var self = this;
            this._onPageHide = function () {
                self._beaconFlush();
            };
            this._onVisibilityChange = function () {
                if (self._isHidden()) {
                    self._beaconFlush();
                } else if (self._running) {
                    var now = Date.now();
                    if (!self._lastVisiblePing || now - self._lastVisiblePing > 1000) {
                        self._lastVisiblePing = now;
                        self._ping();
                    }
                }
            };

            try {
                var winTarget = getEventTarget(this._win);
                if (winTarget) {
                    this._winEventTarget = winTarget;
                    winTarget.addEventListener('pagehide', this._onPageHide, false);
                    winTarget.addEventListener('visibilitychange', this._onVisibilityChange, false);
                }
            } catch (err) {
                // 忽略
            }
            try {
                if (this._doc && typeof this._doc.addEventListener === 'function') {
                    this._doc.addEventListener('pagehide', this._onPageHide, false);
                    this._doc.addEventListener('visibilitychange', this._onVisibilityChange, false);
                }
            } catch (err) {
                // 忽略
            }
        }

        /**
         * 移除页面生命周期监听。
         * @private
         */
        _removeDomListeners() {
            if (!this._domListenersInstalled) return;
            this._domListenersInstalled = false;

            try {
                if (this._winEventTarget && typeof this._winEventTarget.removeEventListener === 'function') {
                    this._winEventTarget.removeEventListener('pagehide', this._onPageHide, false);
                    this._winEventTarget.removeEventListener('visibilitychange', this._onVisibilityChange, false);
                }
                this._winEventTarget = null;
            } catch (err) {
                // 忽略
            }
            try {
                if (this._doc && typeof this._doc.removeEventListener === 'function') {
                    this._doc.removeEventListener('pagehide', this._onPageHide, false);
                    this._doc.removeEventListener('visibilitychange', this._onVisibilityChange, false);
                }
            } catch (err) {
                // 忽略
            }
        }

        /**
         * 使用 sendBeacon 上报，失败时退回 fetch keepalive。
         * @private
         */
        _beaconFlush() {
            try {
                var builders = this._takeBuilders();
                if (builders.length === 0) return;

                var payload = this._buildReportPayload(builders);
                var json = JSON.stringify(payload);
                var url = this.server + '/Trace/Report?Token=' + encodeURIComponent(this._token || '');

                var nav = this._nav;
                var BlobCtor = getBlobConstructor(this._win);
                if (nav && typeof nav.sendBeacon === 'function' && BlobCtor) {
                    var sent = false;
                    try {
                        sent = nav.sendBeacon(url, new BlobCtor([json], { type: 'application/json' }));
                    } catch (err) {
                        sent = false;
                    }
                    if (sent !== false) return;
                }

                if (this._fetchImpl) {
                    try {
                        var promise = this._fetchImpl(url, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: json,
                            keepalive: true,
                        });
                        if (promise && typeof promise.catch === 'function') promise.catch(function () { });
                    } catch (err) {
                        // 忽略回退失败
                    }
                }
            } catch (err) {
                this._log('beacon flush failed: ' + formatError(err));
            }
        }

        /**
         * 标签长度限制。
         * @returns {Number} 限制
         * @private
         */
        _tagLimit() {
            var limit = this.maxTagLength;
            if (this.requestTagLength > 0 && this.requestTagLength < limit) limit = this.requestTagLength;
            return limit > 0 ? limit : 1024;
        }

        /**
         * 调试日志。
         * @param {String} message 消息
         * @private
         */
        _log(message) {
            if (this.debug && typeof console !== 'undefined' && console.log) console.log('[Stardust] ' + message);
        }
    }

    /**
     * 构造带 traceparent 的 fetch init。
     * @param {Object} win 窗口
     * @param {*} input fetch 入参
     * @param {Object} init 原始 init
     * @param {Object} span 出站 Span
     * @returns {Object} 新 init
     */
    function buildFetchInit(win, input, init, span) {
        var value = formatTraceparent(span.traceId, span.id, true);
        var cloned = assign({}, init);
        var HeadersCtor = getWindowProperty(win, 'Headers');
        if (typeof HeadersCtor !== 'function' && root && typeof root.Headers === 'function') HeadersCtor = root.Headers;

        if (typeof HeadersCtor === 'function') {
            try {
                var headers = new HeadersCtor();
                if (input && typeof input === 'object' && input.headers && typeof input.headers.forEach === 'function') {
                    input.headers.forEach(function (headerValue, headerName) {
                        headers.set(headerName, headerValue);
                    });
                }
                if (init && init.headers) {
                    var own = new HeadersCtor(init.headers);
                    own.forEach(function (headerValue, headerName) {
                        headers.set(headerName, headerValue);
                    });
                }
                if (!headers.has('traceparent')) headers.set('traceparent', value);
                cloned.headers = headers;
                return cloned;
            } catch (err) {
                // 回退到普通对象
            }
        }

        var plain = {};
        if (input && typeof input === 'object' && input.headers && typeof input.headers.forEach === 'function' && !(init && init.headers)) {
            try {
                input.headers.forEach(function (headerValue, headerName) {
                    plain[headerName] = headerValue;
                });
            } catch (err) {
                // 忽略
            }
        }
        var ownHeaders = init && init.headers;
        if (ownHeaders && !Array.isArray(ownHeaders) && typeof ownHeaders === 'object') {
            for (var key in ownHeaders) {
                if (hasOwn(ownHeaders, key)) plain[key] = ownHeaders[key];
            }
        }
        if (!hasTraceparent(plain)) plain.traceparent = value;
        cloned.headers = plain;
        return cloned;
    }

    /**
     * 安装 fetch 拦截器。
     * @param {StardustTracer} tracer 追踪器
     */
    function installFetchInterceptor(tracer) {
        try {
            var win = tracer._win || getRootWindow();
            var target = win;
            if (!target || typeof target.fetch !== 'function') {
                target = root && typeof root.fetch === 'function' ? root : null;
            }
            if (!target || typeof target.fetch !== 'function') return;

            var original = target.fetch;
            if (original.__stardustWrapped) return;

            var wrapped = function (input, init) {
                var url = '';
                var method = 'GET';
                var span = null;
                var actualInit = init;
                try {
                    url = resolveAbsoluteUrl(resolveRequestUrl(input), win);
                    method = String((init && init.method) || (input && input.method) || 'GET').toUpperCase();
                    if (!isIgnoredUrl(url)) {
                        span = tracer.newSpan(buildSpanName(method, url, win));
                        span.setTag(truncate(url, tracer._tagLimit()));
                        if (shouldPropagateTrace(tracer, url)) actualInit = buildFetchInit(win, input, init, span);
                    }
                } catch (err) {
                    span = null;
                    tracer._log('fetch instrumentation failed: ' + formatError(err));
                }

                var result;
                try {
                    result = span ? original.call(this, input, actualInit) : original.apply(this, arguments);
                } catch (err) {
                    if (span) {
                        try {
                            span.setError(err);
                            span.finish();
                        } catch (ignore) {
                            // 忽略
                        }
                    }
                    throw err;
                }

                if (span && result && typeof result.then === 'function') {
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
                        } catch (ignore) {
                            // 忽略
                        }
                        try {
                            span.finish();
                        } catch (ignore) {
                            // 忽略
                        }
                        throw err;
                    });
                }

                if (span) {
                    try {
                        span.finish();
                    } catch (err) {
                        // 忽略
                    }
                }
                return result;
            };

            wrapped.__stardustWrapped = true;
            target.fetch = wrapped;
            if (win && win !== target) {
                try {
                    win.fetch = wrapped;
                } catch (err) {
                    // 忽略
                }
            }
        } catch (err) {
            tracer._log('install fetch failed: ' + formatError(err));
        }
    }

    /**
     * 安装 XMLHttpRequest 拦截器。
     * @param {StardustTracer} tracer 追踪器
     */
    function installXHRInterceptor(tracer) {
        try {
            var win = tracer._win || getRootWindow();
            var target = win;
            if (!target || !target.XMLHttpRequest) {
                target = root && root.XMLHttpRequest ? root : null;
            }
            if (!target || !target.XMLHttpRequest) return;

            var XHR = target.XMLHttpRequest;
            if (!XHR.prototype || XHR.prototype.__stardustWrapped) return;

            var originalOpen = XHR.prototype.open;
            var originalSend = XHR.prototype.send;
            var originalSetHeader = XHR.prototype.setRequestHeader;

            if (typeof originalSetHeader === 'function') {
                XHR.prototype.setRequestHeader = function (name) {
                    try {
                        if (!this.__stardustHeaders) this.__stardustHeaders = Object.create(null);
                        this.__stardustHeaders[String(name).toLowerCase()] = true;
                    } catch (err) {
                        // 忽略
                    }
                    return originalSetHeader.apply(this, arguments);
                };
            }

            XHR.prototype.open = function (method, url) {
                try {
                    this.__stardustMethod = method;
                    this.__stardustUrl = url;
                    this.__stardustHeaders = Object.create(null);
                } catch (err) {
                    // 忽略
                }
                return originalOpen.apply(this, arguments);
            };

            XHR.prototype.send = function () {
                var xhr = this;
                var span = null;
                var finish = null;

                try {
                    var absolute = resolveAbsoluteUrl(xhr.__stardustUrl, win);
                    if (!isIgnoredUrl(absolute)) {
                        var method = String(xhr.__stardustMethod || 'GET').toUpperCase();
                        span = tracer.newSpan(buildSpanName(method, absolute, win));
                        span.setTag(truncate(absolute, tracer._tagLimit()));

                        try {
                            var sentHeaders = xhr.__stardustHeaders || {};
                            if (shouldPropagateTrace(tracer, absolute) && !sentHeaders.traceparent && typeof xhr.setRequestHeader === 'function') {
                                xhr.setRequestHeader('traceparent', formatTraceparent(span.traceId, span.id, true));
                            }
                        } catch (err) {
                            // 忽略头注入异常
                        }

                        var finished = false;
                        finish = function (error) {
                            if (finished) return;
                            finished = true;
                            try {
                                if (error) span.setError(error);
                            } catch (err) {
                                // 忽略
                            }
                            try {
                                span.finish();
                            } catch (err) {
                                // 忽略
                            }
                        };

                        var onLoad = function () {
                            if (xhr.status >= 400) finish('HTTP ' + xhr.status);
                            else finish();
                        };
                        var onError = function () {
                            finish('Network Error');
                        };
                        var onAbort = function () {
                            finish('Request Aborted');
                        };
                        var onTimeout = function () {
                            finish('Request Timeout');
                        };

                        if (typeof xhr.addEventListener === 'function') {
                            xhr.addEventListener('load', onLoad);
                            xhr.addEventListener('error', onError);
                            xhr.addEventListener('abort', onAbort);
                            xhr.addEventListener('timeout', onTimeout);
                        }
                    }
                } catch (err) {
                    tracer._log('xhr instrumentation failed: ' + formatError(err));
                }

                try {
                    return originalSend.apply(this, arguments);
                } catch (err) {
                    if (finish) finish(err);
                    throw err;
                }
            };

            XHR.prototype.__stardustWrapped = true;
            if (win && win !== target && !win.XMLHttpRequest) {
                try {
                    win.XMLHttpRequest = XHR;
                } catch (err) {
                    // 忽略
                }
            }
        } catch (err) {
            tracer._log('install xhr failed: ' + formatError(err));
        }
    }

    /**
     * 安装页面加载埋点。
     * @param {StardustTracer} tracer 追踪器
     */
    function installPageLoadInstrumentation(tracer) {
        try {
            var perf = tracer._perf || getWindowProperty(tracer._win, 'performance');
            if (!perf || tracer._pageLoadEmitted) return;

            var emit = function () {
                if (tracer._pageLoadEmitted) return;

                try {
                    var entries = typeof perf.getEntriesByType === 'function'
                        ? perf.getEntriesByType('navigation')
                        : null;
                    var entry = entries && entries.length > 0 ? entries[0] : null;
                    if (!entry) return;

                    var ready = tracer._doc && tracer._doc.readyState === 'complete';
                    if (!entry.loadEventEnd && !entry.domContentLoadedEventEnd && !ready) return;

                    tracer._pageLoadEmitted = true;
                    var initial = tracer._initialTraceparent;
                    var span = initial
                        ? tracer.newSpan('page.load', { parentId: initial.spanId, traceId: initial.traceId })
                        : tracer.newSpan('page.load');
                    span.setTag(buildNavigationTag(entry));
                    span.finish();
                } catch (err) {
                    tracer._log('page load instrumentation failed: ' + formatError(err));
                }
            };

            var entries = typeof perf.getEntriesByType === 'function' ? perf.getEntriesByType('navigation') : null;
            var entry = entries && entries.length > 0 ? entries[0] : null;
            var ready = tracer._doc && tracer._doc.readyState === 'complete';
            if (entry && (entry.loadEventEnd > 0 || entry.domContentLoadedEventEnd > 0 || ready)) {
                emit();
                return;
            }

            var loadTarget = getEventTarget(tracer._win);
            if (loadTarget) {
                var onLoad = function () {
                    try {
                        loadTarget.removeEventListener('load', onLoad, false);
                    } catch (err) {
                        // 忽略
                    }
                    tracer._pageLoadListener = null;
                    emit();
                };
                tracer._pageLoadListener = onLoad;
                loadTarget.addEventListener('load', onLoad, false);
            }
        } catch (err) {
            tracer._log('install page load failed: ' + formatError(err));
        }
    }

    /**
     * 安装 SPA 路由变化埋点。
     * @param {StardustTracer} tracer 追踪器
     */
    function installRouteInstrumentation(tracer) {
        try {
            var win = tracer._win || getRootWindow();
            if (!win || (!win.history && typeof win.addEventListener !== 'function')) win = getEventTarget(tracer._win) || root;
            var loc = tracer._loc || getWindowProperty(win, 'location');
            var initial = loc && loc.pathname ? String(loc.pathname) : '/';
            if (tracer._lastViewPath === null || tracer._lastViewPath === undefined) tracer._lastViewPath = initial;

            var emit = function (path) {
                try {
                    var next = path || '/';
                    if (next === tracer._lastViewPath) return;
                    tracer._lastViewPath = next;

                    var span = tracer.newSpan('page.view ' + next);
                    span.setTag(truncate(getHref(win), tracer._tagLimit()));
                    span.finish();
                } catch (err) {
                    tracer._log('route instrumentation failed: ' + formatError(err));
                }
            };

            var history = win ? win.history : null;
            if (history) {
                var wrap = function (name) {
                    var original = history[name];
                    if (typeof original !== 'function' || original.__stardustWrapped) return;

                    var wrapped = function (state, title, url) {
                        var result = original.apply(this, arguments);
                        try {
                            var path = loc && loc.pathname ? String(loc.pathname) : '/';
                            if (url) {
                                try {
                                    path = new URL(String(url), getHref(win)).pathname;
                                } catch (err) {
                                    // 使用当前位置
                                }
                            }
                            emit(path);
                        } catch (err) {
                            tracer._log('route change failed: ' + formatError(err));
                        }
                        return result;
                    };
                    wrapped.__stardustWrapped = true;
                    try {
                        history[name] = wrapped;
                    } catch (err) {
                        // 忽略
                    }
                };
                wrap('pushState');
                wrap('replaceState');
            }

            if (win && typeof win.addEventListener === 'function') {
                var onPop = function () {
                    var current = loc && loc.pathname ? String(loc.pathname) : '/';
                    emit(current);
                };
                win.addEventListener('popstate', onPop, false);
                win.addEventListener('hashchange', onPop, false);
                tracer._routeListeners = { popstate: onPop, hashchange: onPop };
            }
        } catch (err) {
            tracer._log('install route failed: ' + formatError(err));
        }
    }

    /**
     * 安装 JS 错误与资源错误埋点。
     * @param {StardustTracer} tracer 追踪器
     */
    function installErrorInstrumentation(tracer) {
        try {
            var win = getEventTarget(tracer._win);
            if (!win) return;

            var onError = function (event) {
                try {
                    var target = event && event.target;
                    var tagName = target && (target.tagName || target.nodeName);
                    var isResource = target && target !== win && tagName;

                    if (isResource) {
                        var resourceUrl = target.currentSrc || target.src || target.href || '';
                        var resourceSpan = tracer.newSpan('resource.error');
                        resourceSpan.setError('Resource failed: ' + String(tagName) + ' ' + resourceUrl);
                        resourceSpan.setTag(truncate(resourceUrl || getHref(win), tracer._tagLimit()));
                        resourceSpan.finish();
                        return;
                    }

                    var err = event && event.error;
                    var message = err ? formatError(err) : ((event && event.message) || 'Unknown script error');
                    var source = (event && event.filename) || '';
                    var line = (event && event.lineno) || 0;
                    var col = (event && event.colno) || 0;
                    var tag = 'source=' + source + ';line=' + line + ';col=' + col;
                    if (err && err.stack) tag += '\n' + String(err.stack);

                    var span = tracer.newSpan('js.error');
                    span.setError(message);
                    span.setTag(truncate(tag, tracer._tagLimit()));
                    span.finish();
                } catch (e) {
                    tracer._log('error instrumentation failed: ' + formatError(e));
                }
            };

            var onRejection = function (event) {
                try {
                    var reason = event && event.reason;
                    var span = tracer.newSpan('js.error');
                    span.setError(reason ? formatError(reason) : 'Unhandled promise rejection');

                    var tag = 'type=unhandledrejection';
                    if (reason && reason.stack) tag += '\n' + String(reason.stack);
                    else if (typeof reason === 'string') tag += '\n' + reason;
                    span.setTag(truncate(tag, tracer._tagLimit()));
                    span.finish();
                } catch (e) {
                    tracer._log('rejection instrumentation failed: ' + formatError(e));
                }
            };

            win.addEventListener('error', onError, true);
            win.addEventListener('unhandledrejection', onRejection, false);
            tracer._errorListeners = { error: onError, unhandledrejection: onRejection };
        } catch (err) {
            tracer._log('install error failed: ' + formatError(err));
        }
    }

    /**
     * 安装全部浏览器拦截器。
     * @param {StardustTracer} tracer 追踪器
     */
    function installBrowserInterceptors(tracer) {
        if (!tracer || tracer._instrumented) return;
        tracer._instrumented = true;

        try {
            installFetchInterceptor(tracer);
        } catch (err) {
            tracer._log('install fetch failed: ' + formatError(err));
        }
        try {
            installXHRInterceptor(tracer);
        } catch (err) {
            tracer._log('install xhr failed: ' + formatError(err));
        }
        try {
            installPageLoadInstrumentation(tracer);
        } catch (err) {
            tracer._log('install page load failed: ' + formatError(err));
        }
        try {
            installRouteInstrumentation(tracer);
        } catch (err) {
            tracer._log('install route failed: ' + formatError(err));
        }
        try {
            installErrorInstrumentation(tracer);
        } catch (err) {
            tracer._log('install error failed: ' + formatError(err));
        }

        tracer._log('browser interceptors installed');
    }

    /**
     * 创建浏览器追踪器。
     * @param {Object|String} options 配置对象或服务器地址
     * @param {String} [appId] 应用ID
     * @param {String} [secret] 应用密钥
     * @returns {StardustTracer} 追踪器
     */
    function createTracer(options, appId, secret) {
        return new StardustTracer(options, appId, secret);
    }

    var defaultTracer = null;

    /**
     * 创建并启动浏览器追踪器。适合 npm 包的一行接入。
     * @param {Object|String} options 配置对象或服务器地址
     * @param {String} [appId] 应用ID
     * @param {String} [secret] 应用密钥
     * @returns {StardustTracer} 追踪器
     */
    function init(options, appId, secret) {
        var tracer = createTracer(options, appId, secret);
        defaultTracer = tracer;
        try {
            if (root && root.Stardust) root.Stardust.tracer = tracer;
        } catch (err) {
            // 忽略只读全局对象
        }
        tracer.start();
        return tracer;
    }

    /**
     * 手动上报一个异常。
     * @param {Error|String} err 异常
     * @param {Object} [options] { tag }
     * @returns {Object|null} Span
     */
    function captureException(err, options) {
        var tracer = defaultTracer || (root && root.Stardust ? root.Stardust.tracer : null);
        if (!tracer || typeof tracer.newSpan !== 'function') return null;

        var span = tracer.newSpan('js.error');
        if (options && options.tag) span.setTag(options.tag);
        span.setError(err);
        span.finish();
        return span;
    }

    /**
     * 立即上报当前聚合数据。
     * @returns {Promise<void>} 完成
     */
    function flush() {
        var tracer = defaultTracer || (root && root.Stardust ? root.Stardust.tracer : null);
        return tracer && typeof tracer.flush === 'function' ? tracer.flush() : Promise.resolve();
    }

    return {
        StardustTracer: StardustTracer,
        createTracer: createTracer,
        init: init,
        captureException: captureException,
        flush: flush,
        version: VERSION,
        installBrowserInterceptors: installBrowserInterceptors,
        installFetchInterceptor: installFetchInterceptor,
        installXHRInterceptor: installXHRInterceptor,
        parseTraceparent: parseTraceparent,
        formatTraceparent: formatTraceparent,
        generateTraceId: generateTraceId,
        generateSpanId: generateSpanId,
    };
});
