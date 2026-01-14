using System.Diagnostics;
using NewLife;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>任务部署策略</summary>
/// <remarks>
/// 运行一次后完成，不守护进程。
/// 适用于初始化脚本、数据迁移、定时任务等场景。
/// 运行完成后自动禁用，不会重复执行。
/// 支持直接运行命令，无需zip包。
/// </remarks>
public class TaskStrategy : DeployStrategyBase
{
    /// <summary>部署模式</summary>
    public override DeployMode Mode => DeployMode.Task;

    /// <summary>是否需要守护进程</summary>
    public override Boolean NeedGuardian => false;

    /// <summary>是否允许接管已存在的进程</summary>
    public override Boolean AllowTakeOver => false;

    /// <summary>解压部署包</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>是否成功</returns>
    public override Boolean Extract(DeployContext context)
    {
        using var span = Tracer?.NewSpan("Deploy-Extract", new { context.Name, context.ZipFile, context.WorkingDirectory });

        var zipFile = context.ZipFile;
        var workDir = context.WorkingDirectory;

        // 任务模式可能没有zip包，直接运行命令
        if (zipFile.IsNullOrEmpty() || !zipFile.EndsWithIgnoreCase(".zip"))
        {
            var fileName = context.Service.FileName;
            if (fileName.IsNullOrEmpty())
            {
                context.WriteLog("任务模式未指定执行文件");
                return false;
            }

            context.WriteLog("任务模式，直接执行命令");

            // 查找可执行文件
            var args = context.Arguments ?? context.Service.Arguments;
            if (fileName.EndsWithIgnoreCase(".exe", ".dll", ".jar"))
            {
                var file = workDir.CombinePath(fileName).GetFullPath();
                if (!File.Exists(file)) file = fileName.GetFullPath();
                if (File.Exists(file))
                {
                    context.ExecuteFile = file;
                    context.Arguments = args;
                    return true;
                }
            }

            // 直接执行命令
            context.ExecuteFile = fileName;
            context.Arguments = args;
            return true;
        }

        var fi = RetrieveZip(context);
        if (fi == null) return false;

        context.WriteLog("任务模式，解压到工作目录：{0}", workDir);

        // 直接解压到工作目录
        ExtractZip(fi.FullName, workDir, context.Deploy, context.Log);

        // 查找可执行文件
        return RetrieveExeFile(context, workDir);
    }

    /// <summary>执行应用</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>启动的进程</returns>
    public override Process? Execute(DeployContext context)
    {
        using var span = Tracer?.NewSpan("Deploy-Execute", new { context.ExecuteFile, context.Arguments, context.WorkingDirectory });

        if (context.ExecuteFile.IsNullOrEmpty())
        {
            context.WriteLog("可执行文件路径为空");
            return null;
        }

        context.WriteLog("任务模式，运行后不守护");

        var runFile = context.ExecuteFile.AsFile();

        // 如果文件存在，使用标准启动流程
        if (runFile != null && runFile.Exists)
        {
            context.WriteLog("运行文件 {0}", runFile.FullName);
            var si = BuildProcessStartInfo(context, runFile);
            return StartProcess(context, si);
        }

        // 否则直接执行命令
        context.WriteLog("执行命令 {0} {1}", context.ExecuteFile, context.Arguments);

        var psi = new ProcessStartInfo
        {
            FileName = context.ExecuteFile,
            Arguments = context.Arguments ?? "",
            WorkingDirectory = context.WorkingDirectory,
            UseShellExecute = false,
        };

        Process? p = null;
        try
        {
            p = Process.Start(psi);
            if (p != null)
            {
                context.WriteLog("启动成功！PID={0}", p.Id);
            }
        }
        catch (Exception ex)
        {
            context.LastError = ex.Message;
            context.WriteLog("执行命令失败：{0}", ex.Message);
        }

        return p;
    }
}
