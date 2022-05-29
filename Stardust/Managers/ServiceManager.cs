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
            {
                if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name))) list.Add(item);
            }

            Services = list.ToArray();
        }

        /// <summary>开始管理，拉起应用进程</summary>
        public void Start()
        {
            if (_timer != null) return;

            foreach (var item in _db.FindAll())
            {
                _services.Add(new ServiceController
                {
                    Name = item.Name,
                    ProcessId = item.ProcessId,

                    Tracer = Tracer,
                    Log = Log,
                });
            }

            _timer = new TimerX(DoWork, null, 0, 30_000) { Async = true };
        }

        /// <summary>停止管理，按需杀掉进程</summary>
        /// <param name="reason"></param>
        public void Stop(String reason)
        {
            _timer?.TryDispose();
            _timer = null;

            foreach (var item in _services)
            {
                var svc = item.Info;
                if (svc != null && svc.AutoStop) StopService(svc, reason);
            }
        }

        /// <summary>检查服务。一般用于改变服务后，让其即时生效</summary>
        public void CheckService() => DoWork(null);
        #endregion

        #region 服务控制
        private ServiceController StartService(ServiceInfo service)
        {
            // 检查应用是否已启动
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                if (svc.Check())
                {
                    svc.Save(_db);
                    return svc;
                }
            }

            var isNew = false;
            if (svc == null)
            {
                svc = new ServiceController { Name = service.Name };
                isNew = true;
            }
            svc.Info = service;

            if (svc.Start())
            {
                svc.Save(_db);

                if (isNew) _services.Add(svc);
            }

            return null;
        }

        private void StopService(ServiceInfo service, String reason)
        {
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                svc.Stop(reason);

                _services.Remove(svc);
                _db.Remove(e => e.Name == service.Name);
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
                        svc.Info = item;
                        svc.Check();
                    }
                    else
                    {
                        svc = new ServiceController { Name = item.Name, Info = item };
                        if (svc.Start())
                        {
                            _services.Add(svc);
                            svc.Save(_db);
                        }
                    }
                    //if (svc != null)
                    //{
                    //    var p = svc.Process;
                    //    if (item.AutoRestart && (p == null || p.HasExited))
                    //    {
                    //        WriteLog("应用[{0}/{1}]已退出，准备重新启动！", item.Name, p?.Id);

                    //        StartService(item);
                    //    }
                    //    else
                    //    {
                    //        WriteLog("新增应用[{0}]，准备启动！", item.Name);

                    //        StartService(item);
                    //    }
                    //}
                }
            }

            // 停止不再使用的服务
            for (var i = _services.Count - 1; i >= 0; i--)
            {
                var svc = _services[i];
                var service = Services.FirstOrDefault(e => e.Name.EqualIgnoreCase(svc.Name));
                if (service == null)
                {
                    svc.Stop("配置停止");
                    _services.RemoveAt(i);
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
                    if (svc != null) StopService(svc, cmd.Command);
                    break;
                case "deploy/restart":
                    if (svc != null)
                    {
                        StopService(svc, cmd.Command);
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