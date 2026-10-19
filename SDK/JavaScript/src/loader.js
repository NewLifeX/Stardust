/**
 * Stardust Browser Loader
 *
 * 一行接入的浏览器引导脚本。读取自身 <script> 标签上的 data-* 配置，
 * 动态加载 browser.js，然后创建并启动 Stardust 追踪器。
 *
 * 推荐用法：
 * <script src="https://cdn.example.com/stardust/loader.js"
 *         data-app-id="MyWeb"
 *         data-config-url="/stardust/browser-config"
 *         defer></script>
 *
 * 所有配置都来自公开的 data-* 属性或后端配置端点，不要在页面中写 secret。
 */
(function (root, factory) {
    'use strict';

    var api = factory();

    if (typeof module === 'object' && module.exports) {
        module.exports = api;
        return;
    }

    api.bootstrap(root, root && root.document);
})(typeof globalThis !== 'undefined' ? globalThis : (typeof window !== 'undefined' ? window : this), function () {
    'use strict';

    var META_TRACEPARENT = 'stardust-traceparent';

    /**
     * 读取当前 script 标签。defer/async 下 currentScript 可能为空，回退到最后一个 script。
     * @param {Object} doc document
     * @returns {Object|null} script 元素
     */
    function currentScript(doc) {
        if (!doc) return null;
        if (doc.currentScript) return doc.currentScript;

        var scripts = doc.getElementsByTagName ? doc.getElementsByTagName('script') : null;
        return scripts && scripts.length > 0 ? scripts[scripts.length - 1] : null;
    }

    /**
     * 读取 script 上的配置。
     * @param {Object} script script 元素
     * @param {Object} doc document
     * @returns {Object} 配置
     */
    function readConfig(script, doc) {
        var get = function (name, legacy) {
            var value = script && script.getAttribute ? script.getAttribute('data-stardust-' + name) : null;
            if ((value === null || value === undefined || value === '') && legacy && script && script.getAttribute) {
                value = script.getAttribute('data-' + legacy);
            }
            return value === null || value === undefined || value === '' ? null : String(value);
        };
        var bool = function (name, legacy, fallback) {
            var value = get(name, legacy);
            if (value === null) return fallback;
            return value === '1' || value === 'true' || value === 'yes';
        };

        var traceparent = get('traceparent');
        if (!traceparent) {
            var meta = doc && doc.querySelector ? doc.querySelector('meta[name="' + META_TRACEPARENT + '"], meta[name="traceparent"]') : null;
            if (meta && meta.getAttribute) traceparent = meta.getAttribute('content');
        }

        return {
            appId: get('app', 'app-id'),
            appName: get('app-name'),
            server: get('server'),
            configUrl: get('config', 'config-url'),
            token: get('token-value'),
            tokenUrl: get('token', 'token-url'),
            expire: get('expire'),
            version: get('release', 'version'),
            environment: get('env'),
            sampleRate: get('sample'),
            tracePropagationTargets: get('trace-targets'),
            debug: bool('debug', null, false),
            autoInstrument: bool('auto-instrument', null, true),
            traceparent: traceparent,
            src: get('src'),
            scriptSrc: script && script.src ? String(script.src) : ''
        };
    }

    /**
     * 计算 browser.js 地址。默认与 loader.js 同目录。
     * @param {Object} config 配置
     * @returns {String} browser.js 地址
     */
    function resolveSrc(config) {
        if (config && config.src) return config.src;
        var src = config && config.scriptSrc ? String(config.scriptSrc) : '';
        if (!src) return 'browser.js';

        return src.replace(/loader\.js(\?.*)?$/i, function (match, query) {
            return 'browser.js' + (query || '');
        });
    }

    /**
     * 把后端配置端点的返回值归一化为 tracer 配置。
     * @param {Object} config 当前配置
     * @param {Object} remote 后端配置
     * @returns {Object} 合并后的配置
     */
    function mergeConfig(config, remote) {
        var merged = {};
        var key;
        if (config) {
            for (key in config) {
                if (Object.prototype.hasOwnProperty.call(config, key)) merged[key] = config[key];
            }
        }
        if (remote && typeof remote === 'object') {
            for (key in remote) {
                if (Object.prototype.hasOwnProperty.call(remote, key)) merged[key] = remote[key];
            }
        }
        return merged;
    }

    /**
     * 拉取后端配置。兼容统一响应 { code, data } 与直接对象。
     * @param {Object} root window
     * @param {String} url 配置地址
     * @returns {Promise<Object>} 配置
     */
    function fetchConfig(root, url) {
        var fetchImpl = root && typeof root.fetch === 'function' ? root.fetch : null;
        if (!fetchImpl) return Promise.reject(new Error('fetch is not available'));

        return fetchImpl(url, { credentials: 'same-origin' }).then(function (response) {
            if (!response || !response.ok) throw new Error('stardust config HTTP ' + (response && response.status));
            return response.json();
        }).then(function (body) {
            if (body && typeof body === 'object' && Object.prototype.hasOwnProperty.call(body, 'code')) {
                if (Number(body.code) !== 0) throw new Error(body.message || 'stardust config error');
                return body.data || {};
            }
            return body || {};
        });
    }

    /**
     * 创建并启动追踪器。
     * @param {Object} root window
     * @param {Object} config 配置
     * @returns {Object} tracer
     */
    function start(root, config) {
        if (!root || !config || !config.appId) throw new Error('stardust appId is required');
        if (root.__stardustTracer) return root.__stardustTracer;
        if (!root.Stardust || typeof root.Stardust.createTracer !== 'function') {
            throw new Error('stardust browser SDK is not loaded');
        }

        var targets = config.tracePropagationTargets;
        if (typeof targets === 'string') {
            targets = targets.split(',').map(function (item) {
                return String(item).replace(/^\s+|\s+$/g, '');
            }).filter(function (item) {
                return !!item;
            });
        }

        var options = {
            appId: config.appId,
            appName: config.appName || undefined,
            server: config.server || undefined,
            version: config.version || undefined,
            debug: config.debug === true,
            autoInstrument: config.autoInstrument !== false,
            tracePropagationTargets: targets,
            initialTraceparent: config.traceparent || undefined
        };

        var tokenUrl = config.tokenUrl;
        if (tokenUrl) {
            options.tokenProvider = function () {
                return root.fetch(tokenUrl, { credentials: 'same-origin' }).then(function (response) {
                    if (!response || !response.ok) throw new Error('stardust token HTTP ' + (response && response.status));
                    return response.json();
                }).then(function (body) {
                    if (body && typeof body === 'object' && Object.prototype.hasOwnProperty.call(body, 'code')) {
                        if (Number(body.code) !== 0) throw new Error(body.message || 'stardust token error');
                        body = body.data || {};
                    }
                    return body && body.token ? { token: body.token, expire: body.expire } : body;
                });
            };
        } else if (config.token) {
            options.tokenProvider = function () {
                return { token: config.token, expire: config.expire };
            };
        }

        var tracer = root.Stardust.createTracer(options);
        tracer.start();
        root.__stardustTracer = tracer;
        try {
            root.Stardust.tracer = tracer;
        } catch (err) {
            // 忽略只读全局对象
        }
        return tracer;
    }

    /**
     * 动态加载 browser.js。
     * @param {Object} doc document
     * @param {String} src 地址
     * @returns {Promise<void>} 完成
     */
    function loadBrowser(doc, src) {
        return new Promise(function (resolve, reject) {
            if (!doc || !doc.createElement) {
                reject(new Error('document is not available'));
                return;
            }

            var script = doc.createElement('script');
            script.src = src;
            script.async = true;
            script.onload = function () { resolve(); };
            script.onerror = function () { reject(new Error('failed to load ' + src)); };
            (doc.head || doc.documentElement).appendChild(script);
        });
    }

    /**
     * 引导入口。
     * @param {Object} root window
     * @param {Object} doc document
     * @returns {Promise<Object|null>} tracer
     */
    function bootstrap(root, doc) {
        if (!root || !doc) return Promise.resolve(null);

        var script = currentScript(doc);
        var config = readConfig(script, doc);
        if (!config.appId && !config.configUrl) {
            if (config.debug && root.console) root.console.warn('[Stardust] data-app-id or data-config-url is required');
            return Promise.resolve(null);
        }

        var ready = root.Stardust && typeof root.Stardust.createTracer === 'function'
            ? Promise.resolve()
            : loadBrowser(doc, resolveSrc(config));

        return ready.then(function () {
            if (config.configUrl) {
                return fetchConfig(root, config.configUrl).then(function (remote) {
                    return mergeConfig(config, remote);
                }, function () {
                    // 配置端点不可用时回退到 data-stardust-* 与默认值，不阻塞页面
                    return config;
                });
            }
            return config;
        }).then(function (merged) {
            return start(root, merged);
        }).catch(function (err) {
            if (config.debug && root.console) root.console.warn('[Stardust] bootstrap failed: ' + (err && err.message ? err.message : err));
            return null;
        });
    }

    return {
        bootstrap: bootstrap,
        readConfig: readConfig,
        resolveSrc: resolveSrc,
        mergeConfig: mergeConfig,
        start: start
    };
});
