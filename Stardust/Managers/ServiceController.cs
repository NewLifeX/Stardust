using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Deployment;
using Stardust.Models;
using Stardust.Services;

namespace Stardust.Managers;

/// <summary>
/// 应用服务控制器
/// </summary>
internal class ServiceController : DisposeBase
{
    #region 属性
    static Int32 _gid = 0;
    private readonly Int32 _id = Interlocked.Increment(ref _gid);
    /// <summary>编号</summary>
    public Int32 Id => _id;

    /// <summary>服务名</summary>
    public String Name { get; set; }

    /// <summary>进程ID</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名</summary>
    public String ProcessName { get; set; }

    /// <summary>服务信息</summary>
    public ServiceInfo Info { get; private set; }

    /// <summary>进程</summary>
    public Process Process { get; set; }

    /// <summary>是否正在工作</summary>
    public Boolean Running { get; set; }

    /// <summary>监视文件改变的周期。默认5000ms</summary>
    public Int32 MonitorPeriod { get; set; } = 5000;

    /// <summary>延迟时间。重启进程或服务的延迟时间，默认3000ms</summary>
    public Int32 Delay { get; set; } = 3000;

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>最大失败数。超过该数时，不再尝试启动，默认20</summary>
    public Int32 MaxFails { get; set; } = 20;

    /// <summary>事件客户端</summary>
    public IEventProvider EventProvider { get; set; }

    private String _fileName;
    private String _workdir;
    private TimerX _timer;
    private Int32 _error;
    #endregion

    #region 构造
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>检查并启动应用</summary>
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

