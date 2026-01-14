using System;
using System.ComponentModel;
using System.Diagnostics;
using NewLife;
using NewLife.Agent.Windows;
using NewLife.Log;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>部署策略基类</summary>
/// <remarks>
/// 封装所有策略共用的解压和启动逻辑。
/// 子类通过重写方法来实现不同的部署行为。
/// </remarks>
public abstract class DeployStrategyBase : IDeployStrategy, ITracerFeature
{
    #region 属性
    /// <summary>部署模式</summary>
    public abstract DeployMode Mode { get; }

    /// <summary>是否需要守护进程</summary>
    public abstract Boolean NeedGuardian { get; }

    /// <summary>是否允许接管已存在的进程</summary>
    public virtual Boolean AllowTakeOver => true;

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 核心方法
    /// <summary>解压部署包</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>是否成功</returns>
    public abstract Boolean Extract(DeployContext context);

    /// <summary>执行应用</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>启动的进程</returns>
    public abstract Process? Execute(DeployContext context);
    #endregion

    #region 辅助方法
    /// <summary>获取Zip文件</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected FileInfo? RetrieveZip(DeployContext context)
    {
        var zipFile = context.ZipFile;
        if (zipFile.IsNullOrEmpty()) return null;

        var fi = zipFile.AsFile();
        if (fi != null && fi.Exists) return fi;

        var workDir = context.WorkingDirectory;
        fi = workDir.CombinePath(zipFile).AsFile();
        if (fi != null && fi.Exists) return fi;

        context.WriteLog("Zip文件不存在：{0}", zipFile);
        return null;
    }

    /// <summary>解压Zip文件到目标目录</summary>
    /// <param name="zipFile">Zip文件路径</param>
    /// <param name="targetDir">目标目录</param>
    /// <param name="deploy">部署信息</param>
    /// <param name="log">日志</param>
    protected void ExtractZip(String zipFile, String targetDir, DeployInfo? deploy, ILog log)
    {
        var fi = zipFile.AsFile();
        if (fi == null || !fi.Exists)
        {
            log?.Info("Zip文件不存在：{0}", zipFile);
            return;
        }

        log?.Info("解压缩 {0} 到 {1}", fi.Name, targetDir);

        targetDir.EnsureDirectory(false);
        fi.Extract(targetDir, true);

        // 根据部署模式处理文件覆盖
        var ovs = deploy?.Overwrite?.Split(';');
        if (ovs != null && ovs.Length > 0)
        {
            log?.Info("覆盖文件列表：{0}", deploy?.Overwrite);
        }
    }

    /// <summary>判断是否为配置文件</summary>
    /// <param name="ext">文件扩展名</param>
    /// <returns>是否为配置文件</returns>
    protected Boolean IsConfig(String ext) => ext.EndsWithIgnoreCase(".json", ".config", ".xml", ".yml", ".ini");

    /// <summary>检索可执行文件，并设置到上下文</summary>
    /// <param name="context"></param>
    /// <param name="workDir"></param>
    /// <returns></returns>
    protected Boolean RetrieveExeFile(DeployContext context, String workDir)
    {
        var args = context.Arguments;
        var runfile = FindExeFile(workDir, context.Name, ref args);
        if (runfile == null)
        {
            context.WriteLog("无法找到可执行文件");
            return false;
        }

        context.ExecuteFile = runfile.FullName;
        context.Arguments = args;

        return true;
    }

    /// <summary>查找可执行文件</summary>
    /// <param name="path">搜索目录</param>
    /// <param name="name">应用名称</param>
    /// <param name="arguments">启动参数（可能被修改）</param>
    /// <returns>可执行文件</returns>
    protected FileInfo? FindExeFile(String path, String name, ref String? arguments)
    {
        var dir = path.AsDirectory();
        if (dir == null || !dir.Exists) return null;

        using var span = Tracer?.NewSpan("Deploy-FindExeFile", new { path, name, arguments });
        var fis = dir.GetFiles();

        var runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (runfile == null && Runtime.Windows)
        {
            // 包名的后缀改为exe
            var exeName = $"{name}.exe";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(exeName));

            // 第一个参数可能就是exe
            if (runfile == null && !arguments.IsNullOrEmpty())
            {
                var p = arguments.IndexOf(' ');
                if (p > 0)
                {
                    var first = arguments[..p];
                    runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(first));
                    if (runfile != null) arguments = arguments[(p + 1)..];
                }
            }

