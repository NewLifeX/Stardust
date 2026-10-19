/**
 * SpanBuilder - 聚合同名 Span 的统计信息。
 */

'use strict';

/**
 * 同名 Span 统计构建器。
 */
class SpanBuilder {
    /**
     * @param {String} name 名称
     * @param {Number} [maxSamples] 最大正常样本数
     * @param {Number} [maxErrors] 最大错误样本数
     */
    constructor(name, maxSamples, maxErrors) {
        this.name = name;
        this.startTime = Date.now();
        this.endTime = 0;
        this.total = 0;
        this.errors = 0;
        this.cost = 0;
        this.maxCost = 0;
        this.minCost = 0;
        this.samples = [];
        this.errorSamples = [];
        this._maxSamples = maxSamples > 0 ? maxSamples : 1;
        this._maxErrors = maxErrors > 0 ? maxErrors : 10;
    }

    /**
     * 添加一个 Span 到构建器。
     * @param {Object} span Span 对象
     * @returns {SpanBuilder} 当前实例
     */
    addSpan(span) {
        const elapsed = Math.max(0, span.endTime - span.startTime);
        this.total++;
        this.cost += elapsed;

        if (this.total === 1 || elapsed > this.maxCost) this.maxCost = elapsed;
        if (this.total === 1 || elapsed < this.minCost) this.minCost = elapsed;

        if (span.error) {
            this.errors++;
            if (this.errorSamples.length < this._maxErrors) this.errorSamples.push(span);
        } else if (this.samples.length < this._maxSamples) {
            this.samples.push(span);
        }

        this.endTime = Date.now();
        return this;
    }

    /**
     * 转换为 JSON 对象。
     * @returns {Object} JSON 对象
     */
    toJSON() {
        return {
            Name: this.name,
            StartTime: this.startTime,
            EndTime: this.endTime,
            Total: this.total,
            Errors: this.errors,
            Cost: this.cost,
            MaxCost: this.maxCost,
            MinCost: this.minCost,
            Samples: this.samples.map(s => s.toJSON()),
            ErrorSamples: this.errorSamples.map(s => s.toJSON()),
        };
    }
}

module.exports = SpanBuilder;
