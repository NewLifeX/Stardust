using System;

namespace Stardust.Models
{
    /// <summary>代理信息</summary>
    public class AgentInfo
    {
        /// <summary>进程标识</summary>
        public Int32 ProcessId { get; set; }

        /// <summary>进程名称</summary>
        public String ProcessName { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>文件路径</summary>
        public String FileName { get; set; }

        /// <summary>命令参数</summary>
        public String Arguments { get; set; }

        /// <summary>服务端地址</summary>
        public String Server { get; set; }

        /// <summary>
        /// 应用服务
        /// </summary>
        public String[] Services { get; set; }
    }
}