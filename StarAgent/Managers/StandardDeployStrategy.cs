using System.Diagnostics;
using NewLife;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>标准部署策略</summary>
/// <remarks>
/// 默认推荐模式。解压到工作目录，运行进程，守护进程。
/// 简单直接，适合大多数应用场景。
/// </remarks>
public class StandardDeployStrategy : DeployStrategyBase
{
    /// <summary>部署模式</summary>
    public override DeployMode Mode => DeployMode.Standard;

    /// <summary>是否需要守护进程</summary>
    public override Boolean NeedGuardian => true;

    /// <summary>解压部署包</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>是否成功</returns>
    public override Boolean Extract(DeployContext context)
    {
        using var span = Tracer?.NewSpan("Deploy-Extract", new { context.Name, context.ZipFile, context.WorkingDirectory });

        var fi = RetrieveZip(context);
        if (fi == null) return false;

        var workDir = context.WorkingDirectory;
        context.WriteLog("标准模式，解压到工作目录：{0}", workDir);

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

        var runFile = context.ExecuteFile.AsFile();
        if (runFile == null || !runFile.Exists)
        {
            context.WriteLog("可执行文件不存在：{0}", context.ExecuteFile);
            return null;
        }

        context.WriteLog("运行文件：{0}", runFile.FullName);

        var si = BuildProcessStartInfo(context, runFile);
        return StartProcess(context, si);
    }
}
