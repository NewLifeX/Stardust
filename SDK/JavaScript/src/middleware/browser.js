/**
 * 浏览器环境中间件
 * 自动拦截fetch和XMLHttpRequest请求
 */

/**
 * 安装fetch拦截器
 * @param {StardustBrowserTracer} tracer 浏览器追踪器实例
 */
function installFetchInterceptor(tracer) {
    if (typeof window === 'undefined' || !window.fetch) return;

    const originalFetch = window.fetch;
    
    window.fetch = function (...args) {
        const [url, options = {}] = args;
        const method = (options.method || 'GET').toUpperCase();
        
        // 解析URL
        let urlStr = url;
        if (url instanceof Request) {
            urlStr = url.url;
        }
        
        const name = `${method} ${urlStr}`;
        const span = tracer.newSpan(name);
        span.setTag(`${method} ${urlStr}`);

        return originalFetch.apply(this, args)
            .then(response => {
                if (response.status >= 400) {
                    span.setError(`HTTP ${response.status}`);
                }
                span.finish();
                return response;
            })
            .catch(err => {
                span.setError(err);
                span.finish();
                throw err;
            });
    };
}

/**
 * 安装XMLHttpRequest拦截器
 * @param {StardustBrowserTracer} tracer 浏览器追踪器实例
 */
function installXHRInterceptor(tracer) {
    if (typeof window === 'undefined' || !window.XMLHttpRequest) return;

    const originalOpen = XMLHttpRequest.prototype.open;
    const originalSend = XMLHttpRequest.prototype.send;

    XMLHttpRequest.prototype.open = function (method, url, ...args) {
        this._stardustMethod = method;
        this._stardustUrl = url;
        return originalOpen.apply(this, [method, url, ...args]);
    };

    XMLHttpRequest.prototype.send = function (...args) {
        const method = this._stardustMethod || 'GET';
        const url = this._stardustUrl || '';
        
        const name = `${method} ${url}`;
        const span = tracer.newSpan(name);
        span.setTag(`${method} ${url}`);

        // 监听完成事件
        this.addEventListener('load', function () {
            if (this.status >= 400) {
                span.setError(`HTTP ${this.status}`);
            }
            span.finish();
        });

        // 监听错误事件
        this.addEventListener('error', function () {
            span.setError('Network Error');
            span.finish();
        });

        this.addEventListener('abort', function () {
            span.setError('Request Aborted');
            span.finish();
        });

        return originalSend.apply(this, args);
    };
}

/**
 * 自动安装所有浏览器拦截器
 * @param {StardustBrowserTracer} tracer 浏览器追踪器实例
 */
function installBrowserInterceptors(tracer) {
    installFetchInterceptor(tracer);
    installXHRInterceptor(tracer);
    console.log('[Stardust] Browser interceptors installed');
}

module.exports = {
    installFetchInterceptor,
    installXHRInterceptor,
    installBrowserInterceptors,
};
