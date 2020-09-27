using System;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪统计模型</summary>
    public class TraceStatModel
    {
        /// <summary>时间</summary>
        public DateTime Time { get; set; }

        /// <summary>应用</summary>
        public Int32 AppId { get; set; }

        /// <summary>操作名</summary>
        public String Name { get; set; }

        /// <summary>用于统计的唯一Key</summary>
        public String Key => $"{Time}#{AppId}#{Name}";
    }
}