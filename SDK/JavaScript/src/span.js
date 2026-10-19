/**
 * Span - 表示单个追踪跨度。
 */

'use strict';

const { randomBytes } = require('crypto');

/**
 * 将错误对象格式化为可读消息。
 * @param {*} err 错误对象或字符串
 * @returns {String} 错误消息
 */
function formatError(err) {
    if (err === null || err === undefined) return 'Unknown error';
    if (typeof err === 'string') return err;
    if (err.message) {
        const name = err.name ? err.name + ': ' : '';
        return name + err.message;
    }
    try {
        return String(err);
    } catch (ignore) {
        return 'Unknown error';
    }
}

/**
 * 单个追踪跨度。
 */
class Span {
    /**
     * @param {String} name Span 名称
     * @param {Object} tracer 追踪器
     * @param {String} [parentId] 父 Span ID
     */
    constructor(name, tracer, parentId) {
        this.id = randomBytes(8).toString('hex');
        this.parentId = parentId || '';
        this.traceId = randomBytes(16).toString('hex');
        this.name = name;
        this.startTime = Date.now();
        this.endTime = 0;
        this.tag = '';
        this.error = '';
        this._tracer = tracer;
        this._finished = false;
    }

    /**
     * 设置错误信息。
     * @param {Error|String} err 错误对象或错误消息
     * @returns {Span} 当前实例
     */
    setError(err) {
        this.error = formatError(err);
        return this;
    }

    /**
     * 设置标签。
     * @param {String} tag 标签内容
     * @returns {Span} 当前实例
     */
    setTag(tag) {
        this.tag = tag === null || tag === undefined ? '' : String(tag);
        return this;
    }

    /**
     * 完成跨度并上报。重复调用只生效一次。
     * @returns {Span} 当前实例
     */
    finish() {
        if (this._finished) return this;
        this._finished = true;
        this.endTime = Date.now();
        if (this._tracer && typeof this._tracer._finishSpan === 'function') this._tracer._finishSpan(this);
        return this;
    }

    /**
     * 转换为JSON对象
     * @returns {Object} JSON对象
     */
    toJSON() {
        return {
            Id: this.id,
            ParentId: this.parentId,
            TraceId: this.traceId,
            StartTime: this.startTime,
            EndTime: this.endTime,
            Tag: this.tag,
            Error: this.error,
        };
    }
}

Span.formatError = formatError;

module.exports = Span;
