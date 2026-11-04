using System.Diagnostics;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;
using NewLife.Log;

namespace StarAgent;

/// <summary>升级自愈</summary>
/// <remarks>
/// 旧版本代理升级时会把正在使用的 exe/dll 改名为 *.del 备份，一旦随后拉起新进程失败
/// （例如旧版用 <see cref="System.Diagnostics.Process.ProcessName"/> 拼路径，改名后得到 StarAgent.exe.del.exe），
/// 备份就一直留在部署目录里：既占磁盘，又让每次"检查更新"都报一次 Access denied。
/// <para>新版进程启动时执行一次清理与校正，让设备只要重启一次服务就能收拾完历史残局，无需人工登录机器处理。</para>
/// </remarks>
public static class UpgradeHealer
{
    /// <summary>执行自愈</summary>
    /// <param name="service">当前服务实例，用于校正服务注册；为空时跳过注册校正</param>
    /// <param name="log">日志</param>
    /// <param name="dir">待自愈的部署目录，为空时取当前进程所在目录</param>
    /// <returns>自愈结果摘要，没有需要处理的内容时返回null</returns>
    public static String? Heal(ServiceBase? service, ILog? log, String? dir = null)
    {
        dir ??= AppDomain.CurrentDomain.BaseDirectory;
        dir = dir.TrimEnd(Path.DirectorySeparatorChar);
        if (dir.IsNullOrEmpty() || !Directory.Exists(dir)) return null;

        var msgs = new List<String>();

        // 1，清理升级备份。当前进程已经加载真实文件，.del 不再被占用（占用发生在旧进程里）
        var deleted = 0;
        var failed = 0;
        try
        {
            foreach (var file in Directory.GetFiles(dir, "*.del", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // 仍被旧实例占用时跳过，等下一次启动再清
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: scan backups failed: {0}", ex.Message);
        }
        if (deleted > 0) msgs.Add(L.T("cleaned {0} upgrade backup(s)", deleted));
        if (failed > 0) msgs.Add(L.T("{0} backup(s) are still in use", failed));

        // 2，校正服务注册。半途失败的升级可能把服务入口指向 .del 备份
        try
        {
            var cfg = service?.Host?.QueryConfig(service.ServiceName);
            var path = cfg?.FilePath;
            if (!path.IsNullOrEmpty() && path.Contains(".del"))
            {
                log?.Warn("Upgrade self-heal: service points to backup file {0}; re-registering", path);

                if (service.Host.Install(BuildServiceModel(service))) msgs.Add(L.T("service registration repaired"));
            }
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: repair service registration failed: {0}", ex.Message);
        }

        // 3，清理残留升级包，避免边缘节点小容量存储被反复占用
        try
        {
            var update = dir.CombinePath("Update");
            if (Directory.Exists(update))
            {
                var count = 0;
                foreach (var file in Directory.GetFiles(update, "*.zip"))
                {
                    if (File.GetLastWriteTime(file) > DateTime.Now.AddDays(-1)) continue;

                    try
                    {
                        File.Delete(file);
                        count++;
                    }
                    catch { }
                }
                if (count > 0) msgs.Add(L.T("cleaned {0} stale update package(s)", count));
            }
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: clean update packages failed: {0}", ex.Message);
        }

        return msgs.Count > 0 ? String.Join("; ", msgs) : null;
    }

    /// <summary>按当前进程的实际文件构造服务定义</summary>
    /// <param name="service">当前服务实例</param>
    /// <returns>服务定义</returns>
    /// <remarks>框架依赖部署（dotnet App.dll）下进程路径是 dotnet 主机，需要拼上入口程序集</remarks>
    public static ServiceModel BuildServiceModel(ServiceBase service)
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName + "";
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 1 && !exe.IsNullOrEmpty())
        {
            var fileName = Path.GetFileName(exe);
            var dll = args[0].GetFullPath();
            if (fileName.EqualIgnoreCase("dotnet", "dotnet.exe"))
                exe += " " + dll;
            else if (fileName.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
                exe = dll;

            if (exe.Contains(' ')) exe = $"\"{exe}\"";
        }

        return new ServiceModel
        {
            ServiceName = service.ServiceName,
            DisplayName = service.DisplayName,
            Description = service.Description,
            FileName = exe,
            Arguments = "-s",
        };
    }
}
