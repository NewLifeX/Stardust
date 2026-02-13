/**
 * Stardust JavaScript SDK
 * 星尘监控 JavaScript/Node.js SDK
 * 
 * 提供APM性能监控能力，支持Node.js和浏览器环境
 */

const StardustTracer = require('./tracer');
const StardustBrowserTracer = require('./browser-tracer');
const Span = require('./span');
const SpanBuilder = require('./span-builder');

module.exports = {
    StardustTracer,
    StardustBrowserTracer,
    Span,
    SpanBuilder,
};
