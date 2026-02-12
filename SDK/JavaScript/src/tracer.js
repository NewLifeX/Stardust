/**
 * StardustTracer - Node.js环境的星尘APM追踪器
 */

const http = require('http');
const https = require('https');
const zlib = require('zlib');
const os = require('os');
const Span = require('./span');
const SpanBuilder = require('./span-builder');

class StardustTracer {
    constructor(server, appId, secret = '') {
        this.server = server.replace(/\/+$/, '');
        this.appId = appId;
        this.appName = appId;
        this.secret = secret;
        this.clientId = `${this._getLocalIP()}@${process.pid}`;

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
        this._pingTimer = null;
    }

    /**
     * 启动追踪器
     */
    async start() {
        await this._login();
        this._running = true;
        this._reportTimer = setInterval(() => this._flush(), this.period * 1000);
        this._pingTimer = setInterval(() => this._ping(), 30000);
        console.log(`[Stardust] Tracer started for app: ${this.appId}`);
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
        if (this._pingTimer) {
            clearInterval(this._pingTimer);
            this._pingTimer = null;
        }
        this._flush();
        console.log('[Stardust] Tracer stopped');
    }

    /**
     * 创建新的Span
     * @param {String} name Span名称
     * @param {String} parentId 父Span ID
     * @returns {Span} Span对象
     */
    newSpan(name, parentId = '') {
        return new Span(name, this, parentId);
    }

    /**
     * 内部方法：完成Span
     * @private
     */
    _finishSpan(span) {
        // 排除特殊路径
        if (span.name === '/Trace/Report' || span.name === '/Trace/ReportRaw') return;
        
        // 检查排除列表
        for (const exc of this.excludes) {
            if (exc && span.name.toLowerCase().includes(exc.toLowerCase())) return;
        }

        // 限制Tag长度
        if (span.tag && span.tag.length > this.maxTagLength) {
            span.tag = span.tag.substring(0, this.maxTagLength);
        }

        // 获取或创建Builder
        let builder = this._builders.get(span.name);
        if (!builder) {
            builder = new SpanBuilder(span.name, this.maxSamples, this.maxErrors);
            this._builders.set(span.name, builder);
        }
        builder.addSpan(span);
    }

    /**
     * 登录并获取Token
     * @private
     */
    async _login() {
        const payload = {
            AppId: this.appId,
            Secret: this.secret,
            ClientId: this.clientId,
            AppName: this.appName,
        };

        try {
            const data = await this._postJson(`${this.server}/App/Login`, payload);
            if (data) {
                this._token = data.Token || '';
                this._tokenExpire = Date.now() / 1000 + (data.Expire || 7200);
                if (data.Code) this.appId = data.Code;
                if (data.Secret) this.secret = data.Secret;
                console.log('[Stardust] Login successful');
            }
        } catch (err) {
            console.error(`[Stardust] Login failed: ${err.message}`);
        }
    }

    /**
     * 发送心跳
     * @private
     */
    async _ping() {
        const payload = {
            Id: process.pid,
            Name: this.appName,
            Time: Date.now(),
        };

        try {
            const data = await this._postJson(
                `${this.server}/App/Ping?Token=${encodeURIComponent(this._token)}`,
                payload
            );
            if (data && data.Token) {
                this._token = data.Token;
            }
        } catch (err) {
            console.error(`[Stardust] Ping failed: ${err.message}`);
        }
    }

    /**
     * 上报追踪数据
     * @private
     */
    async _report(buildersData) {
        const payload = {
            AppId: this.appId,
            AppName: this.appName,
            ClientId: this.clientId,
            Builders: buildersData,
        };

        const body = JSON.stringify(payload);

        try {
            let data;
            if (body.length > 1024) {
                const url = `${this.server}/Trace/ReportRaw?Token=${encodeURIComponent(this._token)}`;
                data = await this._postGzip(url, body);
            } else {
                const url = `${this.server}/Trace/Report?Token=${encodeURIComponent(this._token)}`;
                data = await this._postJson(url, payload);
            }
            if (data) this._applyResponse(data);
        } catch (err) {
            console.error(`[Stardust] Report failed: ${err.message}`);
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
     * 刷新并上报数据
     * @private
     */
    _flush() {
        if (this._builders.size === 0) return;
        
        const list = [];
        for (const [, builder] of this._builders) {
            if (builder.total > 0) list.push(builder.toJSON());
        }
        this._builders.clear();
        
        if (list.length > 0) {
            this._report(list);
        }
    }

    // ========== HTTP 工具方法 ==========

    /**
     * POST JSON数据
     * @private
     */
    _postJson(urlStr, payload) {
        return new Promise((resolve, reject) => {
            const body = JSON.stringify(payload);
            const parsed = new URL(urlStr);
            const mod = parsed.protocol === 'https:' ? https : http;

            const req = mod.request(parsed, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json; charset=utf-8',
                    'Content-Length': Buffer.byteLength(body),
                },
                timeout: 10000,
            }, (res) => {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => {
                    try {
                        const json = JSON.parse(data);
                        resolve(json.code === 0 ? json.data : null);
                    } catch {
                        resolve(null);
                    }
                });
            });

            req.on('error', reject);
            req.on('timeout', () => { 
                req.destroy(); 
                reject(new Error('timeout')); 
            });
            req.write(body);
            req.end();
        });
    }

    /**
     * POST GZIP压缩数据
     * @private
     */
    _postGzip(urlStr, jsonBody) {
        return new Promise((resolve, reject) => {
            zlib.gzip(Buffer.from(jsonBody, 'utf-8'), (err, compressed) => {
                if (err) { 
                    reject(err); 
                    return; 
                }

                const parsed = new URL(urlStr);
                const mod = parsed.protocol === 'https:' ? https : http;

                const req = mod.request(parsed, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-gzip',
                        'Content-Length': compressed.length,
                    },
                    timeout: 10000,
                }, (res) => {
                    let data = '';
                    res.on('data', chunk => data += chunk);
                    res.on('end', () => {
                        try {
                            const json = JSON.parse(data);
                            resolve(json.code === 0 ? json.data : null);
                        } catch {
                            resolve(null);
                        }
                    });
                });

                req.on('error', reject);
                req.on('timeout', () => { 
                    req.destroy(); 
                    reject(new Error('timeout')); 
                });
                req.write(compressed);
                req.end();
            });
        });
    }

    /**
     * 获取本地IP地址
     * @private
     */
    _getLocalIP() {
        const interfaces = os.networkInterfaces();
        for (const name of Object.keys(interfaces)) {
            for (const iface of interfaces[name]) {
                if (iface.family === 'IPv4' && !iface.internal) {
                    return iface.address;
                }
            }
        }
        return '127.0.0.1';
    }
}

module.exports = StardustTracer;
