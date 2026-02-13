/**
 * StardustBrowserTracer - 浏览器环境的星尘APM追踪器
 * 适用于现代浏览器，使用fetch API和Web Crypto API
 */

class StardustBrowserTracer {
    constructor(server, appId, secret = '') {
        this.server = server.replace(/\/+$/, '');
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = `browser@${this._generateRandomId()}`;
        
        this._token = '';
        this._tokenExpire = 0;
        
        // 采样参数
        this.period = 60;
        this.maxSamples = 1;
        this.maxErrors = 10;
        this.timeout = 5000;
        this.maxTagLength = 1024;
        this.excludes = [];
        
        this._builders = new Map();
        this._running = false;
        this._reportTimer = null;
    }

    /**
     * 启动追踪器
     */
    async start() {
        await this._login();
        this._running = true;
        this._reportTimer = setInterval(() => this._flush(), this.period * 1000);
        console.log(`[Stardust] Browser tracer started for app: ${this.appId}`);
    }

    /**
     * 停止追踪器
     */
    stop() {
        this._running = false;
        if (this._reportTimer) {
            clearInterval(this._reportTimer);
            this._reportTimer = null;
        }
        this._flush();
        console.log('[Stardust] Browser tracer stopped');
    }

    /**
     * 创建新的Span
     * @param {String} name Span名称
     * @returns {Object} Span对象
     */
    newSpan(name) {
        const span = {
            Id: this._generateSpanId(),
            ParentId: '',
            TraceId: this._generateTraceId(),
            StartTime: Date.now(),
            EndTime: 0,
            Tag: '',
            Error: '',
        };

        return {
            ...span,
            setTag(tag) { span.Tag = String(tag); },
            setError(err) { 
                span.Error = err instanceof Error ? `${err.name}: ${err.message}` : String(err); 
            },
            finish: () => {
                span.EndTime = Date.now();
                this._addSpan(name, span);
            }
        };
    }

    /**
     * 内部方法：添加Span到Builder
     * @private
     */
    _addSpan(name, span) {
        // 排除特殊路径
        if (name === '/Trace/Report' || name === '/Trace/ReportRaw') return;
        
        // 检查排除列表
        for (const exc of this.excludes) {
            if (exc && name.toLowerCase().includes(exc.toLowerCase())) return;
        }

        // 限制Tag长度
        if (span.Tag && span.Tag.length > this.maxTagLength) {
            span.Tag = span.Tag.substring(0, this.maxTagLength);
        }

        let builder = this._builders.get(name);
        if (!builder) {
            builder = {
                Name: name,
                StartTime: Date.now(),
                EndTime: 0,
                Total: 0,
                Errors: 0,
                Cost: 0,
                MaxCost: 0,
                MinCost: 0,
                Samples: [],
                ErrorSamples: []
            };
            this._builders.set(name, builder);
        }

        const elapsed = span.EndTime - span.StartTime;
        builder.Total++;
        builder.Cost += elapsed;
        
        if (!builder.MaxCost || elapsed > builder.MaxCost) {
            builder.MaxCost = elapsed;
        }
        if (!builder.MinCost || elapsed < builder.MinCost) {
            builder.MinCost = elapsed;
        }
        builder.EndTime = Date.now();

        if (span.Error) {
            builder.Errors++;
            if (builder.ErrorSamples.length < this.maxErrors) {
                builder.ErrorSamples.push(span);
            }
        } else {
            if (builder.Samples.length < this.maxSamples) {
                builder.Samples.push(span);
            }
        }
    }

    /**
     * 登录并获取Token
     * @private
     */
    async _login() {
        try {
            const resp = await fetch(`${this.server}/App/Login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    AppId: this.appId,
                    Secret: this.secret,
                    ClientId: this.clientId,
                    AppName: this.appName
                }),
            });
            const json = await resp.json();
            if (json.code === 0 && json.data) {
                this._token = json.data.Token || '';
                this._tokenExpire = Date.now() / 1000 + (json.data.Expire || 7200);
                if (json.data.Code) this.appId = json.data.Code;
                if (json.data.Secret) this.secret = json.data.Secret;
                console.log('[Stardust] Browser login successful');
            }
        } catch (err) {
            console.error('[Stardust] Browser login failed:', err);
        }
    }

    /**
     * 刷新并上报数据
     * @private
     */
    async _flush() {
        if (this._builders.size === 0) return;
        
        const list = Array.from(this._builders.values()).filter(b => b.Total > 0);
        this._builders.clear();
        
        if (list.length === 0) return;

        try {
            const resp = await fetch(
                `${this.server}/Trace/Report?Token=${encodeURIComponent(this._token)}`,
                {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        AppId: this.appId,
                        AppName: this.appName,
                        ClientId: this.clientId,
                        Builders: list
                    }),
                }
            );
            const json = await resp.json();
            if (json.code === 0 && json.data) {
                this._applyResponse(json.data);
            }
        } catch (err) {
            console.error('[Stardust] Browser report failed:', err);
        }
    }

    /**
     * 应用服务器响应的配置
     * @private
     */
    _applyResponse(result) {
        if (result.Period > 0) this.period = result.Period;
        if (result.MaxSamples > 0) this.maxSamples = result.MaxSamples;
        if (result.MaxErrors > 0) this.maxErrors = result.MaxErrors;
        if (result.Timeout > 0) this.timeout = result.Timeout;
        if (result.MaxTagLength > 0) this.maxTagLength = result.MaxTagLength;
        if (result.Excludes) this.excludes = result.Excludes;
    }

    /**
     * 生成随机ID
     * @private
     */
    _generateRandomId() {
        return Math.random().toString(36).substring(2, 10);
    }

    /**
     * 生成Span ID (16位十六进制)
     * @private
     */
    _generateSpanId() {
        if (typeof crypto !== 'undefined' && crypto.randomUUID) {
            return crypto.randomUUID().replace(/-/g, '').substring(0, 16);
        }
        return this._generateRandomHex(16);
    }

    /**
     * 生成Trace ID (32位十六进制)
     * @private
     */
    _generateTraceId() {
        if (typeof crypto !== 'undefined' && crypto.randomUUID) {
            return crypto.randomUUID().replace(/-/g, '');
        }
        return this._generateRandomHex(32);
    }

    /**
     * 生成随机十六进制字符串
     * @private
     */
    _generateRandomHex(length) {
        let result = '';
        const characters = '0123456789abcdef';
        for (let i = 0; i < length; i++) {
            result += characters.charAt(Math.floor(Math.random() * 16));
        }
        return result;
    }
}

// 支持多种模块系统
if (typeof module !== 'undefined' && module.exports) {
    module.exports = StardustBrowserTracer;
}
if (typeof window !== 'undefined') {
    window.StardustBrowserTracer = StardustBrowserTracer;
}
