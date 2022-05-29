using System.Diagnostics;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using Stardust.Models;

namespace Stardust.Managers;

/// <summary>
/// 应用服务控制器
/// </summary>
internal class ServiceController
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; }

    /// <summary>进程ID</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名</summary>
    public String ProcessName { get; set; }

    /// <summary>服务信息</summary>
    public ServiceInfo Info { get; set; }

    /// <summary>进程</summary>
    public Process Process { get; set; }
    #endregion

    #region 方法
    /// <summary>检查并启动应用</summary>
    /// <returns></returns>
    public Boolean Start()
    {
        if (Process != null) return true;

        var service = Info;

        // 修正路径
        var workDir = service.WorkingDirectory;
        var file = service.FileName?.Trim();
        if (file.IsNullOrEmpty()) return false;

        if (file.Contains("/") || file.Contains("\\"))
        {
            file = file.GetFullPath();
            if (workDir.IsNullOrEmpty()) workDir = Path.GetDirectoryName(file);
        }

        var args = service.Arguments?.Trim();
        WriteLog("启动应用：{0} {1} {2}", file, args, workDir);

        var si = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = workDir,

            // false时目前控制台合并到当前控制台，一起退出；
            // true时目标控制台独立窗口，不会一起退出；
            UseShellExecute = true,
        };

        try
        {
            var p = Process.Start(si);

            WriteLog("启动成功 PID={0}/{1}", p.Id, p.ProcessName);

            // 记录进程信息，避免宿主重启后无法继续管理
            SetProcess(p);

            return true;
        }
        catch (Exception ex)
        {
            Log?.Write(LogLevel.Error, "{0}", ex);
        }

        return false;
    }

    /// <summary>停止应用</summary>
    /// <param name="reason"></param>
    public void Stop(String reason)
    {
        var p = Process;
        if (p == null) return;

        WriteLog("停止应用 PID={0}/{0} 原因：{2}", p.Id, p.ProcessName, reason);

        try
        {
            p.CloseMainWindow();
        }
        catch { }

        try
        {
            if (!p.HasExited) p.Kill();
        }
        catch { }

        SetProcess(null);
    }

    /// <summary>检查已存在进程并接管，如果进程已退出则重启</summary>
    /// <returns></returns>
    public Boolean Check()
    {
        var p = Process;
        if (p != null)
        {

        }
        else
        {
            try
            {
                p = Process.GetProcessById(ProcessId);
                if (p != null && !p.HasExited && p.ProcessName == ProcessName)
                {
                    WriteLog("应用[{0}/{1}]已启动，直接接管", Name, p.Id);

                    SetProcess(p);

                    return true;
                }
            }
            catch (Exception ex)
            {
                if (ex is not ArgumentException) XTrace.WriteException(ex);
            }
        }

        return false;
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

    public void Save(CsvDb<ProcessInfo> db)
    {
        var pi = db.Find(e => e.Name.EqualIgnoreCase(Name));
        if (pi == null) pi = new ProcessInfo { Name = Name };

        pi.Save(db, Process);
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
    public void WriteLog(String format, params Object[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}