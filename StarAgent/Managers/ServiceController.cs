using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using Stardust.Models;

namespace Stardust.Managers;

/// <summary>
/// 应用服务控制器
/// </summary>
internal class ServiceController : DisposeBase, IServiceController
{
    #region 属性
    static Int32 _gid = 0;
    private readonly Int32 _id = Interlocked.Increment(ref _gid);
    /// <summary>编号</summary>
    public Int32 Id => _id;

    /// <summary>服务名</summary>
    public String Name { get; set; } = null!;

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

    /// <summary>服务处理器集合</summary>
    public IList<IServiceHandler> Handlers { get; set; } = [];

    /// <summary>事件客户端</summary>
    public IEventProvider? EventProvider { get; set; }

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
    public void Init()
    {
        var hs = Handlers;
        if (hs.Count == 0)
        {
            var service = Info ?? throw new ArgumentNullException(nameof(Info));

            switch (service.Mode)
            {
                case ServiceModes.Default:
                case ServiceModes.Multiple:
                    hs.Add(new DefaultServiceHandler());
                    break;
                case ServiceModes.Extract:
                    hs.Add(new ExtractHandler());
                    break;
                case ServiceModes.ExtractAndRun:
                    hs.Add(new ExtractHandler());
                    hs.Add(new ExeHandler());
                    break;
                case ServiceModes.RunOnce:
                    hs.Add(new ExtractHandler());
                    hs.Add(new ExeHandler());
                    break;
                default:
                    break;
            }
        }
    }

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

            // 初始化处理器，设置日志和追踪器
            foreach (var handler in Handlers)
            {
                if (handler is ITracerFeature tf) tf.Tracer = Tracer;
                if (handler is ILogFeature lf) lf.Log = new ActionLog(WriteLog);

                if (handler.Start(service)) return true;
            }

            return false;
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

        Handler?.Stop(reason);
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

        return rs;
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

            // 定期清理内存
            if (Runtime.Windows && _nextCollect < DateTime.Now)
            {
                _nextCollect = DateTime.Now.AddSeconds(600);

                try
                {
                    NativeMethods.EmptyWorkingSet(p.Handle);
                }
                catch { }
            }

            var mem = p.WorkingSet64 / 1024 / 1024;
            span?.AppendTag($"MaxMemory={inf.MaxMemory}M WorkingSet64={mem}M");
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
        if (Info != null) StartMonitor();

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