using System;

namespace Stardust.Data.Models
{
    /// <summary>Redis库实体</summary>
    public class RedisDbEntry
    {
        /// <summary>键个数</summary>
        public Int32 Keys { get; set; }

        /// <summary>过期数</summary>
        public Int32 Expires { get; set; }

        /// <summary>平均过期时间。秒</summary>
        public Int32 AvgTtl { get; set; }
    }
}