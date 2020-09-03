using System;

namespace Stardust.Monitors
{
    /// <summary>跟踪模型</summary>
    public class MyTraceModel
    {
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
        public String ClientId { get; set; }

        /// <summary>跟踪数据</summary>
        public MyBuilder[] Builders { get; set; }
    }
}