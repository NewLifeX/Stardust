/**
 * Span - 表示单个追踪跨度
 */

const { randomBytes } = require('crypto');

class Span {
    constructor(name, tracer, parentId = '') {
        this.id = randomBytes(8).toString('hex');
        this.parentId = parentId;
        this.traceId = randomBytes(16).toString('hex');
        this.name = name;
        this.startTime = Date.now();
        this.endTime = 0;
        this.tag = '';
        this.error = '';
        this._tracer = tracer;
    }

    /**
     * 设置错误信息
     * @param {Error|String} err 错误对象或错误消息
     */
    setError(err) {
        this.error = err instanceof Error ? `${err.name}: ${err.message}` : String(err);
    }

    /**
     * 设置标签
     * @param {String} tag 标签内容
     */
    setTag(tag) {
        this.tag = String(tag);
    }

    /**
     * 完成跨度并上报
     */
    finish() {
        this.endTime = Date.now();
        this._tracer._finishSpan(this);
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

module.exports = Span;
