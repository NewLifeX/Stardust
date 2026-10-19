/**
 * W3C Trace Context（traceparent）工具。
 *
 * 提供 traceparent 解析/格式化、TraceId 与 SpanId 生成，
 * 以及基于 AsyncLocalStorage 的活动 Span 上下文（Node.js）。
 */

'use strict';

const { randomBytes } = require('crypto');

let AsyncLocalStorageCtor = null;
try {
    AsyncLocalStorageCtor = require('async_hooks').AsyncLocalStorage;
} catch (err) {
    AsyncLocalStorageCtor = null;
}

// W3C 标准要求 32 位 TraceId，但为兼容 NewLife .NET 历史实现允许更短的十六进制串
const TRACEPARENT_PATTERN = /^([0-9a-f]{2})-([0-9a-f]{1,32})-([0-9a-f]{16})-([0-9a-f]{2})(?:-.*)?$/i;

const storage = AsyncLocalStorageCtor ? new AsyncLocalStorageCtor() : null;
const spanStack = [];

/**
 * 生成 32 位小写十六进制 TraceId。
 * @returns {String} TraceId
 */
function generateTraceId() {
    return randomBytes(16).toString('hex');
}

/**
 * 生成 16 位小写十六进制 SpanId。
 * @returns {String} SpanId
 */
function generateSpanId() {
    return randomBytes(8).toString('hex');
}

/**
 * 解析 traceparent 头。
 * @param {String|String[]} value 头值，可为数组（Node 请求头）
 * @returns {Object|null} { version, traceId, spanId, flags, sampled }；非法时返回 null
 */
function parseTraceparent(value) {
    if (Array.isArray(value)) value = value.length > 0 ? value[0] : '';
    if (value === null || value === undefined) return null;

    const text = String(value).trim();
    if (!text) return null;

    const match = TRACEPARENT_PATTERN.exec(text);
    if (!match) return null;

    const version = match[1].toLowerCase();
    if (version === 'ff') return null;
    if (version === '00' && text.split('-').length !== 4) return null;

    const traceId = match[2].toLowerCase();
    const spanId = match[3].toLowerCase();
    if (/^0+$/.test(traceId) || /^0+$/.test(spanId)) return null;

    const flags = parseInt(match[4], 16);
    if (!isFinite(flags)) return null;

    return {
        version: version,
        traceId: traceId,
        spanId: spanId,
        flags: flags,
        sampled: (flags & 1) === 1,
    };
}

/**
 * 格式化 traceparent 头。SDK 不做头部采样，flags 固定为 01。
 * @param {String} traceId TraceId
 * @param {String} spanId SpanId
 * @param {Boolean} [sampled] 是否采样，默认 true
 * @returns {String} traceparent 值
 */
function formatTraceparent(traceId, spanId, sampled) {
    const trace = String(traceId || '').toLowerCase();
    const span = String(spanId || '').toLowerCase();
    if (!/^[0-9a-f]{1,32}$/.test(trace) || /^0+$/.test(trace)) return '';
    if (!/^[0-9a-f]{16}$/.test(span) || /^0+$/.test(span)) return '';

    const flags = sampled === false ? '00' : '01';
    return '00-' + trace + '-' + span + '-' + flags;
}

/**
 * 获取当前活动 Span。
 * @returns {Object|null} 当前 Span
 */
function getCurrentSpan() {
    if (storage) return storage.getStore() || null;
    return spanStack.length > 0 ? spanStack[spanStack.length - 1] : null;
}

/**
 * 在指定 Span 上下文中执行函数。
 * @param {Object} span 活动 Span
 * @param {Function} fn 待执行函数
 * @returns {*} 函数返回值
 */
function runWithSpan(span, fn) {
    if (typeof fn !== 'function') return undefined;
    if (storage) return storage.run(span || null, fn);

    spanStack.push(span || null);
    try {
        return fn();
    } finally {
        spanStack.pop();
    }
}

module.exports = {
    generateTraceId,
    generateSpanId,
    parseTraceparent,
    formatTraceparent,
    getCurrentSpan,
    runWithSpan,
};
