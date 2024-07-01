using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using NewLife;
using NewLife.Remoting.Models;

namespace Stardust.Models;

/// <summary>进程信息</summary>
public class AppInfo : IPingRequest, ICloneable
{
    #region 属性
    /// <summary>进程标识</summary>
    public Int32 Id { get; set; }

    /// <summary>进程名称</summary>
    public String? Name { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    ///// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    //public String ClientId { get; set; }

    /// <summary>命令行</summary>
    public String? CommandLine { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

    /// <summary>机器名</summary>
    public String? MachineName { get; set; }

    /// <summary>本地IP地址。随着网卡变动，可能改变</summary>
    public String? IP { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>处理器时间。单位ms</summary>
    public Int32 ProcessorTime { get; set; }

    /// <summary>CPU负载。处理器时间除以物理时间的占比</summary>
    public Double CpuUsage { get; set; }

    /// <summary>物理内存</summary>
    public Int64 WorkingSet { get; set; }

    /// <summary>线程数</summary>
    public Int32 Threads { get; set; }

    /// <summary>线程池可用工作线程数</summary>
    public Int32 WorkerThreads { get; set; }

    /// <summary>线程池可用IO线程数</summary>
    public Int32 IOThreads { get; set; }

    /// <summary>句柄数</summary>
    public Int32 Handles { get; set; }

    /// <summary>连接数</summary>
    public Int32 Connections { get; set; }

    /// <summary>网络端口监听信息</summary>
    public String? Listens { get; set; }

    /// <summary>GC暂停时间占比，百分之一，最大值10</summary>
    public Double GCPause { get; set; }

    /// <summary>采样周期内发生的二代GC次数</summary>
    public Int32 FullGC { get; set; }

    /// <summary>本地UTC时间。ms毫秒</summary>
    public Int64 Time { get; set; }

    static private Int32 _pid = Process.GetCurrentProcess().Id;
    private readonly Process? _process;
    #endregion

    #region 构造
    /// <summary>实例化进程信息</summary>
    public AppInfo() { }

    /// <summary>根据进程对象实例化进程信息</summary>
    /// <param name="process"></param>
    public AppInfo(Process process)
    {
        try
        {
            Id = process.Id;
            Name = process.GetProcessName2();
            //StartTime = process.StartTime;
            //ProcessorTime = (Int64)process.TotalProcessorTime.TotalMilliseconds;

            _process = process;

            Refresh();
        }
        catch (Win32Exception) { }
        catch (InvalidOperationException) { }
    }
    #endregion

    #region 方法
    private Stopwatch? _stopwatch;
    private Int64 _last;
    private Int32 _lastGC2;

    /// <summary>刷新进程相关信息</summary>
    public void Refresh()
    {
        try
        {
            Time = DateTime.UtcNow.ToLong();

            if (_process != null)
            {
                _process.Refresh();

                WorkingSet = _process.WorkingSet64;
                Threads = _process.Threads.Count;
                Handles = _process.HandleCount;
            }

            ThreadPool.GetAvailableThreads(out var worker, out var io);
            WorkerThreads = worker;
            IOThreads = io;

            if (Id == _pid)
                CommandLine = Environment.CommandLine;

            UserName = Environment.UserName;
            MachineName = Environment.MachineName;
            IP = AgentInfo.GetIps();

            // 获取最新机器名
            if (Runtime.Linux)
            {
                var file = @"/etc/hostname";
                if (File.Exists(file)) MachineName = File.ReadAllText(file).Trim();
            }

            try
            {
                // 获取进程的连接数
                var tcps = NetHelper.GetAllTcpConnections(Id);
                if (tcps != null && tcps.Length > 0)
                {
                    Connections = tcps.Count(e => e.ProcessId == Id);
                    Listens = tcps.Where(e => e.ProcessId == Id && e.State == TcpState.Listen).Join(",", e => e.LocalEndPoint);
                }
            }
            catch { }

            // 本进程才能采集GC数据
            if (Id == _pid)
            {
#if NET5_0_OR_GREATER
                var memory = GC.GetGCMemoryInfo();
                GCPause = memory.PauseTimePercentage;
#endif
                var gc2 = GC.CollectionCount(2);
                FullGC = gc2 - _lastGC2;
                _lastGC2 = gc2;
            }

            if (_process != null)
                ProcessorTime = (Int32)_process.TotalProcessorTime.TotalMilliseconds;

            if (_stopwatch == null)
                _stopwatch = Stopwatch.StartNew();
            else
            {
                var ms = _stopwatch.ElapsedMilliseconds;
                if (ms > 0) CpuUsage = Math.Round((Double)(ProcessorTime - _last) / ms, 4);
                _stopwatch.Restart();
            }
            _last = ProcessorTime;

            if (_process != null)
                StartTime = _process.StartTime;
        }
        catch (Win32Exception) { }
    }

    /// <summary>克隆数据</summary>
    /// <returns></returns>
    public AppInfo Clone() => (base.MemberwiseClone() as AppInfo)!;

    Object ICloneable.Clone() => Clone();
    #endregion
}