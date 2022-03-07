using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using NewLife;
using NewLife.Reflection;

namespace Stardust.Models
{
    /// <summary>代理信息</summary>
    public class AgentInfo
    {
        #region 属性
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

        /// <summary>本地IP地址</summary>
        public String IP { get; set; }

        /// <summary>服务端地址</summary>
        public String Server { get; set; }

        /// <summary>节点编码</summary>
        public String Code { get; set; }

        /// <summary>应用服务</summary>
        public String[] Services { get; set; }
        #endregion

        #region 辅助
        /// <summary>
        /// 获取本地信息
        /// </summary>
        /// <returns></returns>
        public static AgentInfo GetLocal()
        {
            var p = Process.GetCurrentProcess();
            var asmx = AssemblyX.Entry;
            var fileName = p.MainModule.FileName;
            var args = Environment.CommandLine.TrimStart(Path.ChangeExtension(fileName, ".dll")).Trim();
            var ip = NetHelper.GetIPsWithCache().Where(ip => ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169).Join();

            return new AgentInfo
            {
                Version = asmx?.FileVersion,
                ProcessId = p.Id,
                ProcessName = p.ProcessName,
                FileName = fileName,
                Arguments = args,
                IP = ip,
            };
        }
        #endregion
    }
}