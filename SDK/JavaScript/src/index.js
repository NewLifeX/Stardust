/**
 * Stardust JavaScript SDK
 * 星尘监控 JavaScript/Node.js SDK
 *
 * 提供 APM 性能监控能力，支持 Node.js 和浏览器环境。
 */

'use strict';

const StardustTracer = require('./tracer');
const StardustBrowserTracer = require('./browser');
const Span = require('./span');
const SpanBuilder = require('./span-builder');
const traceContext = require('./trace-context');
const { installAutoInstrumentation } = require('./instrument');
const {
    installBrowserInterceptors,
    installFetchInterceptor,
    installXHRInterceptor,
} = require('./browser');

/**
 * 创建 Node.js 追踪器。
 * @param {String|Object} serverOrOptions 服务器地址或配置对象
 * @param {String} [appId] 应用ID
 * @param {String} [secret] 应用密钥
 * @returns {StardustTracer} 追踪器
 */
function createTracer(serverOrOptions, appId, secret) {
    return new StardustTracer(serverOrOptions, appId, secret);
}

/**
 * 创建并启动 Node.js 追踪器（一行接入）。
 * @param {String|Object} serverOrOptions 服务器地址或配置对象
 * @param {String} [appId] 应用ID
 * @param {String} [secret] 应用密钥
 * @returns {StardustTracer} 追踪器
 */
function init(serverOrOptions, appId, secret) {
    const tracer = new StardustTracer(serverOrOptions, appId, secret);
    tracer.start();
    return tracer;
}

module.exports = {
    StardustTracer,
    StardustBrowserTracer,
    Span,
    SpanBuilder,
    createTracer,
    init,
    installBrowserInterceptors,
    installFetchInterceptor,
    installXHRInterceptor,
    installAutoInstrumentation,
    parseTraceparent: traceContext.parseTraceparent,
    formatTraceparent: traceContext.formatTraceparent,
    generateTraceId: traceContext.generateTraceId,
    generateSpanId: traceContext.generateSpanId,
    getCurrentSpan: traceContext.getCurrentSpan,
    runWithSpan: traceContext.runWithSpan,
};
