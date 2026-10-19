/**
 * Redis 自动埋点（redis / ioredis）。
 *
 * 无新增依赖：模块不存在时静默跳过。
 * Span 名称遵循 redis:{COMMAND} 约定。
 */

'use strict';

const { getTracer } = require('./registry');

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
 * 尝试加载可选模块。
 * @param {String} name 模块名
 * @returns {Object|null} 模块
 */
function tryRequire(name) {
    try {
        return require(name);
    } catch (err) {
        return null;
    }
}

/**
 * 把命令参数转为简短摘要。
 * @param {Array} args 参数
 * @returns {String} 摘要
 */
function summarizeArgs(args) {
    if (!Array.isArray(args)) return '';
    const parts = [];
    for (const arg of args) {
        if (arg === null || arg === undefined) continue;
        if (Buffer.isBuffer(arg)) parts.push('<' + arg.length + ' bytes>');
        else parts.push(truncate(arg, 64));
        if (parts.length >= 8) break;
    }
    return parts.join(' ');
}

/**
 * 解析 sendCommand 参数。
 * @param {*} payload 命令参数（数组或 Command 对象）
 * @returns {Object} { name, args }
 */
function parseCommand(payload) {
    if (Array.isArray(payload)) {
        return {
            name: payload.length > 0 ? String(payload[0]) : '',
            args: payload.slice(1),
        };
    }
    if (payload && typeof payload === 'object') {
        if (payload.name) {
            return {
                name: String(payload.name),
                args: Array.isArray(payload.args) ? payload.args : [],
            };
        }
        if (Array.isArray(payload.args) && payload.args.length > 0) {
            return {
                name: String(payload.args[0]),
                args: payload.args.slice(1),
            };
        }
    }
    return { name: '', args: [] };
}

/**
 * 结束 Span。
 * @param {Object} span Span
 * @param {*} err 错误
 * @returns {void}
 */
function finishSpan(span, err) {
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
}

/**
 * 包装 sendCommand 方法。
 * @param {Object} target 原型或实例
 * @returns {void}
 */
function patchSendCommand(target) {
    if (!target) return;
    const original = target.sendCommand;
    if (typeof original !== 'function' || original.__stardustWrapped) return;

    const wrapped = function () {
        const self = this;
        const tracer = getTracer();
        if (!tracer || (self && self.__stardustRedisCall)) return original.apply(self, arguments);

        const args = Array.prototype.slice.call(arguments);
        const command = parseCommand(args[0]);
        const span = tracer.newSpan('redis:' + (command.name ? command.name.toUpperCase() : 'COMMAND'));
        span.setTag(truncate((command.name ? command.name.toUpperCase() + ' ' : '') + summarizeArgs(command.args), tracer.maxTagLength));

        let settled = false;
        const finish = function (err) {
            if (settled) return;
            settled = true;
            finishSpan(span, err);
        };

        let callbackIndex = -1;
        for (let i = 1; i < args.length; i++) {
            if (typeof args[i] === 'function') {
                callbackIndex = i;
                break;
            }
        }
        if (callbackIndex >= 0) {
            const callback = args[callbackIndex];
            args[callbackIndex] = function (err) {
                finish(err);
                return callback.apply(this, arguments);
            };
        }

        let result;
        try {
            if (self) self.__stardustRedisCall = true;
            result = original.apply(self, args);
        } catch (err) {
            finish(err);
            throw err;
        } finally {
            if (self) self.__stardustRedisCall = false;
        }

        if (callbackIndex < 0 && result && typeof result.then === 'function') {
            return result.then(function (value) {
                finish();
                return value;
            }, function (err) {
                finish(err);
                throw err;
            });
        }
        if (callbackIndex < 0) finish();

        return result;
    };

    wrapped.__stardustWrapped = true;
    target.sendCommand = wrapped;
}

/**
 * 包装客户端实例的 sendCommand。
 * @param {Object} client 客户端
 * @returns {void}
 */
function patchClient(client) {
    try {
        if (client && typeof client.sendCommand === 'function' && !client.sendCommand.__stardustWrapped) patchSendCommand(client);
    } catch (err) {
        // 忽略
    }
}

/**
 * 安装 redis（node-redis v4）埋点。
 * @param {Object} tracer 追踪器
 * @param {Object} [redisModule] 模块，缺省时自动加载
 * @returns {Boolean} 是否安装成功
 */
function instrumentRedis(tracer, redisModule) {
    const mod = redisModule || tryRequire('redis');
    if (!mod) return false;

    const RedisClient = mod.RedisClient || (mod.default && mod.default.RedisClient);
    if (RedisClient && RedisClient.prototype && typeof RedisClient.prototype.sendCommand === 'function') {
        patchSendCommand(RedisClient.prototype);
        return true;
    }

    // 未导出 RedisClient 时，包装 createClient 以修补实例
    if (typeof mod.createClient === 'function' && !mod.createClient.__stardustWrapped) {
        const original = mod.createClient;
        const wrapped = function () {
            const client = original.apply(this, arguments);
            patchClient(client);
            return client;
        };
        wrapped.__stardustWrapped = true;
        mod.createClient = wrapped;
        return true;
    }

    return false;
}

/**
 * 安装 ioredis 埋点。
 * @param {Object} tracer 追踪器
 * @param {Object} [ioredisModule] 模块，缺省时自动加载
 * @returns {Boolean} 是否安装成功
 */
function instrumentIoredis(tracer, ioredisModule) {
    const mod = ioredisModule || tryRequire('ioredis');
    if (!mod) return false;

    const Redis = mod.Redis || mod.default || mod;
    if (Redis && Redis.prototype && typeof Redis.prototype.sendCommand === 'function') {
        patchSendCommand(Redis.prototype);
        return true;
    }

    return false;
}

/**
 * 自动检测并安装 Redis 埋点。
 * @param {Object} tracer 追踪器
 * @returns {void}
 */
function install(tracer) {
    try {
        instrumentRedis(tracer);
    } catch (err) {
        // 忽略
    }
    try {
        instrumentIoredis(tracer);
    } catch (err) {
        // 忽略
    }
}

module.exports = {
    install,
    instrumentRedis,
    instrumentIoredis,
    parseCommand,
};
