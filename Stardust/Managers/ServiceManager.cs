using System.Net.Http;
using NewLife;
using NewLife.Http;
using NewLife.IO;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;

namespace Stardust.Managers;

/// <summary>应用服务管理</summary>
public class ServiceManager : DisposeBase
{
    #region 属性
    /// <summary>应用服务集合</summary>
    public ServiceInfo[]? Services { get; private set; }

    /// <summary>延迟时间。重启进程或服务的延迟时间，默认3000ms</summary>
    public Int32 Delay { get; set; } = 3000;

    ///// <summary>星尘服务地址</summary>
    //public String Server { get; set; }

    /// <summary>服务改变事件</summary>
    public event EventHandler? ServiceChanged;

    ///// <summary>事件客户端</summary>
    //public IEventProvider EventProvider { get; set; }

    /// <summary>正在运行的应用服务信息</summary>
    private readonly List<ServiceController> _controllers = [];
    private CsvDb<ProcessInfo>? _db;
    private StarClient? _client;
    #endregion

    #region 构造
    /// <summary>实例化应用服务管理</summary>
    public ServiceManager() { }

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
    public Int32 Add(params ServiceInfo[] services)
    {
        var count = 0;
        var list = Services?.ToList() ?? [];
        foreach (var item in services)
        {
            if (item.Name.IsNullOrEmpty()) continue;

            var flag = false;
            for (var i = 0; i < list.Count; i++)
            {
                // 替换更新已有项
                if (list[i].Name.EqualIgnoreCase(item.Name))
                {
                    list[i] = item;
                    flag = true;
                }
            }
            if (!flag)
            {
                list.Add(item);
                count++;
            }
        }

        Services = list.ToArray();

        //_timer?.SetNext(-1);
        CheckNow();

        RaiseServiceChanged();

        return count;
    }

    /// <summary>删除应用服务</summary>
    /// <param name="serviceName"></param>
    /// <returns></returns>
    public Int32 Remove(String serviceName)
    {
        if (serviceName.IsNullOrEmpty()) return 0;

        var count = 0;
        var list = Services?.ToList() ?? [];
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var item = list[i];
            if (item.Name.EqualIgnoreCase(serviceName))
            {
                list.RemoveAt(i);
                count++;
            }
        }

        if (count > 0)
        {
            Services = list.ToArray();

            RaiseServiceChanged();
        }

