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
        ai.Services = Manager?.Services.Where(e => e.Enable || !e.Name.EqualIgnoreCase("test", "test2")).Select(e => e.Name).ToArray();
        ai.Code = AgentSetting.Code;
        ai.IP = AgentInfo.GetIps();

        var raw = (ControllerContext.Current?.Request as IPacket)?.ToStr();
        if (Provider?.GetService<StarClient>() is StarClient client)
        {
            client.WriteEvent("info", "本地探测", raw);
        }

        // 更新应用服务
        var controller = Manager?.QueryByProcess(info.ProcessId);
        if (controller != null)
        {
            // 标记为星尘应用，停止Deploy上报进程信息
            controller.IsStarApp = true;

            controller.WriteEvent("本地探测", raw);
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
    public PingResponse Ping(LocalPingInfo info)
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

                    span?.AppendTag($"SafetyKill {p.ProcessName}/{p.Id}");
                    p.SafetyKill();

                    //todo 启动应用。暂时不需要，因为StarAgent会自动启动
                    if (state is ServiceManager manager) manager.CheckNow();
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

    static Process GetProcessById(Int32 processId)
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
}