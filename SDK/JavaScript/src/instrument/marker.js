/**
 * 自动埋点内部标记与路径排除。
 *
 * 用于识别 SDK 自身请求（不产生 Span、不注入 traceparent），
 * 以及统一 /App/、/Trace/ 监控接口的排除判断。
 */

'use strict';

const INTERNAL = typeof Symbol === 'function' ? Symbol.for('stardust.internal') : '__stardustInternal';

/**
 * 标记请求参数为 SDK 内部请求。
 * @param {Object} options 请求参数
 * @returns {Object} 原对象
 */
function markInternal(options) {
    if (options && typeof options === 'object') {
        try {
            options[INTERNAL] = true;
        } catch (err) {
            // 冻结对象无法标记时忽略
        }
    }
    return options;
}

/**
 * 判断是否为 SDK 内部请求。
 * @param {Object} options 请求参数
 * @returns {Boolean} 是否内部请求
 */
function isMarkedInternal(options) {
    return !!(options && typeof options === 'object' && options[INTERNAL]);
}

/**
 * 判断路径是否为星尘监控接口。
 * @param {String} path 路径或 URL
 * @returns {Boolean} 是否忽略
 */
function isInternalPath(path) {
    const text = String(path || '');
    if (!text) return false;
    return /^\/(App|Trace)(\/|$)/i.test(text) || /\/App(\/|$|\?)/i.test(text) || /\/Trace(\/|$|\?)/i.test(text);
}

/**
 * 判断 URL 是否为星尘监控接口。
 * @param {String} url URL
 * @returns {Boolean} 是否忽略
 */
function isInternalUrl(url) {
    const text = String(url || '');
    if (!text) return false;
    try {
        return isInternalPath(new URL(text, 'http://localhost').pathname);
    } catch (err) {
        return isInternalPath(text);
    }
}

module.exports = {
    markInternal,
    isMarkedInternal,
    isInternalPath,
    isInternalUrl,
};