                return false;
            }
            _error++;

            // 修正路径
            var workDir = service.WorkingDirectory;
            var file = service.FileName?.Trim();
            if (file.IsNullOrEmpty()) return false;

            if (file.Contains('/') || file.Contains('\\'))
            {
                file = file.GetFullPath();
                if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
            }
            _fileName = file;
            _workdir = workDir;

            var args = service.Arguments?.Trim();
            WriteLog("启动应用：{0} {1} workDir={2} Mode={3} Times={4}", file, args, workDir, service.Mode, _error);
            if (service.MaxMemory > 0) WriteLog("内存限制：{0:n0}M", service.MaxMemory);

            using var span = Tracer?.NewSpan("StartService", service);
            try
            {
                Process p;
                var isZip = file.EqualIgnoreCase("ZipDeploy") || file.EndsWithIgnoreCase(".zip");

                // 工作模式
                switch (service.Mode)
                {
                    case ServiceModes.Default:
                        break;
                    case ServiceModes.Extract:
                        WriteLog("解压后不运行，外部主机（如IIS）将托管应用");
                        Extract(service, ref file, workDir);
                        Running = true;
                        return true;
                    case ServiceModes.ExtractAndRun:
                        WriteLog("解压后在工作目录运行");
                        Extract(service, ref file, workDir);
                        isZip = false;
                        break;
                    case ServiceModes.RunOnce:
                        //service.Enable = false;
                        break;
                    default:
                        break;
                }

                if (isZip)
                {
                    var deploy = new ZipDeploy
                    {
                        FileName = file,
                        WorkingDirectory = workDir,

                        Log = XTrace.Log,
                    };

                    // 如果出现超过一次的重启，则打开调试模式，截取控制台输出到日志
                    if (_error > 1) deploy.Debug = true;

                    if (!args.IsNullOrEmpty() && !deploy.Parse(args.Split(" "))) return false;

                    if (!deploy.Execute())
                    {
                        WriteLog("Zip包启动失败！ExitCode={0}", deploy.Process?.ExitCode);

                        // 上报最后错误
                        if (!deploy.LastError.IsNullOrEmpty()) EventProvider?.WriteErrorEvent("ServiceController", deploy.LastError);

                        return false;
                    }

                    _fileName = deploy.ExecuteFile;

                    p = deploy.Process;
                }
                else
                {
                    WriteLog("拉起进程");
                    var si = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = args,
                        WorkingDirectory = workDir,

                        // false时目前控制台合并到当前控制台，一起退出；
                        // true时目标控制台独立窗口，不会一起退出；
                        UseShellExecute = true,
                    };

                    // 如果出现超过一次的重启，则打开调试模式，截取控制台输出到日志
                    if (_error > 1) si.RedirectStandardError = true;

                    p = Process.Start(si);
                    if (p.WaitForExit(3_000) && p.ExitCode != 0)
                    {
                        WriteLog("启动失败！ExitCode={0}", p.ExitCode);

                        if (si.RedirectStandardError)
                        {
                            var rs = p.StandardError.ReadToEnd();
                            WriteLog(rs);
                        }

                        return false;
                    }
                }

                if (p == null) return false;

                WriteLog("启动成功 PID={0}/{1}", p.Id, p.ProcessName);

                if (service.Mode == ServiceModes.RunOnce)
                {
                    WriteLog("单次运行完成，禁用该应用服务");
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

                // 此时还不能清零，因为进程可能不稳定，待定时器检测可靠后清零
                //_error = 0;

                return true;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                Log?.Write(LogLevel.Error, "{0}", ex);
            }

            return false;
        }
    }

    public Boolean Extract(ServiceInfo service, ref String file, String workDir)
    {
        var isZip = file.EqualIgnoreCase("ZipDeploy") || file.EndsWithIgnoreCase(".zip");
        if (!isZip) return false;

        var deploy = new ZipDeploy
        {
            FileName = file,
            WorkingDirectory = workDir,

            Log = XTrace.Log,
        };

        var args = service.Arguments?.Trim();
        if (!args.IsNullOrEmpty() && !deploy.Parse(args.Split(" "))) return false;

        deploy.Extract(workDir);

        var runfile = deploy.FindExeFile(workDir);
        if (runfile == null)
        {
            WriteLog("无法找到名为[{0}]的可执行文件", deploy.FileName);
            return false;
        }

        file = runfile.FullName;

        return true;
    }

    /// <summary>停止应用</summary>
    /// <param name="reason"></param>
    public void Stop(String reason)
    {
        Running = false;

        var p = Process;
        SetProcess(null);
        if (p == null) return;

        WriteLog("停止应用 PID={0}/{1} 原因：{2}", p.Id, p.ProcessName, reason);

        using var span = Tracer?.NewSpan("StopService", $"{Info.Name} reason={reason}");
        _timer.TryDispose();
        _timer = null;

        try
        {
            p.CloseMainWindow();
        }
        catch { }

        try
        {
            if (!p.HasExited) p.Kill();
        }
        catch (Exception ex)
        {
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
        // 进程存在，常规判断内存
        var p = Process;
        if (p != null)
        {
            if (!p.HasExited)
            {
                _error = 0;

                // 检查内存限制
                if (Info.MaxMemory <= 0) return false;

                var mem = p.WorkingSet64 / 1024 / 1024;
                if (mem <= Info.MaxMemory) return true;

                WriteLog("内存超限！{0}>{1}", mem, Info.MaxMemory);

                Stop("内存超限");
            }
            else
            {
                WriteLog("应用[{0}/{1}]已退出！", p.Id, Name);
            }

            p = null;
            Process = null;
        }

        // 进程不存在，但Id存在
        if (p == null && ProcessId > 0)
        {
            try
            {
                p = Process.GetProcessById(ProcessId);
                // 这里的进程名可能是 dotnet/java，照样可以使用
                if (p != null && !p.HasExited && p.ProcessName == ProcessName) return TakeOver(p, $"按[Id={ProcessId}]查找");
            }
            catch (Exception ex)
            {
                if (ex is not ArgumentException) Log?.Error("{0}", ex);
            }

            p = null;
            ProcessId = 0;
        }

        // 进程不存在，但名称存在
        if (p == null && !ProcessName.IsNullOrEmpty())
        {
            if (ProcessName.EqualIgnoreCase("dotnet", "java"))
            {
                var target = _fileName ?? Info?.FileName;
                if (target.EqualIgnoreCase("dotnet", "java"))
                {
                    var ss = Info?.Arguments.Split(' ');
                    if (ss != null) target = ss.FirstOrDefault(e => e.EndsWithIgnoreCase(".dll", ".jar"));
                }
                if (!target.IsNullOrEmpty())
                {
                    target = Path.GetFileName(target);

                    // 遍历所有进程，从命令行参数中找到启动文件名一致的进程
                    foreach (var item in Process.GetProcesses())
                    {
                        if (!item.ProcessName.EqualIgnoreCase(ProcessName)) continue;

                        var name = AppInfo.GetProcessName(item);
                        if (!name.IsNullOrEmpty())
                        {
                            name = Path.GetFileName(name);
                            if (name.EqualIgnoreCase(target)) return TakeOver(item, $"按[{ProcessName} {target}]查找");
                        }
                    }
                }
            }
            else
            {
                var ps = Process.GetProcessesByName(ProcessName);
                if (ps.Length > 0) return TakeOver(ps[0], $"按[Name={ProcessName}]查找");
            }
        }

        // 准备启动进程
        var rs = Start();

        return rs;
    }

    Boolean TakeOver(Process p, String reason)
    {
        WriteLog("应用[{0}/{1}]已启动（{2}），直接接管", p.Id, Name, reason);

        SetProcess(p);
        if (Info != null) StartMonitor();

        if (StartTime.Year < 2000) StartTime = DateTime.Now;

        Running = true;

        return true;
    }

    public void SetProcess(Process process)
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
        }
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

    private readonly Dictionary<String, DateTime> _files = new();

    /// <summary>是否已准备。发生文件变化时，进入就绪状态，持续5秒没有改变后执行重启</summary>
    private Boolean _ready;
    private DateTime _readyTime;

    private void MonitorFileChange(Object state)
    {
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
            var msg = $"文件[{changed}]发生改变，停止应用，延迟{Delay}秒后启动";
            WriteLog(msg);

            // 进入就绪状态
            if (!_ready)
            {
                Stop(msg);

                _ready = true;

                // 快速再次检查
                _timer.SetNext(1000);
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
    public ITracer Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args)
    {
        Log?.Info($"[{Id}/{Name}]{format}", args);

        if (format.Contains("错误") || format.Contains("失败"))
            EventProvider?.WriteErrorEvent("ServiceController", String.Format(format, args));
        else
            EventProvider?.WriteInfoEvent("ServiceController", String.Format(format, args));
    }
    #endregion
}