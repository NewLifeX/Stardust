#if !NET40
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using Stardust;
using Stardust.Managers;
using Stardust.Models;

namespace StarAgent.WebPanel;

/// <summary>StarAgent Web管理面板 API 控制器</summary>
/// <remarks>
/// 提供 StarAgent 专有 API 接口，包括子服务管理、星尘配置、本机信息等功能。
/// 路由格式：/api/star/{MethodName}，由 HttpServer.ControllerHandler 自动映射。
/// 需要 Bearer Token 鉴权。
/// </remarks>
public class StarApi : IHttpController
{
    #region 属性
    /// <summary>当前Http上下文。由 ControllerHandler 自动注入</summary>
    public IHttpContext? Context { get; set; }

    /// <summary>获取服务管理器</summary>
    private ServiceManager? GetManager()
    {
        var svc = AgentWebPanel.Current?.Service;
        if (svc is IServiceProvider provider)
            return provider.GetService(typeof(ServiceManager)) as ServiceManager;

        return null;
    }

    /// <summary>获取 StarSetting</summary>
    private static StarSetting GetStarSetting() => StarSetting.Current;

    /// <summary>获取 StarAgentSetting</summary>
    private static StarAgentSetting GetAgentSetting() => StarAgentSetting.Current;
    #endregion

    #region 鉴权
    private Boolean CheckAuth()
    {
        var ctx = Context;
        if (ctx == null) return false;

        var auth = ctx.Request.Headers["Authorization"];
        if (auth.IsNullOrEmpty() || !auth.StartsWithIgnoreCase("Bearer ")) return false;

        var token = auth.Substring("Bearer ".Length).Trim();
        if (token.IsNullOrEmpty()) return false;

        return AgentWebPanel.Current?.ValidateToken(token) == true;
    }
    #endregion

    #region 子服务管理
    /// <summary>获取所有子服务列表</summary>
    /// <returns>服务列表，含运行状态</returns>
    public Object Services()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        var list = manager.Services;
        var runningList = manager.RunningServices;

        var services = list?.Select(svc =>
        {
            var running = runningList?.FirstOrDefault(e => e.Name.EqualIgnoreCase(svc.Name));
            return new
            {
                svc.Name,
                svc.FileName,
                svc.Arguments,
                svc.WorkingDirectory,
                svc.Enable,
                mode = svc.Mode.ToString(),
                svc.MaxMemory,
                svc.HealthCheck,
                svc.AllowMultiple,
                svc.AutoStop,
                svc.ReloadOnChange,
                svc.Environments,
                svc.OomScoreAdjust,
                priority = svc.Priority.ToString(),
                svc.UserName,
                Running = running != null,
                ProcessId = running?.ProcessId ?? 0,
                ProcessName = running?.ProcessName ?? "",
                StartTime = running?.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }).ToArray();

        return new
        {
            code = 0,
            data = new
            {
                services,
                total = services?.Length ?? 0,
                running = runningList?.Length ?? 0
            }
        };
    }

    /// <summary>启动子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object StartService(String serviceName)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        if (serviceName.IsNullOrEmpty()) return new { code = 400, message = L.T("Service name cannot be empty.") };

