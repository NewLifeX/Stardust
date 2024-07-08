using System.Diagnostics;
using NewLife;
using NewLife.Log;
using Stardust.Models;

#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Managers;

/// <summary>监控处理器</summary>
public class MonitorHandler : IServiceHandler, ITracerFeature, ILogFeature
{
    #region 属性
    /// <summary>服务控制器</summary>
    public IServiceController Controller { get; set; } = null!;
    #endregion

    /// <summary>启动服务</summary>
    /// <returns></returns>
    public Boolean Start(ServiceInfo service)
    {
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

        var args = service.Arguments?.Trim();
        WriteLog("启动应用：{0} {1} workDir={2} Mode={3} Times={4}", file, args, workDir, service.Mode, _error);
        if (service.MaxMemory > 0) WriteLog("内存限制：{0:n0}M", service.MaxMemory);

        var src = service.ZipFile ?? file;
        using var span = Tracer?.NewSpan("StartService", service);
        try
        {
            Process? p;
            var isZip = src.EndsWithIgnoreCase(".zip");

            // 在环境变量中设置BasePath，不用担心影响当前进程，因为PathHelper仅读取一次
            //Environment.SetEnvironmentVariable("BasePath", workDir);

            // 工作模式
            switch (service.Mode)
            {
                case ServiceModes.Default:
                case ServiceModes.Multiple:
                    break;
                case ServiceModes.Extract:
                    WriteLog("解压后不运行，外部主机（如IIS）将托管应用");
                    Extract(src, args, workDir, false);
                    Running = true;
                    return true;
                case ServiceModes.ExtractAndRun:
                    WriteLog("解压后在工作目录运行");
                    var deploy = Extract(src, args, workDir, false);
                    if (deploy == null) throw new Exception("解压缩失败");

                    //file ??= deploy.ExecuteFile;
                    var runfile = deploy.FindExeFile(workDir);
                    file = runfile?.FullName;
                    if (file.IsNullOrEmpty()) throw new Exception("无法找到启动文件");

                    args = deploy.Arguments;
                    //_fileName = deploy.ExecuteFile;
                    isZip = false;
                    break;
                case ServiceModes.RunOnce:
                    //service.Enable = false;
                    break;
                default:
                    break;
            }

            if (isZip)
                p = RunZip(file, args, workDir, service);
            else
                p = RunExe(file, args, workDir, service);

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
            WriteEvent("error", ex.ToString());
        }

        return false;
    }

    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    public void Stop(String reason)
    {
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
            p.SafetyKill(50, 200);
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

    /// <summary>检查服务是否正常</summary>
    /// <returns></returns>
    public Boolean Check()
    {
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

        // 进程不存在，但名称存在
        if (p == null && !ProcessName.IsNullOrEmpty() && inf.Mode != ServiceModes.Multiple)
        {
            if (ProcessName.EqualIgnoreCase("dotnet", "java"))
            {
                var target = _fileName ?? inf.FileName;
                if (target.EqualIgnoreCase("dotnet", "java"))
                {
                    var ss = inf.Arguments?.Split(' ');
                    if (ss != null) target = ss.FirstOrDefault(e => e.EndsWithIgnoreCase(".dll", ".jar"));
                }
                if (!target.IsNullOrEmpty())
                {
                    //target = Path.GetFileName(target);
                    span?.AppendTag($"GetProcessesByFile({target}) ProcessName={ProcessName}");

                    // 遍历所有进程，从命令行参数中找到启动文件名一致的进程
                    foreach (var item in Process.GetProcesses())
                    {
                        if (item.Id == mypid || item.GetHasExited()) continue;
                        if (!item.ProcessName.EqualIgnoreCase(ProcessName)) continue;

                        var name = StarHelper.GetProcessName(item);
                        if (!name.IsNullOrEmpty())
                        {
                            span?.AppendTag($"id={item.Id} name={name}");

                            // target有可能是文件全路径，此时需要比对无后缀文件名
                            if (name.EqualIgnoreCase(target, Path.GetFileNameWithoutExtension(target)))
                                return TakeOver(item, $"按[{ProcessName} {target}]查找");
                        }
                    }
                }
            }
            else
            {
                span?.AppendTag($"GetProcessesByName({ProcessName})");

                var ps = Process.GetProcessesByName(ProcessName).Where(e => e.Id != mypid && !e.GetHasExited()).ToArray();
                if (ps.Length > 0) return TakeOver(ps[0], $"按[Name={ProcessName}]查找");
            }
        }

        // 准备启动进程
        var rs = Start();

        // 检测并上报性能
        p = Process;
        if (p != null && EventProvider is StarClient client)
        {
            if (_appInfo == null || _appInfo.Id != p.Id)
                _appInfo = new AppInfo(p) { AppName = inf.Name };
            else
                _appInfo.Refresh();

            TaskEx.Run(() => client.AppPing(_appInfo));
        }

        return rs;
    }

    private Process? RunExe(String file, String? args, String workDir, ServiceInfo service)
    {
        //WriteLog("拉起进程：{0} {1}", file, args);

        var fileName = workDir.CombinePath(file).GetFullPath();
        if (!fileName.AsFile().Exists)
        {
            fileName = file;
        }

        if (file.EndsWithIgnoreCase(".dll"))
        {
            args = $"{fileName} {args}";
            fileName = "dotnet";
        }
        else if (file.EndsWithIgnoreCase(".jar"))
        {
            args = $"{fileName} {args}";
            fileName = "java";
        }

        var si = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args ?? "",
            WorkingDirectory = workDir,

            // false时目前控制台合并到当前控制台，一起退出；
            // true时目标控制台独立窗口，不会一起退出；
            UseShellExecute = false,
        };
        si.EnvironmentVariables["BasePath"] = workDir;

        // 环境变量。不能用于ShellExecute
        if (service.Environments.IsNullOrEmpty() && !si.UseShellExecute)
        {
            foreach (var item in service.Environments.SplitAsDictionary("=", ";"))
            {
                if (!item.Key.IsNullOrEmpty())
                    si.EnvironmentVariables[item.Key] = item.Value;
            }
        }

        if (Runtime.Linux)
        {
            // Linux下，需要给予可执行权限
            Process.Start("chmod", $"+x {fileName}").WaitForExit(5_000);
        }

        // 指定用户时，以特定用户启动进程
        var user = service.UserName;
        if (!user.IsNullOrEmpty())
        {
            // 在前台桌面中打开应用
            if (user == "$")
            {
                if (!Runtime.Windows)
                    user = null;
                else
                {
                    var desktop = new Desktop();
                }
            }

            si.UserName = user;
            //si.UseShellExecute = false;

            // 在Linux系统中，改变目录所属用户
            if (Runtime.Linux)
            {
                if (!user.Contains(':')) user = $"{user}:{user}";
                //Process.Start("chown", $"-R {user} {si.WorkingDirectory}");
                Process.Start("chown", $"-R {user} {si.WorkingDirectory.CombinePath("../").GetBasePath()}").WaitForExit(5_000);
            }
        }

        // 如果出现超过一次的重启，则打开调试模式，截取控制台输出到日志
        if (_error > 1)
        {
            // UseShellExecute 必须 false，以便于后续重定向输出流
            si.UseShellExecute = false;
            si.RedirectStandardError = true;
            si.RedirectStandardOutput = true;
        }

        //// 在进程的环境变量中设置BasePath
        //if (!si.UseShellExecute)
        //    si.EnvironmentVariables["BasePath"] = si.WorkingDirectory;

        WriteLog("工作目录: {0}", si.WorkingDirectory);
        WriteLog("启动文件: {0}", si.FileName);
        WriteLog("启动参数: {0}", si.Arguments);
        if (!si.UserName.IsNullOrEmpty())
            WriteLog("启动用户：{0}", si.UserName);

        var p = Process.Start(si);
        if (StartWait > 0 && p != null && p.WaitForExit(StartWait) && p.ExitCode != 0)
        {
            WriteLog("启动失败！ExitCode={0}", p.ExitCode);

            if (si.RedirectStandardError)
            {
                var rs = p.StandardOutput.ReadToEnd();
                if (!rs.IsNullOrEmpty()) WriteLog(rs);

                rs = p.StandardError.ReadToEnd();
                if (!rs.IsNullOrEmpty()) WriteLog(rs);
            }

            return null;
        }

        _fileName ??= file;

        return p;
    }

    #region 日志
    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}
