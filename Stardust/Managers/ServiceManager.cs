using System.Diagnostics;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;
using Stardust.Services;

namespace Stardust.Managers
{
    /// <summary>应用服务管理</summary>
    public class ServiceManager : DisposeBase
    {
        #region 属性
        /// <summary>应用服务集合</summary>
        public ServiceInfo[] Services { get; set; }

        private readonly List<ServiceController> _services = new();
        private readonly CsvDb<ProcessInfo> _db;
        #endregion

        #region 构造
        /// <summary>实例化应用服务管理</summary>
        public ServiceManager()
        {
            var data = Setting.Current.DataPath;
            _db = new CsvDb<ProcessInfo>((x, y) => x.Name == y.Name) { FileName = data.CombinePath("Service.csv") };

            _db.Remove(e => e.UpdateTime.AddDays(1) < DateTime.Now);
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
                if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name))) list.Add(item);

            Services = list.ToArray();
        }

        /// <summary>开始管理，拉起应用进程</summary>
        public void Start()
        {
            foreach (var service in Services)
            {
                if (service.AutoStart)
                {
                    WriteLog("启动应用[{0}]：{1} {2} {3}", service.Name, service.FileName, service.Arguments, service.WorkingDirectory);

                    StartService(service);
                }
            }

            _timer = new TimerX(DoWork, null, 30_000, 30_000) { Async = true };
        }

        /// <summary>检查服务。一般用于改变服务后，让其即时生效</summary>
        public void CheckService() => DoWork(null);

        private ServiceController StartService(ServiceInfo service)
        {
            // 检查应用是否已启动
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                try
                {
                    var p = svc.Process ?? Process.GetProcessById(svc.ProcessId);
                    if (p != null && !p.HasExited && p.ProcessName == svc.ProcessName)
                    {
                        WriteLog("应用[{0}/{1}]已启动，直接接管", service.Name, p.Id);

                        svc.SetProcess(p);
                        svc.Save(_db);

                        return svc;
                    }

                    _services.Remove(svc);
                }
                catch (Exception ex)
                {
                    if (ex is not ArgumentException) XTrace.WriteException(ex);
                }
            }

            svc = new ServiceController { Name = service.Name, Info = service };

            // 修正路径
            var workDir = service.WorkingDirectory;
            var file = service.FileName;
            if (file.Contains("/") || file.Contains("\\"))
            {
                file = file.GetFullPath();
                if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
            }

            //var fullFile = file;
            //if (!workDir.IsNullOrEmpty() && !Path.IsPathRooted(fullFile))
            //    fullFile = workDir.CombinePath(fullFile).GetFullPath();

            //// 单实例
            //if (service.Singleton)
            //{
            //    // 遍历进程，检查是否已启动
            //    foreach (var p in Process.GetProcesses())
            //    {
            //        try
            //        {
            //            if (p.ProcessName.EqualIgnoreCase(service.Name) || p.MainModule.FileName.EqualIgnoreCase(fullFile))
            //            {
            //                WriteLog("应用[{0}/{1}]已启动，直接接管", service.Name, p.Id);

            //                svc.SetProcess(p);
            //                svc.Save(_db);

            //                return svc;
            //            }
            //        }
            //        catch { }
            //    }
            //}

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

            //var retry = service.Retry;
            //if (retry <= 0) retry = 1024;
            //for (var i = 0; i < retry; i++)
            //{
            try
            {
                var p = Process.Start(si);

                WriteLog("应用[{0}]启动成功 PID={1}", service.Name, p.Id);

                // 记录进程信息，避免宿主重启后无法继续管理
                svc.SetProcess(p);
                svc.Save(_db);
                _services.Add(svc);

                return svc;
            }
            catch (Exception ex)
            {
                Log?.Write(LogLevel.Error, "{0}", ex);

                //Thread.Sleep(5_000);
            }
            //}

            return null;
        }

        private void StopService(ServiceInfo service)
        {
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                var p = svc.Process;
                if (p != null)
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

                    svc.SetProcess(null);
                }

                _services.Remove(svc);
                _db.Remove(e => e.Name == service.Name);
            }
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
        }

        private TimerX _timer;
        private void DoWork(Object state)
        {
            foreach (var item in Services)
            {
                if (item != null && item.AutoStart)
                {
                    var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(item.Name));
                    if (svc != null)
                    {
                        var p = svc.Process;
                        if (item.AutoRestart && (p == null || p.HasExited))
                        {
                            WriteLog("应用[{0}/{1}]已退出，准备重新启动！", item.Name, p?.Id);

                            StartService(item);
                        }
                        else
                        {
                            WriteLog("新增应用[{0}]，准备启动！", item.Name);

                            StartService(item);
                        }
                    }
                }
            }
        }
        #endregion

        #region 发布事件
        /// <summary>关联订阅事件</summary>
        /// <param name="client"></param>
        public void Attach(StarClient client)
        {
            client.RegisterCommand("deploy/publish", DoControl);
            client.RegisterCommand("deploy/start", DoControl);
            client.RegisterCommand("deploy/stop", DoControl);
            client.RegisterCommand("deploy/restart", DoControl);
        }

        private CommandReplyModel DoControl(CommandModel cmd)
        {
            var my = cmd.Argument.ToJsonEntity<MyApp>();

            XTrace.WriteLine("{0} Id={1} Name={2}", cmd.Command, my.Id, my.AppName);

            var svc = Services.FirstOrDefault(e => e.Name.EqualIgnoreCase(my.AppName));
            switch (cmd.Command)
            {
                case "deploy/publish":
                    break;
                case "deploy/start":
                    if (svc != null) StartService(svc);
                    break;
                case "deploy/stop":
                    if (svc != null) StopService(svc);
                    break;
                case "deploy/restart":
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

            return new CommandReplyModel { Data = "成功" };
        }

        private class MyApp
        {
            public Int32 Id { get; set; }

            public String AppName { get; set; }
        }
        #endregion

        #region 日志
        /// <summary>性能追踪</summary>
        public ITracer Tracer { get; set; }

        /// <summary>日志</summary>
        public ILog Log { get; set; }

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
        #endregion
    }
}