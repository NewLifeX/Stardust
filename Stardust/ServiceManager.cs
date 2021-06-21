using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;
using Stardust.Services;

namespace Stardust
{
    /// <summary>应用服务管理</summary>
    public class ServiceManager : DisposeBase
    {
        #region 属性
        /// <summary>应用服务集合</summary>
        public ServiceInfo[] Services { get; set; }

        private CsvDb<ProcessInfo> _services;
        private readonly Dictionary<String, Process> _processes = new Dictionary<String, Process>();
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public ServiceManager()
        {
            var data = NewLife.Setting.Current.DataPath;
            _services = new CsvDb<ProcessInfo>((x, y) => x.Name == y.Name) { FileName = data.CombinePath("Service.csv") };

            _services.Remove(e => e.UpdateTime.AddDays(1) < DateTime.Now);
        }

        /// <summary>销毁</summary>
        /// <param name="disposing"></param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            Stop(disposing ? "Dispose" : "GC");
        }
        #endregion

        #region 方法
        /// <summary>添加应用服务</summary>
        /// <param name="services">应用服务集合</param>
        public void Add(ServiceInfo[] services)
        {
            var list = Services.ToList();
            foreach (var item in services)
            {
                if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name))) list.Add(item);
            }

            Services = list.ToArray();
        }

        /// <summary>开始管理，拉起应用进程</summary>
        public void Start()
        {
            foreach (var service in Services)
            {
                WriteLog("启动应用[{0}]：{1} {2} {3}", service.Name, service.FileName, service.Arguments, service.WorkingDirectory);

                if (service.AutoStart) StartService(service);
            }

            _timer = new TimerX(DoWork, null, 30_000, 30_000) { Async = true };
        }

        /// <summary>检查服务。一般用于改变服务后，让其即时生效</summary>
        public void CheckService() => DoWork(null);

        private Process StartService(ServiceInfo service)
        {
            // 检查应用是否已启动
            var pi = _services.Find(e => e.Name.EqualIgnoreCase(service.Name));
            if (pi != null)
            {
                try
                {
                    var p = Process.GetProcessById(pi.ProcessId);
                    if (p != null && !p.HasExited && p.ProcessName == pi.ProcessName)
                    {
                        WriteLog("应用[{0}/{1}]已启动，直接接管", service.Name, p.Id);

                        _processes[service.Name] = p;

                        return p;
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException)) XTrace.WriteException(ex);
                }
            }
            if (pi == null) pi = new ProcessInfo { Name = service.Name };

            // 修正路径
            var workDir = service.WorkingDirectory;
            var file = service.FileName;
            if (file.Contains("/") || file.Contains("\\"))
            {
                file = file.GetFullPath();
                if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
            }

            var fullFile = file;
            if (!workDir.IsNullOrEmpty() && !Path.IsPathRooted(fullFile))
            {
                fullFile = workDir.CombinePath(fullFile).GetFullPath();
            }

            if (service.Singleton)
            {
                // 遍历进程，检查是否已启动
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.ProcessName.EqualIgnoreCase(service.Name) || p.MainModule.FileName.EqualIgnoreCase(fullFile))
                        {
                            WriteLog("应用[{0}/{1}]已启动，直接接管", service.Name, p.Id);

                            _processes[service.Name] = p;
                            pi.Save(_services, p);

                            return p;
                        }
                    }
                    catch { }
                }
            }

            WriteLog("启动进程：{0} {1} {2}", file, service.Arguments, workDir);

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
                    pi.Save(_services, p);

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

        private void StopService(ServiceInfo service)
        {
            if (_processes.TryGetValue(service.Name, out var p))
            {
                WriteLog("停止应用[{0}] PID={1} {2}", service, p.Id, p.ProcessName);

                try
                {
                    p.CloseMainWindow();
                }
                catch { }

                try
                {
                    if (!p.HasExited) p.Kill();
                }
                catch { }

                _processes.Remove(service.Name);
            }

            _services.Remove(e => e.Name == service.Name);
        }

        /// <summary>停止管理，按需杀掉进程</summary>
        /// <param name="reason"></param>
        public void Stop(String reason)
        {
            _timer?.TryDispose();

            foreach (var item in Services)
            {
                if (item.AutoStop) StopService(item);
            }
            //_processes.Clear();
        }

        private TimerX _timer;
        private void DoWork(Object state)
        {
            foreach (var svc in Services)
            {
                if (svc != null && svc.AutoStart)
                {
                    if (_processes.TryGetValue(svc.Name, out var p))
                    {
                        if (svc.AutoRestart && (p == null || p.HasExited))
                        {
                            WriteLog("应用[{0}/{1}]已退出，准备重新启动！", svc.Name, p?.Id);

                            StartService(svc);
                        }
                    }
                    else
                    {
                        WriteLog("新增应用[{0}]，准备启动！", svc.Name);

                        StartService(svc);
                    }
                }
            }
        }
        #endregion

        #region 队列
        /// <summary>关联订阅事件</summary>
        /// <param name="queue"></param>
        public void Attach(IQueueService<CommandModel, Byte[]> queue)
        {
            queue.Subscribe("publish", DoControl);
            queue.Subscribe("start", DoControl);
            queue.Subscribe("stop", DoControl);
            queue.Subscribe("restart", DoControl);
        }

        private Byte[] DoControl(CommandModel cmd)
        {
            //var js = JsonParser.Decode(cmd.Argument);
            var my = cmd.Argument.ToJsonEntity<MyApp>();

            XTrace.WriteLine("{0} Id={1} Name={2}", cmd.Command, my.Id, my.AppName);

            var svc = Services.FirstOrDefault(e => e.Name.EqualIgnoreCase(my.AppName));
            switch (cmd.Command)
            {
                case "publish":
                    break;
                case "start":
                    if (svc != null) StartService(svc);
                    break;
                case "stop":
                    if (svc != null) StopService(svc);
                    break;
                case "restart":
                    if (svc != null)
                    {
                        StopService(svc);
                        Thread.Sleep(1000);
                        StartService(svc);
                    }
                    break;
                default:
                    break;
            }

            var rs = "成功";

            return rs.GetBytes();
        }

        class MyApp
        {
            public Int32 Id { get; set; }

            public String AppName { get; set; }
        }
        #endregion

        #region 辅助
        /// <summary>服务运行信息</summary>
        class ProcessInfo
        {
            public String Name { get; set; }

            public Int32 ProcessId { get; set; }

            public String ProcessName { get; set; }

            public DateTime CreateTime { get; set; }

            public DateTime UpdateTime { get; set; }

            public void Save(CsvDb<ProcessInfo> db, Process p)
            {
                var add = ProcessId == 0;

                ProcessId = p.Id;
                ProcessName = p.ProcessName;

                if (add) CreateTime = DateTime.Now;
                UpdateTime = DateTime.Now;

                if (add)
                    db.Add(this);
                else
                    db.Update(this);
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