        return count;
    }

    /// <summary>根据进程查找应用服务控制器</summary>
    /// <param name="processId"></param>
    /// <returns></returns>
    public ServiceController QueryByProcess(Int32 processId)
    {
        if (processId <= 0) return null;

        return _controllers.FirstOrDefault(e => e.ProcessId == processId);
    }

    /// <summary>开始管理，拉起应用进程</summary>
    public void Start()
    {
        if (_timer != null) return;

        using var span = Tracer?.NewSpan("ServiceManager-Start");

        WriteLog("启动应用服务管理");

        var data = Setting.Current.DataPath;
        var db = new CsvDb<ProcessInfo>((x, y) => x?.Name == y?.Name) { FileName = data.CombinePath("Service.csv") };
        db.Remove(e => e.UpdateTime.AddDays(1) < DateTime.Now);
        _db = db;

        // 从数据库加载应用状态
        foreach (var item in db.FindAll())
        {
            _controllers.Add(new ServiceController
            {
                Name = item.Name,
                ProcessId = item.ProcessId,
                ProcessName = item.ProcessName,
                StartTime = item.CreateTime,
                Delay = Delay,

                EventProvider = _client,
                Tracer = Tracer,
                Log = Log,
            });
        }

        _timer = new TimerX(DoWork, null, 1000, 30_000) { Async = true };
    }

    /// <summary>停止管理，按需杀掉进程</summary>
    /// <param name="reason"></param>
    public void Stop(String reason)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Stop", reason);

        WriteLog("停止应用服务管理：{0}", reason);

        _timer?.TryDispose();
        _timer = null;

        // 伴随服务停止一起退出
        var svcs = _controllers;
        for (var i = svcs.Count - 1; i >= 0; i--)
        {
            var ctrl = svcs[i];
            if (ctrl.Info != null && ctrl.Info.AutoStop)
            {
                ctrl.Stop(reason);
                svcs.RemoveAt(i);
            }
        }

        SaveDb();
    }

    /// <summary>
    /// 启动所有应用
    /// </summary>
    public void StartAll()
    {
        Start();

        var svcs = Services;
        if (svcs == null) return;

        var changed = false;
        foreach (var item in svcs)
        {
            if (item != null && item.Enable)
            {
                changed |= StartService(item, null);
            }
        }

        // 保存状态
        if (changed) SaveDb();
    }

    /// <summary>
    /// 停止所有应用
    /// </summary>
    /// <param name="reason"></param>
    public void StopAll(String reason)
    {
        var svcs = _controllers;
        for (var i = svcs.Count - 1; i >= 0; i--)
        {
            var ctrl = svcs[i];
            if (ctrl.Running)
            {
                ctrl.Stop(reason);
                svcs.RemoveAt(i);
            }
        }

        SaveDb();
    }

    /// <summary>保存应用状态到数据库</summary>
    private void SaveDb()
    {
        var db = _db;
        if (db == null) return;

        var list = _controllers.Select(e => e.ToModel()).ToList();

        if (list.Count == 0)
            db.Clear();
        else
            db.Write(list, false);
    }

    /// <summary>检查服务。一般用于改变服务后，让其即时生效</summary>
    //public void CheckService() => DoWork(null);

    private void RaiseServiceChanged() => ServiceChanged?.Invoke(this, EventArgs.Empty);
    #endregion

    #region 服务控制
    /// <summary>检查并启动服务</summary>
    /// <param name="service"></param>
    /// <param name="deploy"></param>
    /// <param name="isCheck">仅检查时，不记录埋点</param>
    /// <returns>本次是否成功启动，原来已启动返回false</returns>
    private Boolean StartService(ServiceInfo service, DeployInfo? deploy, Boolean isCheck = false)
    {
        using var span = isCheck ? null : Tracer?.NewSpan("ServiceManager-StartService", service);
        if (deploy != null) span?.AppendTag(deploy);

        lock (this)
        {
            var controller = _controllers.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name));
            if (controller != null)
            {
                controller.EventProvider = _client;
                controller.SetInfo(service);

                if (deploy != null) controller.DeployInfo = deploy;

                return controller.Check();
            }

            controller = new ServiceController
            {
                Name = service.Name,
                //Info = service,
                DeployInfo = deploy,

                EventProvider = _client,
                Tracer = Tracer,
                Log = Log,
            };
            controller.SetInfo(service);
            _controllers.Add(controller);

            Fix(service);
            if (controller.Start())
            {
                var svc = controller.Info;
                if (svc != null && svc.Mode == ServiceModes.RunOnce && !svc.Enable) RaiseServiceChanged();

                return true;
            }
        }

        return false;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    private Boolean StopService(String serviceName, String reason)
    {
        using var span = Tracer?.NewSpan("ServiceManager-StopService", serviceName);

        var controller = _controllers.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName));
        if (controller != null)
        {
            // 先删除再停止，避免并发
            _controllers.Remove(controller);

            controller.Stop(reason);
            controller.TryDispose();

            return true;
        }
        else
        {
            span?.AppendTag($"无法找到服务[{serviceName}]的控制器");
        }

        return false;
    }

    /// <summary>
    /// 设置服务集合，常用于读取配置文件后设置
    /// </summary>
    /// <param name="services"></param>
    public void SetServices(ServiceInfo[] services)
    {
        // 一般由外部配置文件改变而驱动，所以本方法不会引发ServiceChanged事件

        var svcs = Services;
        var flag = svcs == null || svcs.Length != services.Length;
        if (!flag)
        {
            foreach (var item in services)
            {
                // 如果新服务在原列表里不存在，或者数值不同，则认为有改变
                var s = svcs?.FirstOrDefault(e => e.Name == item.Name);
                if (s == null || s != item && s.ToJson() != item.ToJson())
                {
                    flag = true;
                    break;
                }
            }
        }

        if (flag)
        {
            using var span = Tracer?.NewSpan("ServiceManager-SetServices", services);

            WriteLog("应用服务配置变更");

            // 拷贝，避免应用后无法及时感知变化。具体ServiceInfo也要克隆，否则上面对比service配置信息时永远失败
            Services = services.Select(e => e.Clone()).ToArray();

            _status = 0;
            //_timer?.SetNext(-1);
            CheckNow();
        }
    }

    private async Task UploadService(ServiceInfo[] svcs)
    {
        if (svcs == null) return;
        if (_client == null) return;

        svcs = svcs.Where(e => !e.Name.IsNullOrEmpty()).ToArray();
        if (svcs.Length == 0) return;

        using var span = Tracer?.NewSpan(nameof(UploadService), svcs);
        WriteLog("上报应用服务 {0}", svcs.Join(",", e => e.Name));

        await _client.UploadDeploy(svcs);
    }

    private async Task<DeployInfo[]?> PullService(Int32 deployId, String? appName)
    {
        if (Services == null) return null;
        if (_client == null) return null;

        using var span = Tracer?.NewSpan(nameof(PullService), appName);
        WriteLog("拉取应用服务：{0}", appName ?? "（所有）");

        var svcs = Services.ToList();

        var rs = await _client.GetDeploy();
        if (rs == null) return null;

        // 数据修正，特别是部署名
        foreach (var deploy in rs)
        {
            var svc = deploy.Service ??= new ServiceInfo();
            if (svc.Name.IsNullOrEmpty())
                svc.Name = deploy.Name;
        }

        // 过滤应用
        if (deployId > 0 && rs.All(e => e.Id > 0))
            rs = rs.Where(e => e.Id == deployId).ToArray();
        else if (!appName.IsNullOrEmpty())
            rs = rs.Where(e => e.Service.Name.EqualIgnoreCase(appName)).ToArray();

        if (rs.Length > 0)
        {
            WriteLog("取得应用服务：{0}", rs.Join(",", e => e.Service.Name));
            WriteLog("可用：{0}", rs.Where(e => e.Service != null && e.Service.Enable).Join(",", e => e.Service.Name));
            WriteLog(rs.ToJson(true));
        }

        // 旧版服务
        span?.AppendTag("旧版服务：" + svcs?.Select(e => e.Name).ToList());

        // 合并
        foreach (var item in rs)
        {
            var svc = item.Service;
            if (svc == null || svc.Name.IsNullOrEmpty()) continue;

            // 外层应用名，内层部署名。主要用于单应用多部署场景
            var deployName = svc.Name;

            // 下载文件到工作目录
            Fix(svc);
            if (svc.Enable && !item.Url.IsNullOrEmpty()) await Download(item, svc);

            var old = svcs.FirstOrDefault(e => e.Name.EqualIgnoreCase(deployName));
            if (old == null)
            {
                WriteLog("新增[{0}]：Enable={1}", deployName, svc.Enable);

                old = svc;
                //svc.ReloadOnChange = true;

                svcs.Add(old);
            }
            else
            {
                if (!old.Enable && svc.Enable)
                    WriteLog("启用[{0}]", deployName);
                else if (old.Enable && !svc.Enable)
                    WriteLog("禁用[{0}]", deployName);

                old.FileName = svc.FileName;
                old.Arguments = svc.Arguments;
                old.WorkingDirectory = svc.WorkingDirectory;
                old.UserName = svc.UserName;
                old.Environments = svc.Environments;
                old.Enable = svc.Enable;
                old.AutoStop = svc.AutoStop;
                old.ReloadOnChange = svc.ReloadOnChange;
                old.MaxMemory = svc.MaxMemory;
                old.Mode = svc.Mode;
                old.ZipFile = svc.ZipFile;
            }
        }

        // 新版服务
        span?.AppendTag(svcs);

        Services = svcs.ToArray();

        RaiseServiceChanged();

        return rs;
    }

    void Fix(ServiceInfo svc)
    {
        var name = svc.Name;
        if (!name.IsNullOrEmpty())
        {
            if (svc.FileName.IsNullOrEmpty()) svc.FileName = $"{name}.zip";

            // 如果没有设置工作目录，默认工作目录是上一级的apps子目录，按应用放置
            if (svc.WorkingDirectory.IsNullOrEmpty()) svc.WorkingDirectory = $"../apps/{name}";
        }
    }

    async Task Download(DeployInfo info, ServiceInfo svc)
    {
        var url = info.Url;
        if (url.IsNullOrEmpty()) return;

        using var span = Tracer?.NewSpan("ServiceManager-Download", info.Url);

        // 下载的文件名必须是压缩包
        var zipName = svc.FileName;
        if (!zipName.EndsWithIgnoreCase(".zip")) zipName = $"{svc.Name}.zip";

        var dst = svc.WorkingDirectory.CombinePath(zipName).AsFile();
        span?.AppendTag(dst.FullName);

        var flag = false;
        if (!dst.Exists)
        {
            WriteLog("文件不存在：{0}", dst);
            flag = true;
        }
        else if (!info.Hash.IsNullOrEmpty())
        {
            var hash = dst.MD5().ToHex();
            if (!hash.EqualIgnoreCase(info.Hash))
            {
                WriteLog("文件哈希不匹配：{0}（本地）!={1}（远程）", hash, info.Hash);
                flag = true;
            }
        }
        if (!flag)
        {
            var hash = dst.MD5().ToHex();
            WriteLog("文件已存在：{0} MD5: {1}", dst, hash);

            svc.ZipFile = dst.FullName;
        }
        if (flag)
        {
            if (_client != null) url = _client.BuildUrl(url);

            WriteLog("下载[{0}]：{1} {2}", svc.Name, info.Version, url);

            // 先下载到临时目录，再整体拷贝，避免进程退出
            var tmp = Path.GetTempFileName();

            var http = new HttpClient();
            await http.DownloadFileAsync(url, tmp);

            WriteLog("下载完成，准备覆盖：{0}", dst.FullName);

            // 校验哈希
            var ti = tmp.AsFile();
            var hash = ti.MD5().ToHex();
            if (!info.Hash.IsNullOrEmpty() && !hash.EqualIgnoreCase(info.Hash))
                WriteLog("下载失败，校验错误！{0}（本地）!={1}（远程）", hash, info.Hash);
            else
            {
                // 删除原文件
                if (dst.Exists)
                {
                    try
                    {
                        dst.Delete();
                    }
                    catch
                    {
                        dst.MoveTo(dst.FullName + ".del");
                    }
                }

                // 创建目录，下载到临时目录的文件拷贝到这里
                dst.FullName.EnsureDirectory(true);
                ti.MoveTo(dst.FullName);

                svc.ZipFile = dst.FullName;
            }
        }
    }

    Int32 _status;
    Boolean _busy;
    private TimerX? _timer;
    private async Task DoWork(Object state)
    {
        // 如果其它任务正在使用管理器，则跳过这一次检查
        if (_busy) return;

        var svcs = Services;
        if (svcs == null) return;

        using var span = Tracer?.NewSpan("ServiceManager-DoWork", svcs.Length);

        // 应用服务的上报和拉取
        DeployInfo[]? deploys = null;
        if (_client != null && _client.Logined)
        {
            // 上传失败不应该影响本地拉起服务
            try
            {
                //if (_status == 0 && svcs.Length > 0)
                if (_status == 0)
                {
                    var svcs2 = svcs.Where(e => e.Enable || !e.Name.EqualIgnoreCase("test", "test2")).ToArray();
                    if (svcs2.Length > 0) await UploadService(svcs2);

                    _status = 1;
                }

                if (_status == 1)
                {
                    deploys = await PullService(-1, null);

                    _status = 2;
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        // 检查应用服务变化，先停止再启动
        var changed = false;
        svcs = Services ?? [];

        // 停止不再使用的服务
        var controllers = _controllers;
        for (var i = controllers.Count - 1; i >= 0; i--)
        {
            var controller = controllers[i];
            var service = svcs.FirstOrDefault(e => e.Name.EqualIgnoreCase(controller.Name));
            if (service == null || !service.Enable)
            {
                controller.Stop("配置停止");
                controllers.RemoveAt(i);
                changed = true;
            }
            else if (controller.Running && controller.Info != null && service.ToJson() != controller.Info.ToJson())
            {
                // 启动成功的短时间内，不认可配置改变，因为可能就是这一次发布的配置变化
                if (controller.StartTime.Year > 2000 && controller.StartTime.AddSeconds(5) < DateTime.Now)
                {
                    controller.Stop("配置改变");
                    controllers.RemoveAt(i);
                    changed = true;
                }
            }
        }

        // 检查并启动服务
        foreach (var svc in svcs)
        {
            if (svc != null && svc.Enable)
            {
                changed |= StartService(svc, deploys?.FirstOrDefault(e => e.Service.Name == svc.Name), true);
            }
        }

        // 保存状态
        if (changed) SaveDb();
    }

    /// <summary>马上检查应用服务</summary>
    public void CheckNow() => _timer?.SetNext(-1);

    private IDisposable CreateBusy()
    {
        _busy = true;

        return new MyBusy(this);
    }

    class MyBusy : IDisposable
    {
        private ServiceManager _mgr;
        public MyBusy(ServiceManager mgr) => _mgr = mgr;

        public void Dispose() => _mgr._busy = false;
    }
    #endregion

    #region 安装卸载
    /// <summary>安装服务，添加后启动</summary>
    /// <param name="service"></param>
    /// <returns></returns>
    public ProcessInfo? Install(ServiceInfo service)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Install", service);

        // 设置繁忙，避免同步进行健康检查
        using var busy = CreateBusy();

        Add(service);

        if (!StartService(service, null)) return null;

        SaveDb();

        return _controllers.FirstOrDefault(e => e.Name.EqualIgnoreCase(service.Name))?.ToModel();
    }

    /// <summary>卸载服务</summary>
    /// <param name="name"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public Boolean Uninstall(String name, String reason)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Uninstall", new { name, reason });

        var svc = Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (svc == null) return false;

        // 设置繁忙，避免同步进行健康检查
        using var busy = CreateBusy();

        StopService(svc.Name, reason);

        SaveDb();

        Remove(svc.Name);

        return true;
    }
    #endregion

    #region 发布事件
    /// <summary>关联订阅事件</summary>
    /// <param name="client"></param>
    public void Attach(StarClient client)
    {
        _client = client;

        // 设置繁忙，避免同步进行健康检查
        using var busy = CreateBusy();

        client.RegisterCommand("deploy/publish", DoControl);
        client.RegisterCommand("deploy/install", DoControl);
        client.RegisterCommand("deploy/start", DoControl);
        client.RegisterCommand("deploy/stop", DoControl);
        client.RegisterCommand("deploy/restart", DoControl);
        client.RegisterCommand("deploy/uninstall", DoControl);

        //_timer?.SetNext(-1);
        CheckNow();
    }

    private CommandReplyModel DoControl(CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-DoControl", cmd);

        var my = cmd.Argument?.ToJsonEntity<MyApp>();
        var serviceName = my?.DeployName ?? my?.AppName;
        if (my == null || serviceName.IsNullOrEmpty())
            return new CommandReplyModel { Status = CommandStatus.错误, Data = "参数错误" };

        WriteLog("{0} Id={1} Name={2}", cmd.Command, my.Id, serviceName);

        var changed = cmd.Command switch
        {
            "deploy/publish" => OnInstall(my.Id, serviceName, cmd),
            "deploy/install" => OnInstall(my.Id, serviceName, cmd),
            "deploy/start" => OnStart(serviceName, cmd),
            "deploy/stop" => OnStop(serviceName, cmd),
            "deploy/restart" => OnRestart(serviceName, cmd),
            "deploy/uninstall" => OnUninstall(serviceName, cmd),
            _ => throw new Exception($"不支持命令[{cmd.Command}]"),
        };

        // 保存状态
        if (changed) SaveDb();

        return new CommandReplyModel { Data = "成功" };
    }

    private class MyApp
    {
        public Int32 Id { get; set; }

        public String? DeployName { get; set; }

        public String? AppName { get; set; }
    }

    Boolean OnInstall(Int32 deployId, String serviceName, CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Install", cmd);

        var dis = PullService(deployId, serviceName).ConfigureAwait(false).GetAwaiter().GetResult();
        if (dis == null || dis.Length == 0) throw new Exception($"无法从服务器取得应用信息，安装{serviceName}失败！");

        // 马上停止并拉起应用服务，定时器只用于双保险
        var svc = dis[0]?.Service;
        if (svc != null)
        {
            var rs = StopService(svc.Name, cmd.Command);
            //if (rs) Thread.Sleep(Delay);
            StartService(svc, dis[0]);
        }
        else
        {
            span?.AppendTag($"无法找到{dis[0]}的服务");
        }

        // 尽快调度一次，拉起服务
        //_timer?.SetNext(-1);
        CheckNow();

        return true;
    }

    Boolean OnStart(String serviceName, CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Start", cmd);

        var svc = (Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName)))
            ?? throw new Exception($"无法找到服务[{serviceName}]");

        var changed = false;
        svc.Enable = true;
        changed |= StartService(svc, null);

        RaiseServiceChanged();

        return changed;
    }

    Boolean OnStop(String serviceName, CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Stop", cmd);

        var svc = (Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName)))
            ?? throw new Exception($"无法找到服务[{serviceName}]");

        var changed = false;
        svc.Enable = false;
        changed |= StopService(svc.Name, cmd.Command);

        RaiseServiceChanged();

        return changed;
    }

    Boolean OnRestart(String serviceName, CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Restart", cmd);

        var svc = (Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName)))
            ?? throw new Exception($"无法找到服务[{serviceName}]");

        var changed = false;
        svc.Enable = false;
        changed |= StopService(svc.Name, cmd.Command);
        //if (changed) Thread.Sleep(Delay);
        svc.Enable = true;
        changed |= StartService(svc, null);

        RaiseServiceChanged();

        return changed;
    }

    Boolean OnUninstall(String serviceName, CommandModel cmd)
    {
        using var span = Tracer?.NewSpan("ServiceManager-Uninstall", cmd);

        var svc = (Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName)))
            ?? throw new Exception($"无法找到服务[{serviceName}]");

        var changed = false;
        svc.Enable = false;
        changed |= StopService(svc.Name, cmd.Command);

        Remove(serviceName);

        //RaiseServiceChanged();

        return changed;
    }
    #endregion

    #region 日志
    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args)
    {
        Log?.Info(format, args);

        var msg = (args == null || args.Length == 0) ? format : String.Format(format, args);
        DefaultSpan.Current?.AppendTag(msg);

        if (format.Contains("错误") || format.Contains("失败"))
            _client?.WriteErrorEvent(nameof(ServiceManager), msg);
        else
            _client?.WriteInfoEvent(nameof(ServiceManager), msg);
    }
    #endregion
}