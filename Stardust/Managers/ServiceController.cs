using System.Diagnostics;
using NewLife;
using NewLife.Agent.Windows;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using StarAgent.Managers;
using Stardust.Models;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Managers;

/// <summary>应用服务控制器</summary>
/// <remarks>
/// 负责单个应用服务的生命周期管理：
/// - 启动和停止应用进程
/// - 守护进程（检测退出并重启）
/// - 监控文件变化并热重启
/// - 接管已存在的同名进程
/// 
/// 通过策略模式支持不同的部署方式：
/// - Standard: 解压到工作目录运行（默认）
/// - Shadow: 解压到影子目录运行
/// - Hosted: 仅解压不运行
/// - Task: 一次性任务
/// </remarks>
public class ServiceController : DisposeBase
{
    #region 属性
    static Int32 _gid = 0;
    private readonly Int32 _id = Interlocked.Increment(ref _gid);
    /// <summary>编号</summary>
    public Int32 Id => _id;

    /// <summary>服务名</summary>
    public String Name { get; set; } = null!;

    /// <summary>服务管理器</summary>
    public ServiceManager? Manager { get; set; }

    /// <summary>应用编码</summary>
    public String? AppId { get; set; }

    /// <summary>进程ID</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名</summary>
    public String? ProcessName { get; set; }

    /// <summary>服务信息</summary>
    public ServiceInfo? Info { get; private set; }

    /// <summary>部署信息</summary>
    public DeployInfo? DeployInfo { get; set; }

    /// <summary>进程</summary>
    public Process? Process { get; set; }

    /// <summary>是否正在工作</summary>
    public Boolean Running { get; set; }

    /// <summary>监视文件改变的周期。默认5000ms</summary>
    public Int32 MonitorPeriod { get; set; } = 5000;

    /// <summary>延迟时间。重启进程或服务的延迟时间，默认3000ms</summary>
    public Int32 Delay { get; set; } = 3000;

    /// <summary>启动应用时的等待时间。如果该时间内进程退出，则认为启动失败</summary>
    public Int32 StartWait { get; set; } = 3000;

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>最大失败数。超过该数时，不再尝试启动，默认20</summary>
    public Int32 MaxFails { get; set; } = 20;

    /// <summary>事件客户端</summary>
    public IEventProvider? EventProvider { get; set; }

    /// <summary>是否引用了星尘SDK的APP。这类APP自带性能上报，无需Deploy上报</summary>
    public Boolean IsStarApp { get; set; }

    /// <summary>部署策略</summary>
    private IDeployStrategy? _strategy;

    private String? _fileName;
    private String? _workdir;
    private TimerX? _timer;
    private Int32 _error;
    private AppInfo? _appInfo;
    #endregion

