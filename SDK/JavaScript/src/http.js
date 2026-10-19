/**
 * Stardust Node.js HTTP 传输工具。
 * 仅使用 Node 内置模块，负责 JSON / GZip POST、响应信封解析与超时控制。
 */

'use strict';

const http = require('http');
const https = require('https');
const zlib = require('zlib');
const { markInternal } = require('./instrument/marker');

/**
 * 星尘接口错误。code 为业务错误码（401 表示令牌失效）。
 */
class StardustHttpError extends Error {
    /**
     * @param {String} message 错误消息
     * @param {Number} code 业务错误码或 HTTP 状态码
     * @param {Number} statusCode HTTP 状态码
     */
    constructor(message, code, statusCode) {
        super(message);
        this.name = 'StardustHttpError';
        this.code = code;
        this.statusCode = statusCode;
    }
}

/**
 * 解析服务端响应信封 { code: 0, data } / { code, message }。
 * @param {Number} statusCode HTTP 状态码
 * @param {String} text 响应文本
 * @returns {*} 成功时的 data
 */
function parseEnvelope(statusCode, text) {
    let json = null;
    if (text) {
        try {
            json = JSON.parse(text);
        } catch (err) {
            json = null;
        }
    }

    if (statusCode === 401) {
        const message = json && json.message ? json.message : 'Unauthorized';
        throw new StardustHttpError(message, 401, 401);
    }

    const hasCode = json && typeof json === 'object' && Object.prototype.hasOwnProperty.call(json, 'code');
    if (hasCode) {
        const code = Number(json.code);
        if (code === 0) {
            if (statusCode >= 400) throw new StardustHttpError(json.message || ('HTTP ' + statusCode), statusCode, statusCode);
            return json.data === undefined ? null : json.data;
        }
        throw new StardustHttpError(json.message || ('Stardust API error ' + code), code, statusCode);
    }

    if (statusCode < 200 || statusCode >= 300) {
        throw new StardustHttpError('HTTP ' + statusCode, statusCode, statusCode);
    }

    return json;
}

/**
 * 判断是否为鉴权失效错误。
 * @param {*} err 错误对象
 * @returns {Boolean} 是否 401
 */
function isAuthError(err) {
    if (!err) return false;
    return Number(err.code) === 401 || Number(err.statusCode) === 401;
}

/**
 * 发送 POST 请求。
 * @param {String} urlStr 完整地址
 * @param {String|Buffer} body 请求体
 * @param {String} contentType 内容类型
 * @param {Number} timeoutMs 超时毫秒
 * @returns {Promise<*>} 解析后的 data
 */
function post(urlStr, body, contentType, timeoutMs) {
    return new Promise((resolve, reject) => {
        let parsed;
        try {
            parsed = new URL(urlStr);
        } catch (err) {
            reject(err);
            return;
        }

        const timeout = timeoutMs > 0 ? timeoutMs : 10000;
        const mod = parsed.protocol === 'https:' ? https : http;
        let settled = false;
        const done = (fn, value) => {
            if (settled) return;
            settled = true;
            fn(value);
        };

        const buffer = Buffer.isBuffer(body) ? body : Buffer.from(String(body || ''), 'utf8');
        const options = markInternal({
            method: 'POST',
            headers: {
                'Content-Type': contentType,
                'Content-Length': buffer.length,
                'Connection': 'close',
            },
            timeout: timeout,
        });
        let req;
        try {
            req = mod.request(parsed, options, (res) => {
                const chunks = [];
                res.on('data', (chunk) => chunks.push(chunk));
                res.on('end', () => {
                    const text = Buffer.concat(chunks).toString('utf8');
                    try {
                        done(resolve, parseEnvelope(res.statusCode, text));
                    } catch (err) {
                        done(reject, err);
                    }
                });
                res.on('error', (err) => done(reject, err));
            });
        } catch (err) {
            reject(err);
            return;
        }

        req.on('error', (err) => done(reject, err));
        req.setTimeout(timeout, () => {
            const err = new Error('Stardust request timeout');
            err.code = 'ETIMEDOUT';
            try {
                req.destroy(err);
            } catch (ignore) {
                // 忽略销毁异常
            }
            done(reject, err);
        });
        req.end(buffer);
    });
}

/**
 * POST JSON 数据。
 * @param {String} urlStr 完整地址
 * @param {*} payload JSON 对象或已序列化字符串
 * @param {Object} [options] { timeout }
 * @returns {Promise<*>} data
 */
function postJson(urlStr, payload, options) {
    const body = typeof payload === 'string' ? payload : JSON.stringify(payload);
    const timeout = options && options.timeout;
    return post(urlStr, body, 'application/json; charset=utf-8', timeout);
}

/**
 * POST GZip 压缩数据，Content-Type 固定为 application/x-gzip。
 * @param {String} urlStr 完整地址
 * @param {String|Buffer} jsonBody 未压缩的 JSON 内容
 * @param {Object} [options] { timeout }
 * @returns {Promise<*>} data
 */
function postGzip(urlStr, jsonBody, options) {
    const timeout = options && options.timeout;
    return new Promise((resolve, reject) => {
        const input = Buffer.isBuffer(jsonBody) ? jsonBody : Buffer.from(String(jsonBody || ''), 'utf8');
        zlib.gzip(input, (err, compressed) => {
            if (err) {
                reject(err);
                return;
            }
            post(urlStr, compressed, 'application/x-gzip', timeout).then(resolve, reject);
        });
    });
}

module.exports = {
    StardustHttpError,
    parseEnvelope,
    isAuthError,
    postJson,
    postGzip,
};
