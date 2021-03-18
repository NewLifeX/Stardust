using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NewLife;

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

        /// <summary>版本</summary>
        public String Version { get; set; }

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

        private readonly Process _process;
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
                if (Name == "dotnet") Name = GetProcessName(process);
                //StartTime = process.StartTime;
                //ProcessorTime = (Int64)process.TotalProcessorTime.TotalMilliseconds;

                _process = process;

                Refresh();
            }
            catch (Win32Exception) { }
        }
        #endregion

        #region 方法
        /// <summary>刷新进程相关信息</summary>
        public void Refresh()
        {
            try
            {
                _process.Refresh();

                WorkingSet = _process.WorkingSet64;
                Threads = _process.Threads.Count;
                Handles = _process.HandleCount;

                UserName = Environment.UserName;
                StartTime = _process.StartTime;
                ProcessorTime = (Int32)_process.TotalProcessorTime.TotalMilliseconds;

                try
                {
                    // 调用WindowApi获取进程的连接数
                    var tcps = NetHelper.GetAllTcpConnections();
                    if (tcps != null && tcps.Length > 0)
                    {
                        var pid = Process.GetCurrentProcess().Id;
                        Connections = tcps.Count(e => e.ProcessId == pid);
                    }
                }
                catch { }
            }
            catch (Win32Exception) { }
        }

        /// <summary>获取进程名</summary>
        /// <param name="process"></param>
        /// <returns></returns>
        public static String GetProcessName(Process process)
        {
            if (Runtime.Linux)
            {
                try
                {
                    var line = File.ReadAllText($"/proc/{process.Id}/cmdline").TrimStart("\0", "dotnet", " ", "./");
                    if (!line.IsNullOrEmpty())
                    {
                        var p = line.IndexOf('\0');
                        if (p < 0) p = line.IndexOf(' ');
                        if (p < 0) p = line.IndexOf('-');
                        if (p < 0) p = line.IndexOf(".dll");
                        if (p > 0)
                            return line.Substring(0, p);
                        else
                            return line;
                    }
                }
                catch { }
            }

            return process.ProcessName;
        }
        #endregion
    }
}