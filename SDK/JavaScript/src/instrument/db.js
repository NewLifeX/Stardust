/**
 * 数据库自动埋点（pg / mysql2）。
 *
 * 无新增依赖：模块不存在时静默跳过。
 * Span 名称遵循 db:{system}:{action}:{tables} 约定。
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
 * 识别 SQL 动作。
 * @param {String} sql SQL 文本
 * @returns {String} 动作
 */
function detectAction(sql) {
    const text = String(sql || '').trim().toLowerCase();
    if (!text) return 'execute';
    if (/^(select|show|describe|desc|explain|with)\b/.test(text)) return 'select';
    if (/^insert\b/.test(text)) return 'insert';
    if (/^update\b/.test(text)) return 'update';
    if (/^delete\b/.test(text)) return 'delete';
    if (/^replace\b/.test(text)) return 'replace';
    if (/^merge\b/.test(text)) return 'merge';
    if (/^create\b/.test(text)) return 'create';
    if (/^alter\b/.test(text)) return 'alter';
    if (/^drop\b/.test(text)) return 'drop';
    if (/^truncate\b/.test(text)) return 'truncate';
    if (/^(begin|commit|rollback|start\s+transaction)\b/.test(text)) return 'transaction';

    const match = /^([a-z]+)/.exec(text);
    return match ? match[1] : 'execute';
}

/**
 * 去掉引号、架构前缀与日期分表后缀。
 * @param {String} raw 原始表名
 * @returns {String} 表名
 */
function normalizeTable(raw) {
    let name = String(raw || '')
        .replace(/[`"\[\]]/g, '')
        .trim();
    const dot = name.lastIndexOf('.');
    if (dot >= 0) name = name.substring(dot + 1);
    name = name.replace(/[_-]?\d{6,8}$/, '');
    return name;
}

/**
 * 从 SQL 中提取表名。
 * @param {String} sql SQL 文本
 * @returns {String} 逗号分隔的表名，未识别时为空串
 */
function extractTables(sql) {
    const text = String(sql || '');
    const patterns = [
        /\bfrom\s+([`"\[\]\w.$]+)/gi,
        /\binsert\s+into\s+([`"\[\]\w.$]+)/gi,
        /\bupdate\s+([`"\[\]\w.$]+)/gi,
        /\bjoin\s+([`"\[\]\w.$]+)/gi,
        /\bdrop\s+table\s+(?:if\s+exists\s+)?([`"\[\]\w.$]+)/gi,
        /\btruncate\s+table\s+([`"\[\]\w.$]+)/gi,
    ];

    const tables = [];
    for (const pattern of patterns) {
        pattern.lastIndex = 0;
        let match;
        while ((match = pattern.exec(text)) !== null) {
            const name = normalizeTable(match[1]);
            if (name && tables.indexOf(name) === -1) tables.push(name);
        }
    }
    return tables.join(',');
}

/**
 * 构造数据库 Span 名称。
 * @param {String} system 数据库系统
 * @param {String} sql SQL 文本
 * @returns {String} Span 名称
 */
function buildSpanName(system, sql) {
    const action = detectAction(sql);
    const tables = extractTables(sql) || 'unknown';
    return 'db:' + system + ':' + action + ':' + tables;
}

/**
 * 提取调用参数中的 SQL 文本。
 * @param {*} value 第一个参数
 * @returns {String} SQL 文本
 */
function getSqlText(value) {
    if (typeof value === 'string') return value;
    if (value && typeof value === 'object') {
        if (typeof value.text === 'string') return value.text;
        if (typeof value.sql === 'string') return value.sql;
    }
    return '';
}

/**
 * 包装原型方法，处理回调与 Promise 两种调用方式。
 * @param {Object} proto 原型
 * @param {String} method 方法名
 * @param {String} system 数据库系统
 * @returns {void}
 */
function patchQuery(proto, method, system) {
    if (!proto) return;
    const original = proto[method];
    if (typeof original !== 'function' || original.__stardustWrapped) return;

    const wrapped = function () {
        const self = this;
        const tracer = getTracer();
        if (!tracer || (self && self.__stardustDbCall)) return original.apply(self, arguments);

        const args = Array.prototype.slice.call(arguments);
        const sql = getSqlText(args[0]);
        const span = tracer.newSpan(buildSpanName(system, sql));
        span.setTag(truncate(sql, tracer.maxTagLength));

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
            if (self) self.__stardustDbCall = true;
            result = original.apply(self, args);
        } catch (err) {
            finish(err);
            throw err;
        } finally {
            if (self) self.__stardustDbCall = false;
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
    proto[method] = wrapped;
}

/**
 * 安装 pg 埋点（Client.prototype.query）。
 * @param {Object} tracer 追踪器
 * @param {Object} [pgModule] 模块，缺省时自动加载
 * @returns {Boolean} 是否安装成功
 */
function instrumentPg(tracer, pgModule) {
    const mod = pgModule || tryRequire('pg');
    if (!mod) return false;

    const Client = mod.Client || (mod.default && mod.default.Client);
    if (!Client || !Client.prototype) return false;

    patchQuery(Client.prototype, 'query', 'postgresql');
    return true;
}

/**
 * 解析 mysql2 Connection 构造器。
 * @param {Object} mod 模块
 * @returns {Function|null} 构造器
 */
function resolveMysqlConnection(mod) {
    if (!mod) return null;
    if (mod.Connection && mod.Connection.prototype) return mod.Connection;
    if (mod.default && mod.default.Connection && mod.default.Connection.prototype) return mod.default.Connection;

    const promiseModule = tryRequire('mysql2/promise');
    if (promiseModule && promiseModule.Connection && promiseModule.Connection.prototype) return promiseModule.Connection;
    return null;
}

/**
 * 安装 mysql2 埋点（Connection.prototype.query/execute）。
 * @param {Object} tracer 追踪器
 * @param {Object} [mysqlModule] 模块，缺省时自动加载
 * @returns {Boolean} 是否安装成功
 */
function instrumentMysql2(tracer, mysqlModule) {
    const mod = mysqlModule || tryRequire('mysql2');
    if (!mod) return false;

    const Connection = resolveMysqlConnection(mod);
    if (!Connection) return false;

    patchQuery(Connection.prototype, 'query', 'mysql');
    patchQuery(Connection.prototype, 'execute', 'mysql');
    return true;
}

/**
 * 自动检测并安装数据库埋点。
 * @param {Object} tracer 追踪器
 * @returns {void}
 */
function install(tracer) {
    try {
        instrumentPg(tracer);
    } catch (err) {
        // 忽略
    }
    try {
        instrumentMysql2(tracer);
    } catch (err) {
        // 忽略
    }
}

module.exports = {
    install,
    instrumentPg,
    instrumentMysql2,
    buildSpanName,
    detectAction,
    extractTables,
};
