using System;

namespace Stardust.Models
{
    /// <summary>节点信息</summary>
    public class NodeInfo
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>编译时间</summary>
        public DateTime Compile { get; set; }

        /// <summary>系统名</summary>
        public String OSName { get; set; }

        /// <summary>系统版本</summary>
        public String OSVersion { get; set; }

        /// <summary>机器名</summary>
        public String MachineName { get; set; }

        /// <summary>用户名</summary>
        public String UserName { get; set; }

        /// <summary>核心数</summary>
        public Int32 ProcessorCount { get; set; }

        /// <summary>内存大小</summary>
        public UInt64 Memory { get; set; }

        /// <summary>可用内存大小</summary>
        public UInt64 AvailableMemory { get; set; }

        /// <summary>磁盘大小。应用所在盘</summary>
        public UInt64 TotalSize { get; set; }

        /// <summary>磁盘可用空间。应用所在盘</summary>
        public UInt64 AvailableFreeSpace { get; set; }

        /// <summary>处理器</summary>
        public String Processor { get; set; }

        /// <summary>处理器标识</summary>
        public String CpuID { get; set; }

        /// <summary>主频</summary>
        public Single CpuRate { get; set; }

        /// <summary>唯一标识</summary>
        public String UUID { get; set; }

        /// <summary>机器标识</summary>
        public String MachineGuid { get; set; }

        /// <summary>MAC地址</summary>
        public String Macs { get; set; }

        ///// <summary>串口</summary>
        //public String COMs { get; set; }

        /// <summary>安装路径</summary>
        public String InstallPath { get; set; }

        /// <summary>运行时</summary>
        public String Runtime { get; set; }

        /// <summary>本地UTC时间</summary>
        /// <remarks>
        /// 跨系统传递UTC时间是严谨的，但是UTC时间序列化比较头疼，目前能够做到自己序列化后，自己能够解析出来，暂时用着，将来向netcore的system.text.json序列化迁移
        /// </remarks>
        public DateTime Time { get; set; }
        #endregion
    }
}