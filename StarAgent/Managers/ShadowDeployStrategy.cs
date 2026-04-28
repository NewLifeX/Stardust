using System.Diagnostics;
using NewLife;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>影子部署策略</summary>
/// <remarks>
/// 解压到影子目录，运行进程，守护进程。
/// 工作目录保持干净，仅存放配置和数据文件。
/// 可执行文件在影子目录中，支持热更新时不影响运行中的进程。
/// </remarks>
public class ShadowDeployStrategy : DeployStrategyBase
{
    /// <summary>部署模式</summary>
    public override DeployMode Mode => DeployMode.Shadow;

    /// <summary>是否需要守护进程</summary>
    public override Boolean NeedGuardian => true;

    /// <summary>解压部署包</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>是否成功</returns>
    public override Boolean Extract(DeployContext context)
    {
        using var span = Tracer?.NewSpan("Deploy-Extract", new { context.Name, context.ZipFile, context.WorkingDirectory });

        var workDir = context.WorkingDirectory;
        var fi = RetrieveZip(context);
        if (fi != null)
        {
            // 计算哈希，构建影子目录
            var hash = fi.MD5().ToHex()[..8].ToLower();
            var shadow = CreateShadow(workDir, $"{context.Name}-{hash}");

            context.Shadow = shadow;
            context.WriteLog("影子模式，解压到影子目录：{0}", shadow);

            var sdi = shadow.AsDirectory();
            if (sdi == null || !sdi.Exists)
            {
                // 删除其它版本的影子目录
                CleanOldShadows(workDir, context.Name, context.Log);

                // 解压到影子目录
                ExtractZip(fi.FullName, shadow, context.Deploy, context.Log);

                // 拷贝配置文件到工作目录（如果不存在）
                CopyConfigToWorkDir(shadow, workDir, context.Log);
            }

            // 查找可执行文件（在影子目录中）
            if (!RetrieveExeFile(context, shadow))
            {
                DeleteShadow(shadow, context.Log);
                return false;
            }
        }
        else
        {
            // 没有 zip 包时，假设文件已经存在于工作目录中
            context.WriteLog("影子模式降级为标准模式，无需解压，直接使用工作目录：{0}", workDir);

            // 查找可执行文件（在工作目录中）
            if (!RetrieveExeFile(context, workDir))
                return false;
        }

        return true;
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

        // 影子目录需要设置权限
        var shadow = context.Shadow;
        if (!shadow.IsNullOrEmpty() && Runtime.Linux)
        {
            var user = context.Service.UserName;
            if (!user.IsNullOrEmpty())
            {
                if (!user.Contains(':')) user = $"{user}:{user}";
                Process.Start("chown", $"-R {user} {shadow}")?.WaitForExit(5_000);
            }
        }

        return StartProcess(context, si);
    }

    #region 辅助方法
    /// <summary>创建影子目录</summary>
    /// <param name="workDir">工作目录</param>
    /// <param name="name">应用目录名，格式为{app}-{hash}</param>
    /// <returns>影子目录路径</returns>
    private String CreateShadow(String workDir, String name)
    {
        var shadow = "";

        // 影子目录默认使用上一级的shadow目录，无权时使用临时目录
        try
        {
            shadow = workDir.CombinePath("../shadow").GetFullPath();
            shadow.EnsureDirectory(false);
        }
        catch
        {
            shadow = Path.GetTempPath();
        }

        return shadow.CombinePath(name);
    }

    /// <summary>清理旧的影子目录</summary>
    private void CleanOldShadows(String workDir, String name, NewLife.Log.ILog log)
    {
        var shadowBase = workDir.CombinePath("../shadow").GetFullPath();
        var sdi = shadowBase.AsDirectory();
        if (sdi == null || !sdi.Exists) return;

        foreach (var di in sdi.GetDirectories($"{name}-*"))
        {
            log?.Info("删除旧版影子目录 {0}", di.FullName);
            try
            {
                di.Delete(true);
            }
            catch (Exception ex)
            {
                log?.Info(ex.Message);
            }
        }
    }

    /// <summary>拷贝配置文件到工作目录</summary>
    private void CopyConfigToWorkDir(String shadow, String workDir, NewLife.Log.ILog log)
    {
        var sdi = shadow.AsDirectory();
        if (sdi == null || !sdi.Exists) return;

        foreach (var fi in sdi.GetFiles())
        {
            if (IsConfig(fi.Extension))
            {
                var dst = workDir.CombinePath(fi.Name);
                if (!File.Exists(dst))
                {
                    log?.Info("拷贝配置文件 {0}", fi.Name);
                    fi.CopyTo(dst, false);
                }
            }
        }
    }

    /// <summary>删除影子目录</summary>
    private void DeleteShadow(String shadow, NewLife.Log.ILog log)
    {
        try
        {
            log?.Info("删除影子目录：{0}", shadow);
            Directory.Delete(shadow, true);
        }
        catch (Exception ex)
        {
            log?.Error(ex.ToString());
        }
    }
    #endregion
}
