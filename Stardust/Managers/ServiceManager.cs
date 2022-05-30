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

        /// <summary>延迟时间。重启进程或服务的延迟时间，默认3000ms</summary>
        public Int32 Delay { get; set; } = 3000;

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
        /// <summary>添加应用服务，或替换同名服务</summary>
        /// <param name="services">应用服务集合</param>
        public void Add(params ServiceInfo[] services)
        {
            var list = Services.ToList();
            foreach (var item in services)
            {
                //if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name))) list.Add(item);
                var flag = false;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Name.EqualIgnoreCase(item.Name))
                    {
                        list[i] = item;
                        flag = true;
                    }
                }
                if (!flag) list.Add(item);
            }

            Services = list.ToArray();
        }

        /// <summary>开始管理，拉起应用进程</summary>
        public void Start()
        {
            if (_timer != null) return;

            WriteLog("启动应用服务管理");

            // 从数据库加载应用状态
            foreach (var item in _db.FindAll())
            {
                _services.Add(new ServiceController
                {
                    Name = item.Name,
                    ProcessId = item.ProcessId,
                    ProcessName = item.ProcessName,
                    StartTime = item.CreateTime,
                    Delay = Delay,

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
            WriteLog("停止应用服务管理：{0}", reason);

            _timer?.TryDispose();
            _timer = null;

            // 伴随服务停止一起退出
            for (var i = _services.Count - 1; i >= 0; i--)
            {
                var svc = _services[i];
                if (svc.Info != null && svc.Info.AutoStop)
                {
                    svc.Stop(reason);
                    _services.RemoveAt(i);
                }
            }

            SaveDb();
        }

        /// <summary>保存应用状态到数据库</summary>
        private void SaveDb()
        {
            var list = _services.Select(e => e.ToModel()).ToList();

            if (list.Count == 0)
                _db.Clear();
            else
                _db.Write(list, false);
        }

        /// <summary>检查服务。一般用于改变服务后，让其即时生效</summary>
        public void CheckService() => DoWork(null);
        #endregion

        #region 服务控制
        /// <summary>启动服务</summary>
        /// <param name="service"></param>
        /// <returns>本次是否成功启动，原来已启动返回false</returns>
        private Boolean StartService(ServiceInfo service)
        {
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                svc.Info = service;
                return svc.Check();
            }
            else
            {
                svc = new ServiceController
                {
                    Name = service.Name,
                    Info = service,

                    Tracer = Tracer,
                    Log = Log,
                };
                if (svc.Start())
                {
                    _services.Add(svc);
                    return true;
                }
            }

            return false;
        }

        /// <summary>停止服务</summary>
        /// <param name="service"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        private Boolean StopService(ServiceInfo service, String reason)
        {
            var svc = _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (svc != null)
            {
                svc.Stop(reason);

                _services.Remove(svc);

                return true;
            }

            return false;
        }

        private TimerX _timer;
        private void DoWork(Object state)
        {
            var changed = false;
            foreach (var item in Services)
            {
                if (item != null && item.AutoStart)
                {
                    changed |= StartService(item);
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
                    changed = true;
                }
            }

            // 保存状态
            if (changed) SaveDb();
        }
        #endregion

        #region 安装卸载
        /// <summary>安装服务，添加后启动</summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public ProcessInfo Install(ServiceInfo service)
        {
            Add(service);

            if (!StartService(service)) return null;

            SaveDb();

            return _services.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name))?.ToModel();
        }

        /// <summary>卸载服务</summary>
        /// <param name="name"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public Boolean Uninstall(String name, String reason)
        {
            var svc = Services.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
            if (svc == null) return false;

            StopService(svc, reason);

            SaveDb();

            return true;
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

            WriteLog("{0} Id={1} Name={2}", cmd.Command, my.Id, my.AppName);

            var changed = false;
            var svc = Services.FirstOrDefault(e => e.Name.EqualIgnoreCase(my.AppName));
            switch (cmd.Command)
            {
                case "deploy/publish":
                    break;
                case "deploy/start":
                    if (svc != null) changed |= StartService(svc);
                    break;
                case "deploy/stop":
                    if (svc != null) changed |= StopService(svc, cmd.Command);
                    break;
                case "deploy/restart":
                    if (svc != null)
                    {
                        changed |= StopService(svc, cmd.Command);
                        Thread.Sleep(Delay);
                        changed |= StartService(svc);
                    }
                    break;
                default:
                    break;
            }

            // 保存状态
            if (changed) SaveDb();

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