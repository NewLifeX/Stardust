/**
 * 自动埋点安装入口。
 *
 * Node.js 追踪器构造时默认安装（autoInstrument: true），
 * 也可手动调用 installAutoInstrumentation(tracer)。
 */

'use strict';

const registry = require('./registry');
const httpInstrument = require('./http');
const dbInstrument = require('./db');
const redisInstrument = require('./redis');

/**
 * 安装全部 Node.js 自动埋点。
 * @param {Object} tracer 追踪器
 * @returns {Object} 追踪器
 */
function installAutoInstrumentation(tracer) {
    if (!tracer) return tracer;

    registry.register(tracer);

    const installers = [httpInstrument.install, dbInstrument.install, redisInstrument.install];
    for (const install of installers) {
        try {
            install(tracer);
        } catch (err) {
            // 监控异常不得影响业务
        }
    }

    return tracer;
}

module.exports = {
    installAutoInstrumentation,
    register: registry.register,
    unregister: registry.unregister,
    getTracer: registry.getTracer,
    http: httpInstrument,
    db: dbInstrument,
    redis: redisInstrument,
};