        try
        {
            var result = manager.Start(serviceName);
            return new
            {
                code = result ? 0 : 1,
                message = result ? L.T("Service started successfully.") : L.T("Service failed to start or does not exist."),
                serviceName
            };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while starting service: {0}", ex.Message), serviceName };
        }
    }

    /// <summary>停止子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object StopService(String serviceName)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        if (serviceName.IsNullOrEmpty()) return new { code = 400, message = L.T("Service name cannot be empty.") };

        try
        {
            var result = manager.Stop(serviceName, L.TLog("Stopped from the web panel"));
            return new
            {
                code = result ? 0 : 1,
                message = result ? L.T("Service stopped successfully.") : L.T("Service failed to stop or does not exist."),
                serviceName
            };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while stopping service: {0}", ex.Message), serviceName };
        }
    }

    /// <summary>重启子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object RestartService(String serviceName)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        if (serviceName.IsNullOrEmpty()) return new { code = 400, message = L.T("Service name cannot be empty.") };

        try
        {
            // 停止服务
            var running = manager.RunningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;
            if (running)
            {
                var stopResult = manager.Stop(serviceName, L.TLog("Restarted from the web panel"));
                if (!stopResult) return new { code = 1, message = L.T("Failed to stop service."), serviceName };
                Thread.Sleep(1000);
            }

            // 启动服务
            var startResult = manager.Start(serviceName);
            return new
            {
                code = startResult ? 0 : 1,
                message = startResult ? L.T("Service restarted successfully.") : L.T("Failed to start service."),
                serviceName
            };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while restarting service: {0}", ex.Message), serviceName };
        }
    }

    /// <summary>新增或更新子服务</summary>
    /// <param name="info">服务信息</param>
    /// <returns>操作结果</returns>
    public Object AddService(ServiceInfo info)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        if (info == null || info.Name.IsNullOrEmpty())
            return new { code = 400, message = L.T("Service name cannot be empty.") };

        try
        {
            manager.Add(info);

            // 持久化到配置
            var set = StarAgentSetting.Current;
            set.Services = manager.Services;
            set.Save();

            return new { code = 0, message = L.T("Service [{0}] added or updated.", info.Name), serviceName = info.Name };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while adding service: {0}", ex.Message) };
        }
    }

    /// <summary>删除子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object RemoveService(String serviceName)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var manager = GetManager();
        if (manager == null) return new { code = 500, message = "Service manager not available" };

        if (serviceName.IsNullOrEmpty()) return new { code = 400, message = L.T("Service name cannot be empty.") };

        try
        {
            // 先停止服务
            if (manager.RunningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true)
                manager.Stop(serviceName, L.TLog("Removed from the web panel"));

            manager.Remove(serviceName);

            // 持久化到配置
            var set = StarAgentSetting.Current;
            set.Services = manager.Services;
            set.Save();

            return new { code = 0, message = L.T("Service [{0}] removed.", serviceName), serviceName };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while removing service: {0}", ex.Message), serviceName };
        }
    }
    #endregion

    #region 星尘配置
    /// <summary>获取星尘配置</summary>
    /// <returns>StarSetting + StarAgentSetting 所有配置项</returns>
    public Object GetStarConfig()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        var starSet = GetStarSetting();
        var agentSet = GetAgentSetting();

        var items = new List<Object>();

        // StarSetting 配置项
        items.Add(new { group = L.T("StarServer connection"), items = new Object[]
        {
            new { name = "Server", displayName = L.T("Server address"), value = (Object)(starSet.Server ?? ""), type = "String", description = L.T("Stardust server address, e.g. http://star.newlifex.com:6600") },
            new { name = "AppKey", displayName = L.T("Application ID"), value = (Object)(starSet.AppKey ?? ""), type = "String", description = L.T("Application ID registered with the Stardust registry") },
            new { name = "Secret", displayName = L.T("Application secret"), value = (Object)(starSet.Secret ?? ""), type = "Password", description = L.T("Application secret registered with the Stardust registry") },
        }});

        items.Add(new { group = L.T("StarAgent local settings"), items = new Object[]
        {
            new { name = "LocalPort", displayName = L.T("Local port (UDP)"), value = (Object)agentSet.LocalPort, type = "Int32", description = L.T("Local API port (UDP), default 5500. Shares the port with the web panel: UDP for local RPC, TCP for the web panel") },
            new { name = "WebBind", displayName = L.T("Panel bind address"), value = (Object)(agentSet.WebBind ?? ""), type = "String", description = L.T("Web panel listen address, default 127.0.0.1 for local access only. Set to * for all interfaces or a specific IP. Requires a StarAgent restart") },
            new { name = "Code", displayName = L.T("Node code"), value = (Object)(agentSet.Code ?? ""), type = "String", description = L.T("Unique code of the current node") },
            new { name = "Secret_Agent", displayName = L.T("Node secret"), value = (Object)(agentSet.Secret ?? ""), type = "Password", description = L.T("Secret used for node communication") },
            new { name = "Project", displayName = L.T("Project"), value = (Object)(agentSet.Project ?? ""), type = "String", description = L.T("Project the new node joins by default") },
            new { name = "Channel", displayName = L.T("Update channel"), value = (Object)agentSet.Channel, type = "String", description = L.T("Update channel: Release/Debug") },
            new { name = "Delay", displayName = L.T("Delay (ms)"), value = (Object)agentSet.Delay, type = "Int32", description = L.T("Delay before restarting the process or service, default 3000 ms") },
            new { name = "SyncTime", displayName = L.T("Sync interval (s)"), value = (Object)agentSet.SyncTime, type = "Int32", description = L.T("Periodically sync server time to local, 0 to disable") },
            new { name = "StartupHook", displayName = L.T("Startup hook"), value = (Object)agentSet.StartupHook, type = "Boolean", description = L.T("Inject the Stardust monitoring hook into .NET applications when launching the target process") },
            new { name = "UseAutorun", displayName = L.T("Windows autorun"), value = (Object)agentSet.UseAutorun, type = "Boolean", description = L.T("Autorun requires a signed-in desktop; false by default uses the system service") },
        }});

        return new { code = 0, data = new { groups = items } };
    }

    /// <summary>更新星尘配置</summary>
    /// <param name="updates">配置更新键值对</param>
    /// <returns>操作结果</returns>
    public Object UpdateStarConfig(Dictionary<String, Object> updates)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        if (updates == null || updates.Count == 0)
            return new { code = 400, message = L.T("No configuration items to update.") };

        try
        {
            var starSet = GetStarSetting();
            var agentSet = GetAgentSetting();
            var changedStar = false;
            var changedAgent = false;

            // 安全白名单：只允许更新以下字段
            var starFields = new[] { "Server", "AppKey", "Secret" };
            var agentFields = new[] { "LocalPort", "WebBind", "Code", "Secret", "Project", "Channel", "Delay", "SyncTime", "StartupHook", "UseAutorun" };

            foreach (var kv in updates)
            {
                var name = kv.Key;
                var value = kv.Value;

                if (starFields.Contains(name))
                {
                    switch (name)
                    {
                        case "Server": starSet.Server = value?.ToString() ?? ""; break;
                        case "AppKey": starSet.AppKey = value?.ToString(); break;
                        case "Secret": starSet.Secret = value?.ToString(); break;
                    }
                    changedStar = true;
                }
                else if (agentFields.Contains(name))
                {
                    switch (name)
                    {
                        case "LocalPort": agentSet.LocalPort = Convert.ToInt32(value ?? 5500); break;
                        case "WebBind": agentSet.WebBind = value?.ToString() ?? "127.0.0.1"; break;
                        case "Code": agentSet.Code = value?.ToString() ?? ""; break;
                        case "Secret": agentSet.Secret = value?.ToString(); break;
                        case "Project": agentSet.Project = value?.ToString(); break;
                        case "Channel": agentSet.Channel = value?.ToString() ?? "Release"; break;
                        case "Delay": agentSet.Delay = Convert.ToInt32(value ?? 3000); break;
                        case "SyncTime": agentSet.SyncTime = Convert.ToInt32(value ?? 0); break;
                        case "StartupHook": agentSet.StartupHook = Convert.ToBoolean(value ?? false); break;
                        case "UseAutorun": agentSet.UseAutorun = Convert.ToBoolean(value ?? false); break;
                    }
                    changedAgent = true;
                }
            }

            if (changedStar) starSet.Save();
            if (changedAgent) agentSet.Save();

            var saved = new List<String>();
            if (changedStar) saved.Add("StarSetting");
            if (changedAgent) saved.Add("StarAgentSetting");

            return new { code = 0, message = L.T("Configuration saved to: {0}", saved.Join(", ")) };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while saving configuration: {0}", ex.Message) };
        }
    }
    #endregion

    #region 本机信息
    /// <summary>获取本机详细信息</summary>
    /// <returns>丰富的本机信息</returns>
    public Object Machine()
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        try
        {
            var mi = MachineInfo.GetCurrent();
            mi.Refresh();

            // 系统概览
            var osInfo = new
            {
                os = Environment.OSVersion.ToString(),
                platform = Runtime.Windows ? "Windows" : Runtime.Linux ? "Linux" : Runtime.OSX ? "macOS" : "Other",
                hostName = Environment.MachineName,
                userName = Environment.UserName,
                processorCount = Environment.ProcessorCount,
                tickCount = Runtime.TickCount64 / 1000,
                hostUptime = FormatUptime(Runtime.TickCount64 / 1000),
#if NET45 || NET461
                runtime = Environment.Version + "",
                processArch = IntPtr.Size == 8 ? "X64" : "X86",
                systemArch = IntPtr.Size == 8 ? "X64" : "X86",
#else
                runtime = RuntimeInformation.FrameworkDescription,
                processArch = RuntimeInformation.ProcessArchitecture.ToString(),
                systemArch = RuntimeInformation.OSArchitecture.ToString(),
#endif
            };

            // CPU
            var cpuInfo = new
            {
                cpuName = mi.Processor ?? "",
                cpuCount = Environment.ProcessorCount,
                cpuRate = mi.CpuRate,
                cpuRatePercent = (mi.CpuRate * 100).ToString("F1") + "%",
            };

            // 内存信息
            var totalMemory = (Int64)mi.Memory;
            var availableMemory = (Int64)mi.AvailableMemory;
            var usedMemory = totalMemory - availableMemory;
            var memRate = totalMemory > 0 ? (Double)usedMemory / totalMemory : 0;
            var memoryInfo = new
            {
                totalMemory = FormatBytes(totalMemory),
                usedMemory = FormatBytes(usedMemory),
                availableMemory = FormatBytes(availableMemory),
                memoryRate = memRate,
                memoryRatePercent = (memRate * 100).ToString("F1") + "%",
                totalMB = totalMemory / 1024 / 1024,
                usedMB = usedMemory / 1024 / 1024,
                availableMB = availableMemory / 1024 / 1024,
            };

            // 磁盘信息
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d =>
                {
                    var total = d.TotalSize;
                    var free = d.TotalFreeSpace;
                    var used = total - free;
                    var rate = total > 0 ? (Double)used / total : 0;
                    return new
                    {
                        name = d.Name,
                        label = d.VolumeLabel ?? "",
                        format = d.DriveFormat,
                        totalSize = FormatBytes(total),
                        usedSize = FormatBytes(used),
                        freeSize = FormatBytes(free),
                        usedPercent = (rate * 100).ToString("F1") + "%",
                        totalMB = total / 1024 / 1024,
                        usedMB = used / 1024 / 1024,
                        freeMB = free / 1024 / 1024,
                    };
                }).ToArray();

            // 网络信息
            var _Excludes = new[] { "Loopback", "VMware", "VBox", "Virtual", "Teredo", "Tunnel", "VPN", "VNIC", "IEEE", "Filter", "Npcap", "QoS", "Miniport", "Kernel Debug" };
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                       n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                       n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                       !_Excludes.Any(e => n.Description.Contains(e)))
                .Select(n =>
                {
                    var props = n.GetIPProperties();
                    var ip = props.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        ?.Address.ToString() ?? "";
                    var stats = n.GetIPv4Statistics();
                    return new
                    {
                        name = n.Name,
                        description = n.Description,
                        ip,
                        mac = n.GetPhysicalAddress()?.ToString() ?? "",
                        type = n.NetworkInterfaceType.ToString(),
                        speed = FormatSpeed(n.Speed),
                        operationalStatus = n.OperationalStatus.ToString(),
                        bytesReceived = FormatBytes(stats.BytesReceived),
                        bytesSent = FormatBytes(stats.BytesSent),
                    };
                }).ToArray();

            // 进程 Top
            var processes = Process.GetProcesses()
                .Where(p =>
                {
                    try { return p.TotalProcessorTime.TotalMilliseconds > 0 || p.WorkingSet64 > 0; }
                    catch { return false; }
                })
                .OrderByDescending(p =>
                {
                    try { return p.WorkingSet64; }
                    catch { return 0L; }
                })
                .Take(15)
                .Select(p =>
                {
                    try
                    {
                        return new
                        {
                            name = p.ProcessName,
                            pid = p.Id,
                            memoryMB = (p.WorkingSet64 / 1024 / 1024).ToString("F0"),
                            cpuTime = p.TotalProcessorTime.TotalSeconds.ToString("F1") + "s",
                            threadCount = p.Threads.Count,
                            handleCount = p.HandleCount,
                            startTime = p.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        };
                    }
                    catch { return null; }
                })
                .Where(p => p != null)
                .ToArray();

            return new
            {
                code = 0,
                data = new
                {
                    os = osInfo,
                    cpu = cpuInfo,
                    memory = memoryInfo,
                    drives,
                    nics,
                    processes,
                }
            };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while getting machine information: {0}", ex.Message) };
        }
    }

    /// <summary>获取 Top 进程列表</summary>
    /// <param name="sort">排序方式：memory/cpu</param>
    /// <param name="count">返回数量，默认 10</param>
    /// <returns>进程列表</returns>
    public Object GetProcessList(String sort = "memory", Int32 count = 10)
    {
        if (!CheckAuth()) return new { code = 401, message = "Unauthorized" };

        try
        {
            var query = Process.GetProcesses()
                .Where(p =>
                {
                    try { return p.TotalProcessorTime.TotalMilliseconds > 0 || p.WorkingSet64 > 0; }
                    catch { return false; }
                });

            if (sort.EqualIgnoreCase("cpu"))
                query = query.OrderByDescending(p =>
                {
                    try { return p.TotalProcessorTime.TotalSeconds; }
                    catch { return 0.0; }
                });
            else
                query = query.OrderByDescending(p =>
                {
                    try { return p.WorkingSet64; }
                    catch { return 0L; }
                });

            var processes = query.Take(count).Select(p =>
            {
                try
                {
                    return new
                    {
                        name = p.ProcessName,
                        pid = p.Id,
                        memoryMB = (p.WorkingSet64 / 1024 / 1024).ToString("F0"),
                        cpuSeconds = p.TotalProcessorTime.TotalSeconds.ToString("F1"),
                        threadCount = p.Threads.Count,
                        handleCount = p.HandleCount,
                        startTime = p.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    };
                }
                catch { return null; }
            }).Where(p => p != null).ToArray();

            return new { code = 0, data = new { processes, total = processes.Length } };
        }
        catch (Exception ex)
        {
            return new { code = 1, message = L.T("Error while getting process list: {0}", ex.Message) };
        }
    }
    #endregion

    #region 辅助方法
    private static String FormatBytes(Int64 bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }

    private static String FormatSpeed(Int64 bps)
    {
        if (bps < 1000) return $"{bps} bps";
        if (bps < 1000_000) return $"{bps / 1000.0:F1} Kbps";
        if (bps < 1000_000_000) return $"{bps / 1000_000.0:F1} Mbps";
        return $"{bps / 1000_000_000.0:F2} Gbps";
    }

    private static String FormatUptime(Int64 seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return L.T("{0}d {1}h {2}m", (Int32)ts.TotalDays, ts.Hours, ts.Minutes);
        if (ts.TotalHours >= 1) return L.T("{0}h {1}m", (Int32)ts.TotalHours, ts.Minutes);
        return L.T("{0}m {1}s", ts.Minutes, ts.Seconds);
    }

    #endregion
}
#endif
