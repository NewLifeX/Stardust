using System.Diagnostics;
using NewLife;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>托管部署策略</summary>
/// <remarks>
/// 解压到工作目录，由外部宿主托管运行。
/// 适用于IIS托管的Web应用、前端静态站点等场景。
/// 由外部宿主（如IIS、Nginx）负责运行应用。
/// 包含IIS特殊处理：创建app_offline.htm使网站离线，更新完成后删除。
/// </remarks>
public class HostedStrategy : DeployStrategyBase
{
    #region 常量
    /// <summary>IIS 应用离线页面内容</summary>
    private const String AppOfflineHtml = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>应用维护中</title></head><body><h1>应用正在更新，请稍候...</h1></body></html>";

    /// <summary>IIS web.config 文件名</summary>
    private const String WebConfigFileName = "web.config";

    /// <summary>IIS app_offline.htm 文件名</summary>
    private const String AppOfflineFileName = "app_offline.htm";

    /// <summary>备份文件扩展名</summary>
    private const String BackupExtension = ".bak";

    /// <summary>IIS 释放文件锁等待时间（毫秒）</summary>
    private const Int32 IisFileReleaseDelayMs = 1000;
    #endregion

    /// <summary>部署模式</summary>
    public override DeployMode Mode => DeployMode.Hosted;

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

        var fi = RetrieveZip(context);
        if (fi == null) return false;

        var workDir = context.WorkingDirectory;
        context.WriteLog("Hosted mode: extract to the working directory: {0}", workDir);
        context.WriteLog("An external host (such as IIS/Nginx) will manage the application");

        // 检测是否为IIS部署
        var isIisDeploy = false;
        var webConfigPath = workDir.CombinePath(WebConfigFileName);
        var webConfigBackupPath = "";
        var appOfflinePath = "";

        if (Runtime.Windows && File.Exists(webConfigPath))
        {
            isIisDeploy = true;
            context.WriteLog("web.config detected; treating this as an IIS deployment");
        }

        // IIS部署策略：创建app_offline.htm确保网站离线
        if (isIisDeploy)
        {
            appOfflinePath = workDir.CombinePath(AppOfflineFileName);
            try
            {
                context.WriteLog("Creating app_offline.htm to take the site offline");
                File.WriteAllText(appOfflinePath, AppOfflineHtml);

                // 备份并删除web.config，进一步确保文件不被占用
                webConfigBackupPath = webConfigPath + BackupExtension;
                context.WriteLog("Backing up web.config to {0}", webConfigBackupPath);
                File.Copy(webConfigPath, webConfigBackupPath, true);
                File.Delete(webConfigPath);

                // 等待IIS释放文件锁
                Thread.Sleep(IisFileReleaseDelayMs);
            }
            catch (Exception ex)
            {
                context.WriteLog("Failed to create app_offline.htm: {0}", ex.Message);
            }
        }

        try
        {
            // 直接解压到工作目录
            ExtractZip(fi.FullName, workDir, context.Deploy, context.Log);
        }
        finally
        {
            // IIS部署后清理
            if (isIisDeploy)
            {
                try
                {
                    // 恢复web.config
                    if (!webConfigBackupPath.IsNullOrEmpty() && File.Exists(webConfigBackupPath))
                    {
                        // 检查zip包是否包含新的web.config
                        if (File.Exists(webConfigPath))
                        {
                            context.WriteLog("A new web.config was found in the zip package; keeping the new version");
                            File.Delete(webConfigBackupPath);
                        }
                        else
                        {
                            context.WriteLog("Restoring web.config from {0}", webConfigBackupPath);
                            File.Copy(webConfigBackupPath, webConfigPath, true);
                            File.Delete(webConfigBackupPath);
                        }
                    }

                    // 删除app_offline.htm使网站上线
                    if (!appOfflinePath.IsNullOrEmpty() && File.Exists(appOfflinePath))
                    {
                        context.WriteLog("Deleting app_offline.htm to bring the site online");
                        File.Delete(appOfflinePath);
                    }
                }
                catch (Exception ex)
                {
                    context.WriteLog("Failed to clean up IIS deployment files: {0}", ex.Message);
                }
            }
        }

        return true;
    }

    /// <summary>执行应用</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>不运行进程，始终返回null</returns>
    public override Process? Execute(DeployContext context)
    {
        // 托管模式不执行进程
        context.WriteLog("Hosted mode: do not start the process");
        return null;
    }
}
