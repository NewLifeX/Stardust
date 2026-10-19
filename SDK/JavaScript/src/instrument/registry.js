/**
 * 自动埋点追踪器注册表。
 *
 * 模块级 Monkey Patch 是全局的，埋点执行时从注册表取最近的追踪器。
 */

'use strict';

const tracers = [];

/**
 * 注册追踪器。
 * @param {Object} tracer 追踪器
 * @returns {void}
 */
function register(tracer) {
    if (!tracer || tracers.indexOf(tracer) !== -1) return;
    tracers.push(tracer);
}

/**
 * 注销追踪器。
 * @param {Object} tracer 追踪器
 * @returns {void}
 */
function unregister(tracer) {
    const index = tracers.indexOf(tracer);
    if (index !== -1) tracers.splice(index, 1);
}

/**
 * 获取当前用于自动埋点的追踪器（后注册者优先）。
 * @returns {Object|null} 追踪器
 */
function getTracer() {
    return tracers.length > 0 ? tracers[tracers.length - 1] : null;
}

module.exports = {
    register,
    unregister,
    getTracer,
};