    #region 构造
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>检查并启动应用，等待一会确认进程已启动</summary>
    /// <returns>本次是否成功启动，原来已启动返回false</returns>
    public Boolean Start()
    {
        if (Running) return false;

        // 加锁避免多线程同时启动服务
        lock (this)
        {
            if (Running) return false;

            var service = Info;
            if (service == null) return false;

            // 连续错误一定数量后，不再尝试启动
            if (_error >= MaxFails)
            {
                if (_error == MaxFails) WriteLog("应用[{0}]累计错误次数{1}达到最大值{2}", Name, _error, MaxFails);

                _error++;
                return false;
            }
            _error++;

            // 修正路径
            var workDir = service.WorkingDirectory;
            var file = service.FileName?.Trim();
            if (file.IsNullOrEmpty())
            {
                WriteLog("应用[{0}]文件名为空", Name);
                return false;
            }

            if (file.Contains('/') || file.Contains('\\'))
            {
                file = file.GetFullPath();
            }
            if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
            if (workDir.IsNullOrEmpty())
            {
                WriteLog("应用[{0}]工作目录为空", Name);
                return false;
            }

            workDir = workDir.GetFullPath();
            _fileName = null;
            _workdir = workDir;

            // 获取部署策略
            var strategy = _strategy = DeployStrategyFactory.Create(service);
            if (strategy is ITracerFeature tf) tf.Tracer = Tracer;
            var deployMode = strategy.Mode;
            var allowMultiple = DeployStrategyFactory.IsMultipleAllowed(service);

            var args = service.Arguments?.Trim();
            WriteLog("启动应用：{0} {1} workDir={2} Mode={3} Times={4}", file, args, workDir, deployMode, _error);
            if (service.MaxMemory > 0) WriteLog("内存限制：{0:n0}M", service.MaxMemory);
            if (!AppId.IsNullOrEmpty()) WriteLog("应用编码：{0}", AppId);
            if (allowMultiple) WriteLog("多实例模式");

            using var span = Tracer?.NewSpan("StartService", service);
            try
            {
                // 构建部署上下文
                var context = new DeployContext
                {
                    Name = Name,
                    AppId = AppId,
                    Service = service,
                    Deploy = DeployInfo,
                    WorkingDirectory = workDir,
                    ZipFile = service.ZipFile ?? file,
                    Arguments = args,
                    AllowMultiple = allowMultiple,
                    StartupHook = Manager?.StartupHook ?? false,
                    StartWait = StartWait,
                    Debug = _error > 1,  // 多次重启时开启调试
                    Tracer = Tracer,
                    Log = new ActionLog(WriteLog),
                };

                // 解压部署包
                if (!strategy.Extract(context))
                {
                    WriteLog("解压部署包失败");
                    return false;
                }

                _fileName = context.ExecuteFile;

                // 获取实际部署模式（兼容旧版）
                var actualMode = DeployStrategyFactory.GetActualMode(deployMode);

                // 托管模式，不执行
                if (actualMode == DeployMode.Hosted)
                {
                    WriteLog("托管模式，外部主机将托管应用");
                    //CheckStarApp(context.Shadow, workDir);
                    Running = true;
                    return true;
                }

                // 执行应用
                var p = strategy.Execute(context);
                if (p == null)
                {
                    if (!context.LastError.IsNullOrEmpty()) WriteEvent("error", context.LastError);
                    return false;
                }

                WriteLog("启动成功 PID={0}/{1}", p.Id, p.ProcessName);
                CheckStarApp(Path.GetDirectoryName(_fileName), workDir);

                // 任务模式，运行后不守护
                if (actualMode == DeployMode.Task)
                {
                    WriteLog("任务完成，禁用该应用服务");
                    service.Enable = false;
                    Running = false;
                    return true;
                }

                // 记录进程信息，避免宿主重启后无法继续管理
                SetProcess(p);
                Running = true;
                StartTime = DateTime.Now;

                // 定时检查文件是否有改变
                StartMonitor();

                // 检查nginx配置文件
                PublishNginxConfig(workDir);

                return true;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                Log?.Write(LogLevel.Error, "{0}", ex);
                WriteEvent("error", ex.ToString());
            }

            return false;
        }
    }

    /// <summary>发布Nginx配置</summary>
    /// <param name="workDir">工作目录</param>
    private void PublishNginxConfig(String workDir)
    {
        var sites = NginxDeploy.DetectNginxConfig(workDir).ToList();
        if (sites.Count > 0)
        {
            WriteLog("Nginx配置目录：{0}", sites[0].ConfigPath);
            WriteLog("Nginx扩展名：{0}", sites[0].Extension);

            if (!sites[0].ConfigPath.IsNullOrEmpty())
            {
                foreach (var site in sites)
                {
                    WriteLog("站点：{0}", site.SiteFile);
                    site.Log = new ActionLog(WriteLog);
                    var rs = site.Publish();
                    WriteLog("站点发布{0}！", rs ? "成功" : "无变化");
                }
            }
        }
    }

    private void CheckStarApp(String? exeDir, String workDir)
    {
        // 是否引用了星尘SDK的APP。这类APP自带性能上报，无需Deploy上报
        IsStarApp = false;
        var starFile = exeDir?.CombinePath("Stardust.dll").GetFullPath();
        if (!starFile.IsNullOrEmpty() && File.Exists(starFile))
        {
            IsStarApp = true;
        }
        else
        {
            starFile = workDir.CombinePath("Stardust.dll").GetFullPath();
            if (File.Exists(starFile)) IsStarApp = true;
        }
    }

