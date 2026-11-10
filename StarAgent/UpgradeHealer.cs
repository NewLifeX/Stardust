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
        if (deleted > 0) msgs.Add(L.TLog("cleaned {0} upgrade backup(s)", deleted));
        if (failed > 0) msgs.Add(L.TLog("{0} backup(s) are still in use", failed));

        // 2，校正服务注册。半途失败的升级可能把服务入口指向 .del 备份
        try
        {
            var cfg = service?.Host?.QueryConfig(service.ServiceName);
            var path = cfg?.FilePath;
            if (!path.IsNullOrEmpty() && path.Contains(".del"))
            {
                log?.Warn("Upgrade self-heal: service points to backup file {0}; re-registering", path);

                if (service.Host.Install(BuildServiceModel(service))) msgs.Add(L.TLog("service registration repaired"));
            }
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: repair service registration failed: {0}", ex.Message);
        }

        // 3，修复 SysVinit/systemd 仍指向旧 apphost 的启动入口
        try
        {
            var entry = HealEntry(dir, log);
            if (!entry.IsNullOrEmpty()) msgs.Add(entry);
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: repair startup entry failed: {0}", ex.Message);
        }

        // 4，清理残留升级包，避免边缘节点小容量存储被反复占用
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
                if (count > 0) msgs.Add(L.TLog("cleaned {0} stale update package(s)", count));
            }
        }
        catch (Exception ex)
        {
            log?.Debug("Upgrade self-heal: clean update packages failed: {0}", ex.Message);
        }

        return msgs.Count > 0 ? String.Join("; ", msgs) : null;
    }

    /// <summary>修复 Linux 启动入口</summary>
    /// <param name="dir">部署目录</param>
    /// <param name="log">日志</param>
    /// <returns>修复摘要，无需修复时返回null</returns>
    /// <remarks>
    /// 平台下发的 Linux 升级包只含 <c>StarAgent.dll</c> 与 runtimeconfig，不含 apphost。
    /// 升级后 init 脚本里的 <c>./StarAgent</c> 仍是旧的自包含 apphost，加载新程序集时因缺 deps.json 直接退出，
    /// 表现为升级成功却再也起不来（ok3506-buildroot 实测）。这里把过期的 apphost 换成调用 dotnet 的 shim，
    /// 让 <c>./StarAgent -s</c> 与 <c>dotnet StarAgent.dll -s</c> 两种入口都能用。
    /// </remarks>
    public static String? HealEntry(String dir, ILog? log)
    {
        if (!Runtime.Linux) return null;

        // 只处理框架依赖部署（dotnet StarAgent.dll）；自包含/AOT 部署的 apphost 本身就是入口，不能替换
        var args = Environment.GetCommandLineArgs();
        var entryName = args.Length >= 1 ? Path.GetFileName(args[0]) : null;
        if (entryName.IsNullOrEmpty() || !entryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return null;

        var host = Process.GetCurrentProcess().MainModule?.FileName + "";
        if (host.IsNullOrEmpty() || !File.Exists(host)) return null;

        return HealEntry(dir, entryName, host, log);
    }

    /// <summary>把早于入口程序集的 apphost 替换为调用 dotnet 的 shim</summary>
    /// <param name="dir">部署目录</param>
    /// <param name="entryName">入口程序集文件名，如 StarAgent.dll</param>
    /// <param name="host">dotnet 主机完整路径</param>
    /// <param name="log">日志</param>
    /// <returns>修复摘要，无需修复时返回null</returns>
    public static String? HealEntry(String dir, String entryName, String host, ILog? log)
    {
        if (dir.IsNullOrEmpty() || entryName.IsNullOrEmpty() || host.IsNullOrEmpty()) return null;

        var entry = Path.Combine(dir, entryName);
        var app = Path.Combine(dir, Path.GetFileNameWithoutExtension(entryName));
        if (!File.Exists(entry) || !File.Exists(app)) return null;

        // apphost 比程序集新说明两者来自同一次部署，保持原样
        if (File.GetLastWriteTimeUtc(app) >= File.GetLastWriteTimeUtc(entry)) return null;

        var shim = "#!/bin/sh\n" +
            "# Generated by StarAgent upgrade self-heal.\n" +
            "# Framework-dependent upgrade only replaces " + entryName + ", the old apphost can no longer load it.\n" +
            "# Keep ./" + Path.GetFileName(app) + " working for SysVinit/systemd scripts.\n" +
            "DIR=$(cd \"$(dirname \"$0\")\" && pwd)\n" +
            "exec \"" + host + "\" \"$DIR/" + entryName + "\" \"$@\"\n";

        var tmp = app + ".new";
        File.WriteAllText(tmp, shim);
#if NET7_0_OR_GREATER
        if (!Runtime.Windows)
        {
            // 脚本必须可执行，否则 init 脚本调用时权限不足
            try
            {
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch (Exception ex)
            {
                log?.Debug("Upgrade self-heal: set executable mode failed: {0}", ex.Message);
            }
        }
#endif
        // 覆盖式改名，失败时旧文件仍在，不影响启动
#if NETFRAMEWORK
        File.Delete(app);
        File.Move(tmp, app);
#else
        File.Move(tmp, app, true);
#endif

        log?.Info("Upgrade self-heal: startup entry {0} replaced by dotnet shim {1}", app, host);

        return L.TLog("startup entry {0} repaired", Path.GetFileName(app));
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
