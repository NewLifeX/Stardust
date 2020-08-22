using System;
using NewLife.Log;

namespace Stardust.Monitors
{
    /// <summary>跟踪请求模型</summary>
    public class TraceModel
    {
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>应用密钥</summary>
        public String AppSecret { get; set; }

        /// <summary>跟踪数据</summary>
        public ISpanBuilder[] Builders { get; set; }
    }

    /// <summary>跟踪响应模型</summary>
    public class TraceResponse
    {
        /// <summary>采样周期。默认15s</summary>
        public Int32 Period { get; set; } = 15;

        /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
        public Int32 MaxSamples { get; set; } = 1;

        /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
        public Int32 MaxErrors { get; set; } = 10;

        /// <summary>要排除的操作名</summary>
        public String[] Excludes { get; set; }
    }
}