    /// <summary>停止应用，等待一会确认进程已退出</summary>
    /// <param name="reason"></param>
    public void Stop(String reason)
    {
        Running = false;

        var p = Process;
        SetProcess(null);
        if (p == null) return;

        WriteLog("停止应用 PID={0}/{1} 原因：{2}", p.Id, p.ProcessName, reason);

        using var span = Tracer?.NewSpan("StopService", $"{Info?.Name} reason={reason}");
        _timer.TryDispose();
        _timer = null;

        try
        {
            if (!p.GetHasExited() && p.CloseMainWindow())
            {
                WriteLog("已发送关闭窗口消息，等待目标进程退出");

                for (var i = 0; i < 50 && !p.GetHasExited(); i++)
                {
                    Thread.Sleep(200);
                }
            }
        }
        catch { }

        // 优雅关闭进程
        if (!p.GetHasExited())
        {
            WriteLog("优雅退出进程：PID={0}/{1}，最大等待{2}毫秒", p.Id, p.ProcessName, 50 * 200);
            p.SafetyKill(5_000, 50, 200);
        }

        try
        {
            if (!p.GetHasExited())
            {
                WriteLog("强行结束进程 PID={0}/{1}", p.Id, p.ProcessName);
                p.ForceKill();
            }

            if (p.GetHasExited()) WriteLog("进程[PID={0}]已退出！ExitCode={1}", p.Id, p.ExitCode);
        }
        catch (Exception ex)
        {
            WriteLog("进程[PID={0}]退出失败！{1}", p.Id, ex.Message);
            span?.SetError(ex, null);
        }
    }

    /// <summary>设置服务信息</summary>
    /// <param name="info"></param>
    public void SetInfo(ServiceInfo info)
    {
        if (Info != info)
        {
            Info = info;
            _error = 0;
        }
    }

    /// <summary>检查已存在进程并接管，如果进程已退出则重启</summary>
    /// <returns>本次是否成功启动（或接管），原来已启动返回false</returns>
    public Boolean Check()
    {
        var inf = Info ?? new ServiceInfo();
        using var span = Tracer?.NewSpan("CheckService", inf);

        // 获取当前进程Id
        var mypid = Process.GetCurrentProcess().Id;

        // 判断进程存在
        var p = Process;
        if (p != null && p.GetHasExited())
        {
            WriteLog("应用[{0}/{1}]已退出！", p.Id, Name);

            p = null;
            Process = null;
            Running = false;
        }

        // 进程存在，常规判断内存
        if (p != null) p = CheckMaxMemory(p, inf);

        // 进程不存在，但Id存在
        if (p == null && ProcessId > 0 && ProcessId != mypid)
        {
            span?.AppendTag($"GetProcessById({ProcessId})");
            try
            {
                p = Process.GetProcessById(ProcessId);

                var exited = p == null || p.GetHasExited();

                // 这里的进程名可能是 dotnet/java，照样可以使用
                if (p != null && !exited && p.ProcessName == ProcessName) return TakeOver(p, $"按[Id={ProcessId}]查找");
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                if (ex is not ArgumentException)
                {
                    Log?.Error("{0}", ex);
                    WriteEvent("error", ex.ToString());
                }
            }

            p = null;
            ProcessId = 0;
        }

        // 进程不存在，但名称存在，按名称查找进程
        var allowMultiple = DeployStrategyFactory.IsMultipleAllowed(inf);
        if (p == null && !ProcessName.IsNullOrEmpty())
        {
            p = FindProcessByName(mypid, inf, allowMultiple, span);
            if (p != null) return TakeOver(p, $"按[Name={ProcessName}]查找");
        }

        // 准备启动进程
        var rs = Start();

        // 检测并上报性能
        p = Process;
        if (p != null && EventProvider is StarClient client && !IsStarApp)
        {
            if (_appInfo == null || _appInfo.Id != p.Id)
                _appInfo = new AppInfo(p) { AppName = DeployInfo?.Name ?? inf.Name };
            else
                _appInfo.Refresh();

            TaskEx.Run(() => client.AppPing(_appInfo));
        }

        return rs;
    }

