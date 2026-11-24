using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using StarAgent.Managers;
using Stardust.Deployment;
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

    /// <summary>最大失败数。超过该数时，不再尝试启动，默认10</summary>
    public Int32 MaxFails { get; set; } = 10;

    /// <summary>部署备份根目录。为空时使用 Core.config 的 BackupPath</summary>
    public String? BackupPath { get; set; }

    /// <summary>稳定运行多少分钟后保存为回滚包，默认10分钟</summary>
    public Int32 StableMinutes { get; set; } = 10;

    /// <summary>是否启用启动失败自动回滚，默认true</summary>
    public Boolean AutoRollback { get; set; } = true;

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
    private Boolean _rollbackTried;
    private Boolean _goodMarked;
    private DateTime _goodRetryTime;
    private String? _runZip;
    private String? _runHash;
    private String? _failedHash;
    private String? _failedVersion;
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

            // 连续错误一定数量后，尝试回滚一次，再停止启动
            if (_error >= MaxFails)
            {
                if (_error == MaxFails)
                {
                    WriteLog("Application [{0}] has reached the maximum error count {2} (current: {1})", Name, _error, MaxFails);

                    if (!_rollbackTried && AutoRollback)
                    {
                        _rollbackTried = true;
                        if (TryRollback())
                        {
                            _error = 0;
                            return Start();
                        }
                    }
                    else if (_rollbackTried)
                    {
                        WriteEvent("error", L.TLog("Application [{0}] failed to start {1} times after rollback; both the new and previous packages failed. Manual intervention is required.", Name, MaxFails));
                    }
                    else
                    {
                        WriteEvent("warn", L.TLog("Application [{0}] failed to start {1} times; automatic rollback is disabled.", Name, MaxFails));
                    }
                }

                _error++;
                return false;
            }
            _error++;

            // 修正路径
            var workDir = service.WorkingDirectory;
            var file = service.FileName?.Trim();
            if (file.IsNullOrEmpty())
            {
                WriteLog("The file name of application [{0}] is empty", Name);
                return false;
            }

            if (file.Contains('/') || file.Contains('\\'))
            {
                file = file.GetFullPath();
            }
            if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
            if (workDir.IsNullOrEmpty())
            {
                WriteLog("The working directory of application [{0}] is empty", Name);
                return false;
            }

            workDir = workDir.GetFullPath();
            _fileName = null;
            _workdir = workDir;
            _goodMarked = false;
            _goodRetryTime = DateTime.MinValue;

            // 获取部署策略
            var strategy = _strategy = DeployStrategyFactory.Create(service);
            if (strategy is ITracerFeature tf) tf.Tracer = Tracer;
            var deployMode = strategy.Mode;
            var allowMultiple = DeployStrategyFactory.IsMultipleAllowed(service);

            var args = service.Arguments?.Trim();
            WriteLog("Starting application: {0} {1} workDir={2} Mode={3} Times={4}", file, args, workDir, deployMode, _error);
            if (service.MaxMemory > 0) WriteLog("Memory limit: {0:n0}M", service.MaxMemory);
            if (!AppId.IsNullOrEmpty()) WriteLog("Application code: {0}", AppId);
            if (allowMultiple) WriteLog("Multiple instance mode");

            using var span = Tracer?.NewSpan("StartService", service);
            try
            {
                // 判断 file 是否为 zip 文件
                String? zipFile = service.ZipFile;
                if (zipFile.IsNullOrEmpty() && file.EndsWithIgnoreCase(".zip"))
                    zipFile = file;

                // 记录本次运行使用的包，稳定运行后提升为回滚包。
                // 服务端下发了哈希就直接复用，避免连续失败重试时反复读取大文件
                if (!zipFile.IsNullOrEmpty())
                {
                    var fi = zipFile.AsFile();
                    if (fi == null || !fi.Exists) fi = workDir.CombinePath(zipFile).AsFile();
                    if (fi != null && fi.Exists)
                    {
                        if (_runZip != fi.FullName || _runHash.IsNullOrEmpty())
                        {
                            _runZip = fi.FullName;
                            _runHash = DeployInfo?.Hash;
                            if (_runHash.IsNullOrEmpty()) _runHash = fi.MD5().ToHex();
                        }
                    }
                    else
                    {
                        _runZip = null;
                        _runHash = null;
                    }
                }
                else
                {
                    _runZip = null;
                    _runHash = null;
                }

                // 构建部署上下文
                var context = new DeployContext
                {
                    Name = Name,
                    AppId = AppId,
                    Service = service,
                    Deploy = DeployInfo,
                    WorkingDirectory = workDir,
                    ZipFile = zipFile,
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
                    WriteLog("Failed to extract the deployment package");
                    return false;
                }

                _fileName = context.ExecuteFile;

                // 获取实际部署模式（兼容旧版）
                var actualMode = DeployStrategyFactory.GetActualMode(deployMode);

                // 发布 nginx 站点配置。放在启动进程之前，托管模式（由 nginx 承载）同样需要；一次性任务不发布
                if (actualMode != DeployMode.Task) PublishNginxConfig(workDir);

                // 托管模式，不执行
                if (actualMode == DeployMode.Hosted)
                {
                    WriteLog("Hosted mode; an external host will manage the application");
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

                WriteLog("Started successfully, PID={0}/{1}", p.Id, p.ProcessName);
                CheckStarApp(Path.GetDirectoryName(_fileName), workDir);

                // 任务模式，运行后不守护
                if (actualMode == DeployMode.Task)
                {
                    WriteLog("Task completed; disabling this application service");
                    service.Enable = false;
                    Running = false;
                    return true;
                }

                // 记录进程信息，避免宿主重启后无法继续管理
                SetProcess(p);
                Running = true;
                StartTime = DateTime.Now;

                // 执行健康检查（如果配置了）
                if (!service.HealthCheck.IsNullOrEmpty())
                {
                    // 等待应用启动完成
                    Thread.Sleep(StartWait);
                    
                    // 检查进程是否还在运行
                    if (p.GetHasExited())
                    {
                        WriteLog("Health check failed: the process has exited");
                        Running = false;
                        return false;
                    }

                    // 执行健康检查
                    WriteLog("Performing health check: {0}", service.HealthCheck);
                    var isHealthy = HealthCheckHelper.Check(service.HealthCheck, 5000, new ActionLog(WriteLog));
                    if (!isHealthy)
                    {
                        WriteLog("Health check failed; the application may not have started correctly");
                        // 健康检查失败不影响启动，只记录日志
                    }
                    else
                    {
                        WriteLog("Health check succeeded");
                    }
                }

                // 定时检查文件是否有改变
                StartMonitor();

                // 开放防火墙端口
                OpenFirewallPorts(workDir, args);

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
            WriteLog("Nginx configuration directory: {0}", sites[0].ConfigPath);
            WriteLog("Nginx extension: {0}", sites[0].Extension);

            if (!sites[0].ConfigPath.IsNullOrEmpty())
            {
                foreach (var site in sites)
                {
                    WriteLog("Site: {0}", site.SiteFile);
                    site.Log = new ActionLog(WriteLog);
                    try
                    {
                        var rs = site.Publish();
                        WriteLog(rs ? "Site published successfully: {0}" : "Site is unchanged: {0}", site.SiteFile);
                    }
                    catch (Exception ex)
                    {
                        // 站点配置或证书有问题时只记录日志，不能阻断应用本身的启动
                        WriteLog("Failed to publish site {0}: {1}", site.SiteFile, ex.Message);
                    }
                }
            }
        }
    }

    /// <summary>开放防火墙端口</summary>
    /// <param name="workDir">工作目录</param>
    /// <param name="arguments">命令行参数，用于检测 urls 覆盖的端口</param>
    private void OpenFirewallPorts(String workDir, String? arguments = null)
    {
        try
        {
            var firewall = new FirewallManager { Log = new ActionLog(WriteLog) };

            if (!firewall.Available)
            {
                WriteLog("No usable firewall was detected or access was denied; skipping port opening");
                return;
            }

            WriteLog("Detected firewall type: {0}", firewall.Type);

            // 检测需要开放的端口
            var ports = FirewallManager.DetectPorts(workDir, arguments).ToList();
            if (ports.Count == 0)
            {
                WriteLog("No ports need to be opened");
                return;
            }

            WriteLog("Detected {0} port(s) to open: {1}", ports.Count, String.Join(", ", ports));

            // 开放每个端口
            foreach (var port in ports)
            {
                var ruleName = $"{Name}-{port}";
                var success = firewall.OpenPort(port, ruleName);
                if (!success)
                {
                    WriteLog("Failed to open port {0}", port);
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("An error occurred while opening firewall ports: {0}", ex.Message);
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

        WriteLog("Stopping application PID={0}/{1}, reason: {2}", p.Id, p.ProcessName, reason);

        using var span = Tracer?.NewSpan("StopService", $"{Info?.Name} reason={reason}");
        _timer.TryDispose();
        _timer = null;

        try
        {
            if (!p.GetHasExited() && p.CloseMainWindow())
            {
                WriteLog("Sent the close-window message; waiting for the target process to exit");

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
            WriteLog("Gracefully exiting process PID={0}/{1}, maximum wait {2} ms", p.Id, p.ProcessName, 50 * 200);
            p.SafetyKill(5_000, 50, 200);
        }

        try
        {
            if (!p.GetHasExited())
            {
                WriteLog("Force-terminating process PID={0}/{1}", p.Id, p.ProcessName);
                p.ForceKill();
            }

            if (p.GetHasExited())
            {
                // 外部接管的进程（Process.GetProcessById）不允许读取 ExitCode，
                // 不能因为拿不到退出码就把已正常退出误报为停止失败
                try
                {
                    WriteLog("Process [PID={0}] has exited, ExitCode={1}", p.Id, p.ExitCode);
                }
                catch
                {
                    WriteLog("Process [PID={0}] has exited", p.Id);
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("Failed to exit process [PID={0}]: {1}", p.Id, ex.Message);
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
            ResetErrors();
        }
    }

    /// <summary>重置启动失败计数与回滚状态。新版本到达时调用</summary>
    internal void ResetErrors()
    {
        _error = 0;
        _rollbackTried = false;
        _goodMarked = false;
        _goodRetryTime = DateTime.MinValue;
        _runZip = null;
        _runHash = null;
        _failedHash = null;
        _failedVersion = null;
    }

    /// <summary>判断是否刚刚回滚掉的失败版本。服务端重复下发同一版本时不要清零失败计数，避免新旧包都失败后停不下来</summary>
    /// <param name="deploy">服务端下发的部署信息</param>
    /// <returns>是否同一失败版本</returns>
    internal Boolean IsFailedVersion(DeployInfo? deploy)
    {
        if (deploy == null) return false;

        // 优先按哈希判断；服务端未下发哈希时退回版本号
        if (!deploy.Hash.IsNullOrEmpty()) return !_failedHash.IsNullOrEmpty() && deploy.Hash.EqualIgnoreCase(_failedHash);
        return !_failedVersion.IsNullOrEmpty() && deploy.Version.EqualIgnoreCase(_failedVersion);
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
            WriteLog("Application [{0}/{1}] has exited.", p.Id, Name);

            p = null;
            Process = null;
            Running = false;
        }

        // 进程存在，常规判断内存
        if (p != null) p = CheckMaxMemory(p, inf);
        if (p != null && !p.GetHasExited()) TryMarkGood();

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
                    span?.AppendTag(L.TLog("Failed to get the command line"));
                    continue;
                }

                // 从命令行参数中查找dll/jar文件
                var dllOrJar = args.FirstOrDefault(e => e.EndsWithIgnoreCase(".dll", ".jar"));
                if (dllOrJar.IsNullOrEmpty())
                {
                    span?.AppendTag(L.TLog("No dll/jar in the command line"));
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
                        span?.AppendTag(L.TLog("Failed to get MainModule"));
                        continue;
                    }

                    span?.AppendTag($"mainModule={mainModule}");

                    // 路径匹配
                    if (MatchPath(mainModule, target, targetFullPath, span)) return item;
                }
                catch
                {
                    span?.AppendTag(L.TLog("Failed to get MainModule"));
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

        span?.AppendTag(L.TLog("Path mismatch"));
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

            // PrivateMemorySize64 反映进程真实已提交私有内存，不受 OS 换页影响
            // WorkingSet64 仅反映当前物理驻留集，内存紧张时会被 OS 自动修剪导致误判
            var ws = p.WorkingSet64 / 1024 / 1024;
            var prv = p.PrivateMemorySize64 / 1024 / 1024;
            span?.AppendTag($"MaxMemory={inf.MaxMemory}M WorkingSet64={ws}M PrivateMemorySize64={prv}M");

            var mem = prv;

            // 定期清理内存（仅 Windows，Linux 上 GC 会自动归还 OS）
            if (Runtime.Windows && _nextCollect < DateTime.Now && mem > inf.MaxMemory)
            {
                _nextCollect = DateTime.Now.AddSeconds(120);

                try
                {
                    Runtime.FreeMemory(p.Id);
                }
                catch { }

                p.Refresh();
                ws = p.WorkingSet64 / 1024 / 1024;
                prv = p.PrivateMemorySize64 / 1024 / 1024;
                mem = prv;
                span?.AppendTag($"After FreeMemory: WorkingSet64={ws}M PrivateMemorySize64={prv}M");
            }
            if (mem <= inf.MaxMemory) return p;

            WriteLog("Memory limit exceeded: PrivateMemorySize64={0}M / WorkingSet64={1}M > MaxMemory={2}M", prv, ws, inf.MaxMemory);

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

    #region 部署回滚
    /// <summary>创建部署备份存储</summary>
    /// <returns></returns>
    private DeployBackupStore CreateBackupStore()
    {
        var root = BackupPath;
        if (root.IsNullOrEmpty()) root = Setting.Current.BackupPath;
        if (root.IsNullOrEmpty()) root = "Backup";

        return new DeployBackupStore(root)
        {
            Log = Log,
            Tracer = Tracer,
        };
    }

    /// <summary>解析服务当前使用的 zip 路径</summary>
    /// <param name="service">服务信息</param>
    /// <returns>zip 完整路径，未配置时返回 null</returns>
    private String? ResolveZipPath(ServiceInfo service)
    {
        var zipFile = service.ZipFile;
        if (zipFile.IsNullOrEmpty() && !service.FileName.IsNullOrEmpty() && service.FileName.EndsWithIgnoreCase(".zip"))
            zipFile = service.FileName;
        if (zipFile.IsNullOrEmpty()) return null;

        var fi = zipFile.AsFile();
        if (fi != null && fi.Exists) return fi.FullName;

        var workDir = _workdir;
        if (workDir.IsNullOrEmpty()) workDir = service.WorkingDirectory;
        if (!workDir.IsNullOrEmpty()) return workDir.CombinePath(zipFile).GetFullPath();

        return zipFile.GetFullPath();
    }

    /// <summary>稳定运行足够时长后，把当前包保存为回滚包</summary>
    internal void TryMarkGood()
    {
        try
        {
            if (_goodMarked || !AutoRollback || StableMinutes <= 0) return;
            if (!Running) return;
            if (Name.IsNullOrEmpty() || _goodRetryTime > DateTime.Now) return;
            if (StartTime.Year < 2000 || StartTime.AddMinutes(StableMinutes) > DateTime.Now) return;

            // 只提升本控制器实际拉起过的包，避免把刚下载但尚未运行的包误标为可用包
            var zipFile = _runZip;
            if (zipFile.IsNullOrEmpty() || !File.Exists(zipFile)) return;

            // deploy.json 保存服务端下发的 DeployInfo，没有它就不保存备份
            var deploy = DeployInfo;
            if (deploy == null) return;

            // 服务端未下发哈希时用本地计算的哈希补齐，保证仍能生成回滚包
            if (deploy.Hash.IsNullOrEmpty())
            {
                deploy.Hash = _runHash;
                if (deploy.Hash.IsNullOrEmpty()) deploy.Hash = DeployBackupStore.GetFileHash(zipFile);
            }
            if (deploy.Hash.IsNullOrEmpty()) return;
            if (!_runHash.IsNullOrEmpty() && !deploy.Hash.EqualIgnoreCase(_runHash)) return;

            var service = Info;
            if (service == null) return;

            // Hosted/Task 由外部宿主或一次性任务负责，代理无法判断稳定性
            var strategy = _strategy ?? DeployStrategyFactory.Create(service);
            if (strategy != null && !strategy.NeedGuardian) return;

            var store = CreateBackupStore();
            if (store.Promote(Name, zipFile, deploy))
            {
                _goodMarked = true;
                _runZip = zipFile;
                _runHash = deploy.Hash;

                // 当前版本已被证明稳定，后续失败按环境问题处理，重新判断是否需要回滚
                _rollbackTried = false;

                WriteLog("Application [{0}] has been stable for {1} minutes; saved as the rollback package", Name, StableMinutes);
            }
            else
            {
                _goodRetryTime = DateTime.Now.AddMinutes(5);
            }
        }
        catch (Exception ex)
        {
            _goodRetryTime = DateTime.Now.AddMinutes(5);
            WriteLog("Failed to save the rollback package: {0}", ex.Message);
        }
    }

    /// <summary>连续启动失败后尝试回滚一次</summary>
    /// <returns>是否成功恢复并准备重新启动</returns>
    private Boolean TryRollback()
    {
        try
        {
            return TryRollbackCore();
        }
        catch (Exception ex)
        {
            WriteLog("Rollback failed: {0}", ex.Message);
            WriteEvent("error", L.TLog("Application [{0}] failed to start {1} times; rollback was unavailable or failed. Manual intervention is required.", Name, MaxFails));
            return false;
        }
    }

    private Boolean TryRollbackCore()
    {
        var service = Info;
        if (service == null)
        {
            WriteEvent("error", L.TLog("Application [{0}] failed to start {1} times; rollback was unavailable or failed. Manual intervention is required.", Name, MaxFails));
            return false;
        }

        var zipFile = ResolveZipPath(service);
        if (zipFile.IsNullOrEmpty())
        {
            WriteEvent("warn", L.TLog("Application [{0}] failed to start {1} times; no zip package is available for rollback.", Name, MaxFails));
            return false;
        }

        // 回滚前记下失败版本，用于告警文案与避免同版本重复下发时清零失败计数
        var failedDeploy = DeployInfo;
        var failedHash = failedDeploy?.Hash;
        if (failedHash.IsNullOrEmpty()) failedHash = _runHash;
        var fromVersion = failedDeploy?.Version ?? service.PackageVersion;

        var store = CreateBackupStore();

        // 先停止失败进程，避免 Windows 上文件占用影响覆盖
        Stop("Rollback");

        var result = store.Restore(Name, zipFile, out var backup);
        if (result != DeployRestoreResult.Restored || backup == null)
        {
            if (result == DeployRestoreResult.AlreadyGood)
                WriteEvent("warn", L.TLog("Application [{0}] failed to start {1} times; the current package is already the last known good version. The failure is likely caused by the environment; manual intervention is required.", Name, MaxFails));
            else
                WriteEvent("warn", L.TLog("Application [{0}] failed to start {1} times; no stable rollback package is available.", Name, MaxFails));

            return false;
        }

        // 恢复备份的部署信息，旧包稳定后再写回 deploy.json
        _failedHash = failedHash;
        _failedVersion = fromVersion;
        DeployInfo = backup;
        _goodMarked = false;
        _runZip = zipFile;
        _runHash = backup.Hash;

        WriteLog("Rolled back application [{0}] from version [{1}] to [{2}]", Name, fromVersion, backup.Version);
        WriteEvent("warn", L.TLog("Application [{0}] failed to start {1} times; rolled back from version [{2}] to [{3}].", Name, MaxFails, fromVersion, backup.Version));

        return true;
    }
    #endregion

    Boolean TakeOver(Process p, String reason)
    {
        using var span = Tracer?.NewSpan(nameof(TakeOver), new { p.Id, p.ProcessName, reason });

        WriteLog("Application [{0}/{1}] is already running ({2}); taking over directly", p.Id, Name, reason);

        SetProcess(p);

        // OOM分值。接管已存在进程时修正其OOM分值，仅对 StarAgent 启动的子进程有效（避免对非子进程写入失败）
        var oomScore = Info?.OomScoreAdjust ?? 0;
        if (Runtime.Linux && oomScore != -1000 && StarClient.IsChildProcess(p.Id))
            StarClient.SetOomScoreAdj(p.Id, oomScore);

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

        _error = 0;
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

        if (first) WriteLog("Watching file changes: {0}", di.FullName);

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
            var msg = L.TLog("File [{0}] changed; stopping the application and starting it {1}ms later", changed, Delay);
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
        Log?.InfoPrefixed($"[{Id}/{Name}]", format, args);

        var msg = (args == null || args.Length == 0) ? format : String.Format(format, args);
        DefaultSpan.Current?.AppendTag(msg);

        if (format.Contains("错误") || format.Contains("失败") || format.Contains("error", StringComparison.OrdinalIgnoreCase) || format.Contains("fail", StringComparison.OrdinalIgnoreCase) || format.Contains("exception", StringComparison.OrdinalIgnoreCase))
            WriteEvent("error", L.TLog(format, args));
        else
            WriteEvent("info", L.TLog(format, args));
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