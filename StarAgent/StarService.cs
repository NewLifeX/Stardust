using System.Collections.Concurrent;
using System.Diagnostics;

using NewLife;
using NewLife.Agent;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Threading;

using Stardust;
using Stardust.Managers;
using Stardust.Models;

namespace StarAgent;

[Api(null)]
public class StarService : DisposeBase, IApi
{
    #region 属性
    /// <summary>
    /// 网络会话
    /// </summary>
    public IApiSession Session { get; set; }

    ///// <summary>服务对象</summary>
    //public ServiceBase Service { get; set; }

    ///// <summary>服务主机</summary>
    //public IHost Host { get; set; }

    /// <summary>应用服务管理</summary>
    public ServiceManager Manager { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider Provider { get; set; }

    ///// <summary>插件管理</summary>
    //public PluginManager PluginManager { get; set; }

    /// <summary>星尘设置</summary>
    public StarSetting StarSetting { get; set; }

    /// <summary>星尘代理设置</summary>
    public StarAgentSetting AgentSetting { get; set; }

    private AgentInfo _agentInfo;
    private TimerX _timer;
    #endregion

    #region 构造
    public StarService()
    {
        // 获取本地进程名比较慢，平均200ms，有时候超过500ms
        //Task.Run(() =>
        //{
        //    _agentInfo = AgentInfo.GetLocal(true);
        //});
        _timer = new TimerX(DoRefreshLocal, null, 100, 5_000) { Async = true };
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
    }
    #endregion

    #region 业务
    /// <summary>信息</summary>
    /// <returns></returns>
    [Api(nameof(Info))]
    public AgentInfo Info(AgentInfo info)
    {
        //XTrace.WriteLine("Info<={0}", info.ToJson());

        var set = StarSetting;

        var ai = _agentInfo ??= AgentInfo.GetLocal(true);
        ai.Server = set.Server;
        ai.Services = Manager?.Services?.Where(e => e.Enable || !e.Name.EqualIgnoreCase("test", "test2")).Select(e => e.Name).ToArray();
        ai.Code = AgentSetting.Code;
        ai.IP = AgentInfo.GetIps();

        var raw = (ControllerContext.Current?.Request as IPacket)?.ToStr();
        if (Provider?.GetService<StarClient>() is StarClient client)
        {
            client.WriteEvent("info", "本地探测", raw);
        }

        // 更新应用服务
        var controller = info == null ? null : Manager?.QueryByProcess(info.ProcessId);
        if (controller != null)
        {
            // 标记为星尘应用，停止Deploy上报进程信息
            controller.IsStarApp = true;

            controller.WriteEvent("本地探测", raw!);
        }

        // 返回插件服务器地址
        var core = NewLife.Setting.Current;
        if (!core.PluginServer.IsNullOrEmpty() && !core.PluginServer.Contains("x.newlifex.com"))
        {
            ai.PluginServer = core.PluginServer;
        }

        return ai;
    }

    /// <summary>本地心跳</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    [Api(nameof(Ping))]
    public IPingResponse Ping(LocalPingInfo info)
    {
        if (info != null && info.ProcessId > 0)
        {
            // 喂狗
            if (info.WatchdogTimeout > 0) FeedDog(info.ProcessId, info.WatchdogTimeout);
        }

        return new PingResponse { ServerTime = DateTime.UtcNow.ToLong() };
    }

    /// <summary>设置星尘服务端地址</summary>
    /// <returns></returns>
    [Api(nameof(SetServer))]
    public String SetServer(String server)
    {
        var set = StarSetting;
        if (set.Server.IsNullOrEmpty() && !server.IsNullOrEmpty())
        {
            set.Server = server;
            set.Save();

            XTrace.WriteLine("StarAgent使用[{0}]送过来的星尘服务端地址：{1}", Session, server);

            if (Provider?.GetService<ServiceBase>() is MyService svc)
            {
                ThreadPool.QueueUserWorkItem(s =>
                {
                    Thread.Sleep(1000);

                    svc.StartFactory();
                    svc.StartClient();
                });
            }
        }

        return set.Server;
    }

    /// <summary>获取所有服务列表</summary>
    /// <returns></returns>
    [Api(nameof(GetServices))]
    public ServicesInfo GetServices()
    {
        CheckLocal();

        var list = Manager.Services;
        var runningList = Manager.RunningServices;

        var result = new ServicesInfo
        {
            Services = list?.ToArray(),
            RunningServices = runningList?.ToArray()
        };

        return result;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns></returns>
    [Api("StartService")]
    public ServiceOperationResult StartService(String serviceName)
    {
        CheckLocal();

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = "服务名称不能为空" };
        }

        try
        {
            var result = Manager?.Start(serviceName);
            return new ServiceOperationResult
            {
                Success = result ?? false,
                Message = result == true ? "服务启动成功" : "服务启动失败或服务不存在",
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = $"启动服务时发生错误: {ex.Message}",
                ServiceName = serviceName
            };
        }
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns></returns>
    [Api("StopService")]
    public ServiceOperationResult StopService(String serviceName)
    {
        CheckLocal();

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = "服务名称不能为空" };
        }