    /// <summary>根据进程名查找目标进程</summary>
    /// <param name="mypid">当前进程ID，用于排除自身</param>
    /// <param name="inf">服务信息</param>
    /// <param name="allowMultiple">是否允许多实例</param>
    /// <param name="span">追踪span</param>
    /// <returns>找到的目标进程，未找到返回null</returns>
    private Process? FindProcessByName(Int32 mypid, ServiceInfo inf, Boolean allowMultiple, ISpan? span)
    {
        var processName = ProcessName;
        if (processName.IsNullOrEmpty()) return null;

        // 获取目标文件用于精确匹配（多实例模式必须，单实例dotnet/java也需要）
        var target = _fileName ?? inf.FileName;
        if (target.EqualIgnoreCase("dotnet", "java"))
        {
            var ss = inf.Arguments?.Split(' ');
            if (ss != null) target = ss.FirstOrDefault(e => e.EndsWithIgnoreCase(".dll", ".jar"));
        }

        // 获取目标文件的完整路径用于精确匹配
        var targetFullPath = target;
        if (!target.IsNullOrEmpty() && !Path.IsPathRooted(target) && !_workdir.IsNullOrEmpty())
            targetFullPath = _workdir.CombinePath(target).GetFullPath();

        var isDotnetJava = processName.EqualIgnoreCase("dotnet", "java");
        span?.AppendTag($"FindProcessByName({processName}) target={target} allowMultiple={allowMultiple}");

        // 遍历所有同名进程
        foreach (var item in Process.GetProcessesByName(processName))
        {
            if (item.Id == mypid || item.GetHasExited()) continue;

            span?.AppendTag($"id={item.Id}");

            // 单实例模式且非dotnet/java，直接返回第一个
            if (!allowMultiple && !isDotnetJava) return item;

            // 需要精确匹配路径：多实例模式 或 dotnet/java进程
            if (target.IsNullOrEmpty()) continue;

            if (isDotnetJava)
            {
                // dotnet/java进程通过命令行参数获取实际执行的dll/jar路径
                var args = ProcessHelper.GetCommandLineArgs(item.Id);
                if (args == null || args.Length == 0)
                {
                    span?.AppendTag($"无法获取命令行");
                    continue;
                }

                // 从命令行参数中查找dll/jar文件
                var dllOrJar = args.FirstOrDefault(e => e.EndsWithIgnoreCase(".dll", ".jar"));
                if (dllOrJar.IsNullOrEmpty())
                {
                    span?.AppendTag($"命令行中无dll/jar");
                    continue;
                }

                span?.AppendTag($"cmdFile={dllOrJar}");

                // 路径匹配：完整路径比对 或 文件名比对
                if (MatchPath(dllOrJar, target, targetFullPath, span)) return item;
            }
            else
            {
                // 普通进程通过MainModule获取执行文件路径
                try
                {
                    var mainModule = item.MainModule?.FileName;
                    if (mainModule.IsNullOrEmpty())
                    {
                        span?.AppendTag($"无法获取MainModule");
                        continue;
                    }

                    span?.AppendTag($"mainModule={mainModule}");

                    // 路径匹配
                    if (MatchPath(mainModule, target, targetFullPath, span)) return item;
                }
                catch
                {
                    span?.AppendTag($"获取MainModule异常");
                }
            }
        }

        return null;
    }

    /// <summary>匹配文件路径</summary>
    /// <param name="actualPath">进程实际文件路径（命令行或MainModule）</param>
    /// <param name="target">目标文件名或路径</param>
    /// <param name="targetFullPath">目标完整路径</param>
    /// <param name="span">追踪span</param>
    /// <returns>是否匹配</returns>
    private Boolean MatchPath(String actualPath, String? target, String? targetFullPath, ISpan? span)
    {
        if (actualPath.IsNullOrEmpty()) return false;

        // 完整路径精确匹配
        if (!targetFullPath.IsNullOrEmpty() && actualPath.EqualIgnoreCase(targetFullPath))
            return true;

        // 工作目录匹配
        if (!_workdir.IsNullOrEmpty() && actualPath.StartsWithIgnoreCase(_workdir))
            return true;

        // target本身包含路径，直接比对
        if (target != null && (target.Contains('/') || target.Contains('\\')))
        {
            if (actualPath.EndsWithIgnoreCase(target)) return true;
        }

        span?.AppendTag($"路径不匹配");
        return false;
    }

    private DateTime _nextCollect;
    private Process? CheckMaxMemory(Process p, ServiceInfo inf)
    {
        var span = DefaultSpan.Current;
        span?.AppendTag("CheckMaxMemory");
        try
        {
            _error = 0;

            // 检查内存限制
            if (inf.MaxMemory <= 0) return p;

            var mem = p.WorkingSet64 / 1024 / 1024;
            span?.AppendTag($"MaxMemory={inf.MaxMemory}M WorkingSet64={mem}M");

            // 定期清理内存
            if (Runtime.Windows && _nextCollect < DateTime.Now && mem > inf.MaxMemory)
            {
                _nextCollect = DateTime.Now.AddSeconds(600);

                try
                {
                    Runtime.FreeMemory(p.Id);
                    //NativeMethods.EmptyWorkingSet(p.Handle);
                }
                catch { }

                p.Refresh();
                mem = p.WorkingSet64 / 1024 / 1024;
            }
            if (mem <= inf.MaxMemory) return p;

            WriteLog("内存超限！{0}>{1}", mem, inf.MaxMemory);

            Stop("内存超限");

            Process = null;
            // 这里不能清空 ProcessId 和 ProcessName，可能因为异常操作导致进程丢了，但是根据名称还能找到。也可能外部启动了进程
            //SetProcess(null);

            Running = false;

            return null;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }

        return p;
    }

