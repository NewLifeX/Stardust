using System;

namespace Stardust.Models
{
    /// <summary>命令模型</summary>
    public class CommandModel
    {
        /// <summary>序号</summary>
        public Int32 Id { get; set; }

        /// <summary>命令</summary>
        public String Command { get; set; }

        /// <summary>参数</summary>
        public String Argument { get; set; }

        /// <summary>过期时间。未指定时表示不限制</summary>
        public DateTime Expire { get; set; }
    }
}