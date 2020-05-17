using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Models;

namespace Stardust
{
    /// <summary>应用服务管理</summary>
    public class ServiceManager : DisposeBase
    {
        #region 属性
        /// <summary>应用服务集合</summary>
        public ServiceInfo[] Services { get; set; }

        private readonly Dictionary<String, Process> _processes = new Dictionary<String, Process>();
        #endregion

        #region 构造
        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            Stop(disposing ? "Dispose" : "GC");
        }
        #endregion

        #region 方法
        /// <summary>开始管理，拉起应用进程</summary>
        public void Start()
        {
            //var ts = new List<Task<Process>>();
            foreach (var service in Services)
            {
                WriteLog("启动应用[{0}]：{1} {2}", service.Name, service.FileName, service.Arguments);

                //if (item.AutoStart) ts.Add(Task.Run(() => StartService(item)));
                if (service.AutoStart) StartService(service);
            }

            //// 等待全部完成
            //var ps = Task.WhenAll(ts).Result;
            //_processes.AddRange(ps.Where(e => e != null));

            _timer = new TimerX(DoWork, null, 5000, 5000) { Async = true };
        }

        private Process StartService(ServiceInfo service)
        {
            // 检查应用是否已启动
            var pidFile = Setting.Current.DataPath.CombinePath($"{service.Name}.pid").GetBasePath();
            if (File.Exists(pidFile))
            {
                try
                {
                    // 读取 pid,procss_name
                    var ss = File.ReadAllText(pidFile).Split(",");
                    if (ss != null && ss.Length >= 2)
                    {
                        var p = Process.GetProcessById(ss[0].ToInt());
                        if (p != null && !p.HasExited && p.ProcessName == ss[1])
                        {
                            WriteLog("应用[{0}/{1}]已启动，直接接管", service.Name, ss[0]);

                            _processes[service.Name] = p;
                            return p;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException)) XTrace.WriteException(ex);
                }
            }

            // 修正路径
            var workDir = "";
            var file = service.FileName;
            if (file.Contains("/") || file.Contains("\\"))
            {
                file = file.GetFullPath();
                workDir = Path.GetDirectoryName(file);
            }

            var si = new ProcessStartInfo
            {
                FileName = file,
                Arguments = service.Arguments,
                WorkingDirectory = workDir,

                // false时目前控制台合并到当前控制台，一起退出；
                // true时目标控制台独立窗口，不会一起退出；
                UseShellExecute = true,
            };

            var retry = service.Retry;
            if (retry <= 0) retry = 1024;
            for (var i = 0; i < retry; i++)
            {
                try
                {
                    var p = Process.Start(si);

                    WriteLog("应用[{0}]启动成功 PID={1}", service.Name, p.Id);

                    // 记录进程信息，避免宿主重启后无法继续管理
                    _processes[service.Name] = p;
                    pidFile.EnsureDirectory(true);
                    File.WriteAllText(pidFile, $"{p.Id},{p.ProcessName}");

                    return p;
                }
                catch (Exception ex)
                {
                    Log?.Write(LogLevel.Error, "{0}", ex);

                    Thread.Sleep(5_000);
                }
            }

            return null;
        }

        /// <summary>停止管理，按需杀掉进程</summary>
        /// <param name="reason"></param>
        public void Stop(String reason)
        {
            _timer?.TryDispose();

            foreach (var item in _processes)
            {
                var p = item.Value;
                WriteLog("停止应用[{0}] PID={1} {2}", item.Key, p.Id, reason);

                //p.Kill();
            }
            _processes.Clear();
        }

        private TimerX _timer;
        private void DoWork(Object state)
        {
            foreach (var svc in Services)
            {
                if (svc != null && svc.AutoRestart && _processes.TryGetValue(svc.Name, out var p))
                {
                    if (p == null || p.HasExited)
                    {
                        WriteLog("应用[{0}/{1}]已退出，准备重新启动！", svc.Name, p?.Id);

                        StartService(svc);
                    }
                }
            }
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