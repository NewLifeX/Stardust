using System;
using NewLife.Log;

namespace Stardust.Monitors
{
    /// <summary>片段</summary>
    public class MySpan : ISpan
    {
        #region 属性
        /// <summary>唯一标识。随线程上下文、Http、Rpc传递，作为内部片段的父级</summary>
        public String Id { get; set; }

        /// <summary>父级片段标识</summary>
        public String ParentId { get; set; }

        /// <summary>跟踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public String TraceId { get; set; }

        /// <summary>开始时间。Unix毫秒</summary>
        public Int64 StartTime { get; set; }

        /// <summary>结束时间。Unix毫秒</summary>
        public Int64 EndTime { get; set; }

        /// <summary>数据标签。记录一些附加数据</summary>
        public String Tag { get; set; }

        /// <summary>错误信息</summary>
        public String Error { get; set; }
        #endregion

        void IDisposable.Dispose() => throw new NotImplementedException();

        void ISpan.SetError(Exception ex, Object tag) => throw new NotImplementedException();
    }
}