            // 如果当前目录有唯一exe文件，选择它
            if (runfile == null)
            {
                var exes = fis.Where(e => e.Extension.EqualIgnoreCase(".exe")).ToList();
                if (exes.Count == 1) runfile = exes[0];
            }
        }

        // 跟配置文件配套的dll
        if (runfile == null)
        {
            var ext = ".runtimeconfig.json";
            var cfg = fis.FirstOrDefault(e => e.Name.EndsWithIgnoreCase(ext));
            if (cfg != null)
            {
                var dllName = $"{cfg.Name[..^ext.Length]}.dll";
                runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(dllName));
            }
        }

        // 指定名称dll
        if (runfile == null)
        {
            var dllName = $"{name}.dll";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(dllName));
        }

        // 指定名称jar
        if (runfile == null)
        {
            var jarName = $"{name}.jar";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(jarName));
        }

        if (runfile != null) span?.AppendTag($"runfile: {runfile.FullName}");

        return runfile;
    }

    /// <summary>构建进程启动信息</summary>
    /// <param name="context">部署上下文</param>
    /// <param name="runFile">可执行文件</param>
    /// <returns>进程启动信息</returns>
    protected ProcessStartInfo BuildProcessStartInfo(DeployContext context, FileInfo runFile)
    {
        var service = context.Service;
        var workDir = context.WorkingDirectory;
        var arguments = context.Arguments ?? "";

        var si = new ProcessStartInfo
        {
            FileName = runFile.FullName,
            Arguments = arguments,
            WorkingDirectory = workDir,

            // false时目前控制台合并到当前控制台，一起退出；
            // true时目标控制台独立窗口，不会一起退出；
            UseShellExecute = false,
        };
        si.EnvironmentVariables["BasePath"] = workDir;

        // 调试模式
        if (context.Debug)
        {
            si.UseShellExecute = false;
            si.RedirectStandardError = true;
            si.RedirectStandardOutput = true;
        }

        // 注入星尘监控
        if (context.StartupHook) SetStartupHook(context, runFile, service, si);

        // 设置应用标识。目标应用将使用该标识连接星尘服务端，实现一份应用程序以多个应用身份运行，比如魔方以cube/cube2/cube3等身份运行
        if (!context.AppId.IsNullOrEmpty())
            si.EnvironmentVariables["StarAppId"] = context.AppId;

        // 处理dll和jar文件
        if (runFile.Extension.EqualIgnoreCase(".dll"))
        {
            si.FileName = "dotnet";
            si.Arguments = $"{runFile.FullName} {arguments}";
        }
        else if (runFile.Extension.EqualIgnoreCase(".jar"))
        {
            si.FileName = "java";
            si.Arguments = $"-jar {runFile.FullName} {arguments}";
        }
        else if (Runtime.Linux)
        {
            // Linux下，需要给予可执行权限
            Process.Start("chmod", $"+x {runFile.FullName}")?.WaitForExit(5_000);
        }

        // 环境变量。不能用于ShellExecute
        if (!service.Environments.IsNullOrEmpty() && !si.UseShellExecute)
        {
            foreach (var item in service.Environments.SplitAsDictionary("=", ";"))
            {
                if (!item.Key.IsNullOrEmpty())
                    si.EnvironmentVariables[item.Key] = item.Value;
            }
        }

        return si;
    }

    /// <summary>设置启动钩子。向目标进程注入星尘监控</summary>
    private static void SetStartupHook(DeployContext context, FileInfo runFile, ServiceInfo service, ProcessStartInfo si)
    {
        if (!service.UserName.IsNullOrEmpty() && service.UserName != Environment.UserName && !Runtime.Windows)
            return;

        var dir = runFile.Directory!.FullName;
        var targets = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
        if (targets.Any(e => e.EndsWithIgnoreCase(".runtimeconfig.json")) &&
            !targets.Any(e => e.EqualIgnoreCase("Stardust.dll")))
        {
            var dll = "Stardust.dll".GetFullPath();
            context.WriteLog("执行目录：{0}，注入：{1}", dir, dll);
            si.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = dll;
        }
    }

    /// <summary>启动进程</summary>
    /// <param name="context">部署上下文</param>
    /// <param name="si">进程启动信息</param>
    /// <returns>启动的进程</returns>
    protected Process? StartProcess(DeployContext context, ProcessStartInfo si)
    {
        var service = context.Service;
        var user = service.UserName;

        context.WriteLog("工作目录: {0}", si.WorkingDirectory);
        context.WriteLog("启动文件: {0}", si.FileName);
        context.WriteLog("启动参数: {0}", si.Arguments);
        if (!user.IsNullOrEmpty())
            context.WriteLog("启动用户：{0}", user);

        Process? p = null;

        // Windows桌面用户运行
        if (Runtime.Windows && (user == "$" || user == "$$"))
        {
            // 交互模式直接运行
            if (Environment.UserInteractive)
            {
                si.UserName = null!;
                p = Process.Start(si);
            }
            else
            {
                // 桌面用户运行。$表示在用户桌面上启动进程，$$表示以用户身份启动进程（无需登录桌面）
                var desktop = new Desktop { Log = context.Log };
                var pid = 0u;
                if (user == "$")
                    pid = desktop.StartProcess(si.FileName, si.Arguments, si.WorkingDirectory, false, true);
                else
                    pid = desktop.StartProcessAsUser(si.FileName, si.Arguments, si.WorkingDirectory, null);
                p = Process.GetProcessById((Int32)pid);
            }
        }
        else if (Runtime.Windows && !user.IsNullOrEmpty())
        {
            context.WriteLog("在Windows下以特定用户[{0}]启动进程，大概率失败，因没有密码令牌", user);
            p = Process.Start(si);
        }
        else
        {
            // 指定用户时，以特定用户启动进程
            if (!user.IsNullOrEmpty())
            {
                si.UserName = user;

                // 在Linux系统中，改变目录所属用户
                if (Runtime.Linux)
                {
                    if (!user.Contains(':')) user = $"{user}:{user}";
                    Process.Start("chown", $"-R {user} {si.WorkingDirectory.CombinePath("../").GetBasePath()}")?.WaitForExit(5_000);
                }
            }

            try
            {
                p = Process.Start(si);
            }
            catch (Win32Exception ex)
            {
                context.LastError = ex.Message;
                context.WriteLog("启动失败！Win32Exception={0} File={1}", ex.NativeErrorCode, si.FileName);
                context.WriteLog(ex.ToString());
                return null;
            }
        }

        if (p == null) return null;

        // 进程优先级
        if (service.Priority != ProcessPriority.Normal)
        {
            context.WriteLog("优先级：{0}", service.Priority);
            p.PriorityClass = service.Priority switch
            {
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal,
            };
        }

        // 等待启动
        if (context.StartWait > 0 && p.WaitForExit(context.StartWait) && p.ExitCode != 0)
        {
            context.WriteLog("启动失败！PID={0} ExitCode={1}", p.Id, p.ExitCode);

            if (si.RedirectStandardError)
            {
                var rs = p.StandardOutput.ReadToEnd();
                if (!rs.IsNullOrEmpty()) context.WriteLog(rs);

                rs = p.StandardError.ReadToEnd();
                context.LastError = rs;
                if (!rs.IsNullOrEmpty()) context.WriteLog(rs);
            }

            return null;
        }

        context.WriteLog("启动成功！PID={0}/{1}", p.Id, p.ProcessName);
        return p;
    }
    #endregion
}
