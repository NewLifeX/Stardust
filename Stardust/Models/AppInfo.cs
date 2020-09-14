using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Stardust.Models
{
    /// <summary>进程信息</summary>
    public class AppInfo
    {
        #region 属性
        /// <summary>标识</summary>
        public Int32 Id { get; set; }

        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>用户名</summary>
        public String UserName { get; set; }

        /// <summary>开始时间</summary>
        public DateTime StartTime { get; set; }

        /// <summary>处理器时间。单位ms</summary>
        public Int32 ProcessorTime { get; set; }

        /// <summary>物理内存</summary>
        public Int64 WorkingSet { get; set; }

        /// <summary>线程数</summary>
        public Int32 Threads { get; set; }

        /// <summary>句柄数</summary>
        public Int32 Handles { get; set; }

        /// <summary>连接数</summary>
        public Int32 Connections { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化进程信息</summary>
        public AppInfo() { }

        /// <summary>根据进程对象实例化进程信息</summary>
        /// <param name="process"></param>
        public AppInfo(Process process)
        {
            try
            {
                Id = process.Id;
                Name = process.ProcessName;
                //StartTime = process.StartTime;
                //ProcessorTime = (Int64)process.TotalProcessorTime.TotalMilliseconds;
                WorkingSet = process.WorkingSet64;
                Threads = process.Threads.Count;
                Handles = process.HandleCount;

                UserName = Environment.UserName;
                StartTime = process.StartTime;
                ProcessorTime = (Int32)process.TotalProcessorTime.TotalMilliseconds;
            }
            catch (Win32Exception) { }
        }
        #endregion
    }
}