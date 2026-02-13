/**
 * SpanBuilder - 聚合同名Span的统计信息
 */

class SpanBuilder {
    constructor(name, maxSamples = 1, maxErrors = 10) {
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
        this._maxSamples = maxSamples;
        this._maxErrors = maxErrors;
    }

    /**
     * 添加一个Span到构建器
     * @param {Object} span Span对象
     */
    addSpan(span) {
        const elapsed = span.endTime - span.startTime;
        this.total++;
        this.cost += elapsed;
        
        if (this.maxCost === 0 || elapsed > this.maxCost) {
            this.maxCost = elapsed;
        }
        if (this.minCost === 0 || elapsed < this.minCost) {
            this.minCost = elapsed;
        }

        if (span.error) {
            this.errors++;
            if (this.errorSamples.length < this._maxErrors) {
                this.errorSamples.push(span);
            }
        } else {
            if (this.samples.length < this._maxSamples) {
                this.samples.push(span);
            }
        }
        
        this.endTime = Date.now();
    }

    /**
     * 转换为JSON对象
     * @returns {Object} JSON对象
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
