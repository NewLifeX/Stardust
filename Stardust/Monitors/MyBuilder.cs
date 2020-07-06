using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.Log;

namespace Stardust.Monitors
{
    /// <summary>构建器</summary>
    public class MyBuilder : ISpanBuilder
    {
        #region 属性
        /// <summary>操作名</summary>
        public String Name { get; set; }

        /// <summary>开始时间。Unix毫秒</summary>
        public Int64 StartTime { get; set; }

        /// <summary>结束时间。Unix毫秒</summary>
        public Int64 EndTime { get; set; }

        /// <summary>采样总数</summary>
        public Int32 Total { get; set; }

        /// <summary>错误次数</summary>
        public Int32 Errors { get; set; }

        /// <summary>总耗时。所有请求耗时累加，单位ms</summary>
        public Int64 Cost { get; set; }

        /// <summary>最大耗时。单位ms</summary>
        public Int32 MaxCost { get; set; }

        /// <summary>最小耗时。单位ms</summary>
        public Int32 MinCost { get; set; }

        /// <summary>正常采样</summary>
        public List<MySpan> Samples { get; set; }

        /// <summary>异常采样</summary>
        public List<MySpan> ErrorSamples { get; set; }
        #endregion

        ITracer ISpanBuilder.Tracer => throw new NotImplementedException();

        IList<ISpan> ISpanBuilder.Samples => Samples?.Cast<ISpan>().ToList();

        IList<ISpan> ISpanBuilder.ErrorSamples => ErrorSamples?.Cast<ISpan>().ToList();

        void ISpanBuilder.Finish(ISpan span) => throw new NotImplementedException();

        ISpan ISpanBuilder.Start() => throw new NotImplementedException();
    }
}