    Boolean TakeOver(Process p, String reason)
    {
        using var span = Tracer?.NewSpan(nameof(TakeOver), new { p.Id, p.ProcessName, reason });

        WriteLog("应用[{0}/{1}]已启动（{2}），直接接管", p.Id, Name, reason);

        SetProcess(p);
        var service = Info;
        if (service != null)
        {
            var fileName = service.FileName;
            try
            {
                fileName = p.MainModule?.FileName ?? service.FileName;
            }
            catch { }

            CheckStarApp(Path.GetDirectoryName(fileName)!, service.WorkingDirectory!);

            StartMonitor();
        }

        if (StartTime.Year < 2000) StartTime = DateTime.Now;

        Running = true;

        return true;
    }

    public void SetProcess(Process? process)
    {
        Process = process;
        if (process != null)
        {
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
        }
        else
        {
            ProcessId = 0;
            ProcessName = null;
            _appInfo = null;
        }
    }

    public void LoadModel(ProcessInfo info)
    {
        Name = info.Name;
        ProcessId = info.ProcessId;
        ProcessName = info.ProcessName;
        StartTime = info.CreateTime;
    }

    /// <summary>获取进程信息</summary>
    /// <returns></returns>
    public ProcessInfo ToModel()
    {
        return new ProcessInfo
        {
            Name = Name,
            ProcessId = ProcessId,
            ProcessName = ProcessName,
            CreateTime = StartTime,
            UpdateTime = DateTime.Now,
        };
    }

    private void StartMonitor()
    {
        // 定时检查文件是否有改变
        _timer ??= new TimerX(MonitorFileChange, null, 1_000, MonitorPeriod) { Async = true };
    }

    private readonly Dictionary<String, DateTime> _files = [];

    /// <summary>是否已准备。发生文件变化时，进入就绪状态，持续5秒没有改变后执行重启</summary>
    private Boolean _ready;
    private DateTime _readyTime;

    private void MonitorFileChange(Object? state)
    {
        if (Info?.ReloadOnChange == false) return;

        var first = _files.Count == 0;
        var changed = "";

        // 检查目标目录所有 *.dll 文件
        var dir = _workdir;
        if (dir.IsNullOrEmpty()) dir = Info?.WorkingDirectory;
        if (dir.IsNullOrEmpty()) return;

        var di = dir.AsDirectory();
        if (di == null || !di.Exists) return;

        if (first) WriteLog("监视文件改变：{0}", di.FullName);

        foreach (var fi in di.GetAllFiles("*.dll;*.exe;*.zip;*.jar"))
        {
            var time = fi.LastWriteTime.Trim();
            if (_files.TryGetValue(fi.FullName, out var dt))
            {
                if (dt < time)
                {
                    _files[fi.FullName] = time;
                    changed = fi.FullName;
                }
            }
            else
            {
                _files[fi.FullName] = time;
                changed = fi.FullName;
            }
        }

        using var span = !changed.IsNullOrEmpty() || _ready ?
            Tracer?.NewSpan("ServiceFileChange", changed) :
            null;

        if (!first && !changed.IsNullOrEmpty())
        {
            var msg = $"文件[{changed}]发生改变，停止应用，延迟{Delay}毫秒后启动";
            WriteLog(msg);

            // 进入就绪状态
            if (!_ready)
            {
                Stop(msg);

                _ready = true;

                // 快速再次检查
                _timer?.SetNext(1000);
            }

            // 更新最后就绪时间，该时间之后5秒再启动
            _readyTime = DateTime.Now;
        }

        if (_ready && _readyTime.AddMilliseconds(Delay) < DateTime.Now)
        {
            Start();

            _ready = false;
        }
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
        Log?.Info($"[{Id}/{Name}]{format}", args);

        var msg = (args == null || args.Length == 0) ? format : String.Format(format, args);
        DefaultSpan.Current?.AppendTag(msg);

        if (format.Contains("错误") || format.Contains("失败"))
            WriteEvent("error", msg);
        else
            WriteEvent("info", msg);
    }

    /// <summary>写事件到服务端</summary>
    /// <param name="type"></param>
    /// <param name="msg"></param>
    public void WriteEvent(String type, String msg)
    {
        if (type.IsNullOrEmpty()) type = "info";
        if (Info != null) type = $"{Info.Name}-{type}";

        EventProvider?.WriteEvent(type, nameof(ServiceController), msg);
    }
    #endregion
}