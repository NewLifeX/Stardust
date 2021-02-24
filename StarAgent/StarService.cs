using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Threading;
using Stardust;
using Stardust.Models;

namespace StarAgent
{
    [Api(null)]
    public class StarService
    {
        #region 属性
        /// <summary>服务对象</summary>
        public ServiceBase Service { get; set; }

        /// <summary>服务主机</summary>
        public IHost Host { get; set; }

        /// <summary>本地应用服务管理</summary>
        public ServiceManager Manager { get; set; }
        #endregion

        #region 业务
        /// <summary>信息</summary>
        /// <returns></returns>
        [Api(nameof(Info))]
        public AgentInfo Info(AgentInfo info)
        {
            var p = Process.GetCurrentProcess();
            var asmx = AssemblyX.Entry;
            var fileName = p.MainModule.FileName;
            var args = Environment.CommandLine.TrimStart(Path.ChangeExtension(fileName, ".dll")).Trim();

            var set = Stardust.Setting.Current;

            return new AgentInfo
            {
                Version = asmx?.Version,
                ProcessId = p.Id,
                ProcessName = p.ProcessName,
                FileName = fileName,
                Arguments = args,
                Server = set.Server,
            };
        }

        /// <summary>杀死并启动进程</summary>
        /// <param name="processId">进程</param>
        /// <param name="delay">延迟结束的秒数</param>
        /// <param name="fileName">文件名</param>
        /// <param name="arguments">参数</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <returns></returns>
        [Api(nameof(KillAndStart))]
        public String KillAndStart(Int32 processId, Int32 delay, String fileName, String arguments, String workingDirectory)
        {
            var p = Process.GetProcessById(processId);
            if (p == null) throw new InvalidOperationException($"无效进程Id[{processId}]");

            var name = p.ProcessName;

            ThreadPoolX.QueueUserWorkItem(() =>
            {
                WriteLog("杀死进程 {0}/{1}，等待 {2}秒", processId, p.ProcessName, delay);

                if (delay > 0) Thread.Sleep(delay * 1000);

                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(5_000);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }

                // 启动进程
                if (!fileName.IsNullOrEmpty())
                {
                    WriteLog("启动进程：{0} {1} {2}", fileName, arguments, workingDirectory);

                    var si = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,

                        // false时目前控制台合并到当前控制台，一起退出；
                        // true时目标控制台独立窗口，不会一起退出；
                        UseShellExecute = true,
                    };

                    var p2 = Process.Start(si);

                    WriteLog("应用[{0}]启动成功 PID={1}", p2.ProcessName, p2.Id);
                }
            });

            return name;
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; }

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
        #endregion
    }
}