        try
        {
            var result = Manager?.Stop(serviceName, "API调用停止");
            return new ServiceOperationResult
            {
                Success = result ?? false,
                Message = result == true ? "服务停止成功" : "服务停止失败或服务不存在",
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = $"停止服务时发生错误: {ex.Message}",
                ServiceName = serviceName
            };
        }
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns></returns>
    [Api("RestartService")]
    public ServiceOperationResult RestartService(String serviceName)
    {
        CheckLocal();

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = "服务名称不能为空" };
        }

        try
        {
            // 使用共同的重启逻辑
            var success = InternalRestartService(Manager, serviceName, "API调用重启");

            // 根据结果提供更详细的信息
            var message = success ? "服务重启成功" : GetRestartFailureMessage(Manager, serviceName);

            return new ServiceOperationResult
            {
                Success = success,
                Message = message,
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = $"重启服务时发生错误: {ex.Message}",
                ServiceName = serviceName
            };
        }
    }

    /// <summary>获取重启失败的详细原因</summary>
    private static String GetRestartFailureMessage(ServiceManager manager, String serviceName)
    {
        if (manager?.Services?.Any(e => e.Name.EqualIgnoreCase(serviceName)) != true)
        {
            return "服务不存在";
        }

        var isRunning = manager.RunningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;
        if (isRunning)
        {
            return "服务重启失败：停止服务失败";
        }
        else
        {
            return "服务重启失败：启动服务失败";
        }
    }

    private void DoRefreshLocal(Object state)
    {
        var ai = AgentInfo.GetLocal(true);
        if (ai != null)
        {
            //XTrace.WriteLine("IP: {0}", ai.IP);
            _agentInfo = ai;

            // 如果未取得本机IP，则在较短时间内重新获取
            if (_timer != null)
                _timer.Period = ai.IP.IsNullOrEmpty() ? 5_000 : 60_000;
        }
    }

    private void CheckLocal()
    {
        if (Session is INetSession ns && !ns.Remote.Address.IsLocal()) throw new InvalidOperationException("禁止非本机操作！");
    }

    ///// <summary>杀死并启动进程</summary>
    ///// <param name="processId">进程</param>
    ///// <param name="delay">延迟结束的秒数</param>
    ///// <param name="fileName">文件名</param>
    ///// <param name="arguments">参数</param>
    ///// <param name="workingDirectory">工作目录</param>
    ///// <returns></returns>
    //[Api(nameof(KillAndStart))]
    //public Object KillAndStart(Int32 processId, Int32 delay, String fileName, String arguments, String workingDirectory)
    //{
    //    CheckLocal();

    //    var p = Process.GetProcessById(processId);
    //    if (p == null) throw new InvalidOperationException($"无效进程Id[{processId}]");

    //    var name = p.ProcessName;
    //    var pid = 0;

    //    ThreadPool.QueueUserWorkItem(s =>
    //    {
    //        WriteLog("杀死进程 {0}/{1}，等待 {2}秒", processId, p.ProcessName, delay);

    //        if (delay > 0) Thread.Sleep(delay * 1000);

    //        try
    //        {
    //            if (!p.HasExited)
    //            {
    //                p.Kill();
    //                p.WaitForExit(5_000);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            XTrace.WriteException(ex);
    //        }

    //        // 启动进程
    //        if (!fileName.IsNullOrEmpty())
    //        {
    //            WriteLog("启动进程：{0} {1} {2}", fileName, arguments, workingDirectory);

    //            var si = new ProcessStartInfo
    //            {
    //                FileName = fileName,
    //                Arguments = arguments,
    //                WorkingDirectory = workingDirectory,

    //                // false时目前控制台合并到当前控制台，一起退出；
    //                // true时目标控制台独立窗口，不会一起退出；
    //                UseShellExecute = true,
    //            };

    //            var p2 = Process.Start(si);
    //            pid = p2.Id;

    //            WriteLog("应用[{0}]启动成功 PID={1}", p2.ProcessName, p2.Id);
    //        }
    //    });

    //    return new { name, pid };
    //}

    ///// <summary>安装应用服务（星尘代理守护）</summary>
    ///// <param name="service"></param>
    ///// <returns></returns>
    //[Api(nameof(Install))]
    //public ProcessInfo Install(ServiceInfo service)
    //{
    //    XTrace.WriteLine("Install<={0}", service.ToJson());

    //    CheckLocal();

    //    var rs = Manager.Install(service);
    //    if (rs != null)
    //    {
    //        var set = Setting.Current;
    //        set.Services = Manager.Services;
    //        set.Save();
    //    }

    //    return rs;
    //}

    ///// <summary>卸载应用服务</summary>
    ///// <param name="serviceName"></param>
    ///// <returns></returns>
    //[Api(nameof(Uninstall))]
    //public Boolean Uninstall(String serviceName)
    //{
    //    XTrace.WriteLine("Uninstall<={0}", serviceName);

    //    CheckLocal();

    //    var rs = Manager.Uninstall(serviceName, "ServiceUninstall");
    //    if (rs)
    //    {
    //        var set = Setting.Current;
    //        set.Services = Manager.Services;
    //        set.Save();
    //    }

    //    return rs;
    //}
    #endregion

    #region 看门狗
    static ConcurrentDictionary<Int32, DateTime> _dogs = new();
    static TimerX _dogTimer;
    void FeedDog(Int32 pid, Int32 timeout)
    {
        if (pid <= 0 || timeout <= 0) return;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(FeedDog), new { pid, timeout });

        // 更新过期时间，超过该时间未收到心跳，将会重启本应用进程
        _dogs[pid] = DateTime.Now.AddSeconds(timeout);

        _dogTimer ??= new TimerX(CheckDog, Manager, 1000, 15000) { Async = true };
    }

    static void CheckDog(Object state)
    {
        var ks = new List<Int32>();
        var ds = new List<Int32>();
        foreach (var item in _dogs)
        {
            if (item.Value < DateTime.Now)
                ks.Add(item.Key);
            else
            {
                var p = GetProcessById(item.Key);
                if (p == null || p.GetHasExited())
                    ds.Add(item.Key);
            }
        }

        if (ks.Count == 0 && ds.Count == 0) return;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(CheckDog));

        // 重启超时的进程
        foreach (var item in ks)
        {
            try
            {
                var p = GetProcessById(item);
                if (p == null || p.GetHasExited())
                    _dogs.Remove(item);
                else
                {
                    XTrace.WriteLine("进程[{0}/{1}]超过一定时间没有心跳，可能已经假死，准备重启。", p.ProcessName, p.Id);

                    var restartSuccess = false;

                    // 优先尝试通过服务重启
                    if (state is ServiceManager manager)
                    {
                        try
                        {
                            // 通过进程ID查找对应的服务控制器
                            var controller = manager.QueryByProcess(item);
                            if (controller != null && !controller.Name.IsNullOrEmpty())
                            {
                                XTrace.WriteLine("尝试通过服务重启：{0}", controller.Name);
                                span?.AppendTag($"RestartService {controller.Name}");

                                // 使用共同的重启逻辑
                                restartSuccess = InternalRestartService(manager, controller.Name, "看门狗重启");

                                if (restartSuccess)
                                {
                                    XTrace.WriteLine("服务[{0}]重启成功", controller.Name);
                                }
                                else
                                {
                                    XTrace.WriteLine("服务[{0}]重启失败", controller.Name);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }

                    // 如果服务重启失败，则使用原有的SafetyKill方法
                    if (!restartSuccess)
                    {
                        XTrace.WriteLine("服务重启失败，使用SafetyKill强制杀死进程[{0}/{1}]", p.ProcessName, p.Id);
                        span?.AppendTag($"SafetyKill {p.ProcessName}/{p.Id}");
                        p.SafetyKill();
                    }

                    //todo 启动应用。暂时不需要，因为StarAgent会自动启动
                    if (state is ServiceManager manager2) manager2.CheckNow();
                }
            }
            catch (Exception ex)
            {
                _dogs.Remove(item);
                XTrace.WriteException(ex);
            }
        }

        // 删除不存在的进程
        foreach (var item in ds)
        {
            _dogs.Remove(item);
        }
    }

    static Process? GetProcessById(Int32 processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch { }

        return null;
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion

    #region 内部方法
    /// <summary>内部服务重启方法</summary>
    /// <param name="manager">服务管理器</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="reason">重启原因</param>
    /// <returns>是否成功</returns>
    private static Boolean InternalRestartService(ServiceManager manager, String serviceName, String reason = "重启")
    {
        if (manager == null || serviceName.IsNullOrEmpty()) return false;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(InternalRestartService), new { serviceName, reason });

        try
        {
            // 检查服务是否存在
            var service = manager.Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName));
            if (service == null)
            {
                XTrace.WriteLine("服务重启失败：服务[{0}]不存在", serviceName);
                span?.AppendTag("ServiceNotFound");
                return false;
            }

            // 检查服务是否正在运行
            var runningServices = manager.RunningServices;
            var isRunning = runningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;

            XTrace.WriteLine("开始重启服务[{0}]，当前状态：{1}，原因：{2}", serviceName, isRunning ? "运行中" : "已停止", reason);

            if (isRunning)
            {
                // 服务正在运行，先停止服务
                XTrace.WriteLine("停止服务[{0}]", serviceName);
                span?.AppendTag("StopService");

                var stopResult = manager.Stop(serviceName, reason);
                if (stopResult != true)
                {
                    XTrace.WriteLine("服务重启失败：无法停止服务[{0}]", serviceName);
                    span?.AppendTag("StopFailed");
                    return false;
                }

                XTrace.WriteLine("服务[{0}]停止成功，等待1秒后启动", serviceName);
                // 等待服务完全停止
                Thread.Sleep(1000);
            }
            else
            {
                XTrace.WriteLine("服务[{0}]未运行，直接启动", serviceName);
                span?.AppendTag("DirectStart");
            }

            // 启动服务（无论之前是否运行）
            XTrace.WriteLine("启动服务[{0}]", serviceName);
            span?.AppendTag("StartService");

            var startResult = manager.Start(serviceName);
            if (startResult == true)
            {
                XTrace.WriteLine("服务[{0}]重启成功", serviceName);
                span?.AppendTag("Success");
                return true;
            }
            else
            {
                XTrace.WriteLine("服务重启失败：无法启动服务[{0}]", serviceName);
                span?.AppendTag("StartFailed");
                return false;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("服务[{0}]重启时发生异常：{1}", serviceName, ex.Message);
            span?.SetError(ex, null);
            return false;
        }
    }
    #endregion
}