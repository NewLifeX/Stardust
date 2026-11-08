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
/// 需要 ****** 鉴权。
/// <para>
/// 响应统一使用字典封套而非匿名类型：匿名类型由编译器生成、不可标注，裁剪与 NativeAOT 下成员会丢失，
/// 序列化只能得到 <c>{}</c>；字典走 <c>IDictionary</c> 通道，不依赖反射枚举。
/// </para>
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

    /// <summary>创建失败响应</summary>
    private static Dictionary<String, Object?> Fail(Int32 code, String? message) => new()
    {
        ["code"] = code,
        ["message"] = message,
    };

    /// <summary>创建带服务名的操作响应</summary>
    private static Dictionary<String, Object?> Result(Int32 code, String? message, String serviceName) => new()
    {
        ["code"] = code,
        ["message"] = message,
        ["serviceName"] = serviceName,
    };
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
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        var list = manager.Services;
        var runningList = manager.RunningServices;

        var services = list?.Select(svc =>
        {
            var running = runningList?.FirstOrDefault(e => e.Name.EqualIgnoreCase(svc.Name));
            return new Dictionary<String, Object?>
            {
                ["name"] = svc.Name,
                ["fileName"] = svc.FileName,
                ["arguments"] = svc.Arguments,
                ["workingDirectory"] = svc.WorkingDirectory,
                ["enable"] = svc.Enable,
                ["mode"] = svc.Mode.ToString(),
                ["maxMemory"] = svc.MaxMemory,
                ["healthCheck"] = svc.HealthCheck,
                ["allowMultiple"] = svc.AllowMultiple,
                ["autoStop"] = svc.AutoStop,
                ["reloadOnChange"] = svc.ReloadOnChange,
                ["environments"] = svc.Environments,
                ["oomScoreAdjust"] = svc.OomScoreAdjust,
                ["priority"] = svc.Priority.ToString(),
                ["userName"] = svc.UserName,
                ["running"] = running != null,
                ["processId"] = running?.ProcessId ?? 0,
                ["processName"] = running?.ProcessName ?? "",
                ["startTime"] = running?.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }).ToArray();

        return new Dictionary<String, Object?>
        {
            ["code"] = 0,
            ["data"] = new Dictionary<String, Object?>
            {
                ["services"] = services,
                ["total"] = services?.Length ?? 0,
                ["running"] = runningList?.Length ?? 0
            }
        };
    }

    /// <summary>启动子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object StartService(String serviceName)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        if (serviceName.IsNullOrEmpty()) return Fail(400, L.T("Service name cannot be empty."));

        try
        {
            var result = manager.Start(serviceName);
            return Result(result ? 0 : 1, result ? L.T("Service started successfully.") : L.T("Service failed to start or does not exist."), serviceName);
        }
        catch (Exception ex)
        {
            return Result(1, L.T("Error while starting service: {0}", ex.Message), serviceName);
        }
    }

    /// <summary>停止子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object StopService(String serviceName)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        if (serviceName.IsNullOrEmpty()) return Fail(400, L.T("Service name cannot be empty."));

        try
        {
            var result = manager.Stop(serviceName, L.TLog("Stopped from the web panel"));
            return Result(result ? 0 : 1, result ? L.T("Service stopped successfully.") : L.T("Service failed to stop or does not exist."), serviceName);
        }
        catch (Exception ex)
        {
            return Result(1, L.T("Error while stopping service: {0}", ex.Message), serviceName);
        }
    }

    /// <summary>重启子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object RestartService(String serviceName)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        if (serviceName.IsNullOrEmpty()) return Fail(400, L.T("Service name cannot be empty."));

        try
        {
            // 停止服务
            var running = manager.RunningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;
            if (running)
            {
                var stopResult = manager.Stop(serviceName, L.TLog("Restarted from the web panel"));
                if (!stopResult) return Result(1, L.T("Failed to stop service."), serviceName);
                Thread.Sleep(1000);
            }

            // 启动服务
            var startResult = manager.Start(serviceName);
            return Result(startResult ? 0 : 1, startResult ? L.T("Service restarted successfully.") : L.T("Failed to start service."), serviceName);
        }
        catch (Exception ex)
        {
            return Result(1, L.T("Error while restarting service: {0}", ex.Message), serviceName);
        }
    }

    /// <summary>新增或更新子服务</summary>
    /// <param name="info">服务信息</param>
    /// <returns>操作结果</returns>
    public Object AddService(ServiceInfo info)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        if (info == null || info.Name.IsNullOrEmpty())
            return Fail(400, L.T("Service name cannot be empty."));

        try
        {
            manager.Add(info);

            // 持久化到配置
            var set = StarAgentSetting.Current;
            set.Services = manager.Services;
            set.Save();

            return Result(0, L.T("Service [{0}] added or updated.", info.Name), info.Name);
        }
        catch (Exception ex)
        {
            return Fail(1, L.T("Error while adding service: {0}", ex.Message));
        }
    }

    /// <summary>删除子服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>操作结果</returns>
    public Object RemoveService(String serviceName)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var manager = GetManager();
        if (manager == null) return Fail(500, L.T("Service manager not available"));

        if (serviceName.IsNullOrEmpty()) return Fail(400, L.T("Service name cannot be empty."));

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

            return Result(0, L.T("Service [{0}] removed.", serviceName), serviceName);
        }
        catch (Exception ex)
        {
            return Result(1, L.T("Error while removing service: {0}", ex.Message), serviceName);
        }
    }
    #endregion

    #region 星尘配置
    /// <summary>获取星尘配置</summary>
    /// <returns>StarSetting + StarAgentSetting 所有配置项</returns>
    public Object GetStarConfig()
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        var starSet = GetStarSetting();
        var agentSet = GetAgentSetting();

        var items = new List<Object>();

        // StarSetting 配置项
        items.Add(new Dictionary<String, Object?>
        {
            ["group"] = L.T("StarServer connection"),
            ["items"] = new Object[]
            {
                ConfigItem("Server", L.T("Server address"), starSet.Server ?? "", "String", L.T("Stardust server address, e.g. http://star.newlifex.com:6600")),
                ConfigItem("AppKey", L.T("Application ID"), starSet.AppKey ?? "", "String", L.T("Application ID registered with the Stardust registry")),
                // 密钥只写不读：不回显已保存的值，避免面板与浏览器侧泄漏
                ConfigItem("Secret", L.T("Application secret"), "", "Password", L.T("Application secret registered with the Stardust registry")),
            }
        });

        items.Add(new Dictionary<String, Object?>
        {
            ["group"] = L.T("StarAgent local settings"),
            ["items"] = new Object[]
            {
                ConfigItem("LocalPort", L.T("Local port (UDP)"), agentSet.LocalPort, "Int32", L.T("Local API port (UDP), default 5500. Shares the port with the web panel: UDP for local RPC, TCP for the web panel")),
                ConfigItem("WebBind", L.T("Panel bind address"), agentSet.WebBind ?? "", "String", L.T("Web panel listen address, default 127.0.0.1 for local access only. Set to * for all interfaces or a specific IP. Requires a StarAgent restart")),
                ConfigItem("Code", L.T("Node code"), agentSet.Code ?? "", "String", L.T("Unique code of the current node")),
                // 节点密钥同样只写不读
                ConfigItem("Secret_Agent", L.T("Node secret"), "", "Password", L.T("Secret used for node communication")),
                ConfigItem("Project", L.T("Project"), agentSet.Project ?? "", "String", L.T("Project the new node joins by default")),
                ConfigItem("Channel", L.T("Update channel"), agentSet.Channel, "String", L.T("Update channel: Release/Debug")),
                ConfigItem("Delay", L.T("Delay (ms)"), agentSet.Delay, "Int32", L.T("Delay before restarting the process or service, default 3000 ms")),
                ConfigItem("SyncTime", L.T("Sync interval (s)"), agentSet.SyncTime, "Int32", L.T("Periodically sync server time to local, 0 to disable")),
                ConfigItem("StartupHook", L.T("Startup hook"), agentSet.StartupHook, "Boolean", L.T("Inject the Stardust monitoring hook into .NET applications when launching the target process")),
                ConfigItem("UseAutorun", L.T("Windows autorun"), agentSet.UseAutorun, "Boolean", L.T("Autorun requires a signed-in desktop; false by default uses the system service")),
            }
        });

        return new Dictionary<String, Object?>
        {
            ["code"] = 0,
            ["data"] = new Dictionary<String, Object?>
            {
                ["groups"] = items
            }
        };
    }

    /// <summary>创建配置项</summary>
    private static Dictionary<String, Object?> ConfigItem(String name, String displayName, Object? value, String type, String description) => new()
    {
        ["name"] = name,
        ["displayName"] = displayName,
        ["value"] = value,
        ["type"] = type,
        ["description"] = description,
    };

    /// <summary>更新星尘配置</summary>
    /// <param name="updates">配置更新键值对</param>
    /// <returns>操作结果</returns>
    public Object UpdateStarConfig(Dictionary<String, Object> updates)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        if (updates == null || updates.Count == 0)
            return Fail(400, L.T("No configuration items to update."));

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
                        // 密钥只写不读：空值表示保持原值不变
                        case "Secret":
                            {
                                var secret = value?.ToString();
                                if (!secret.IsNullOrEmpty()) starSet.Secret = secret;

                                break;
                            }
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
                        // 密钥只写不读：空值表示保持原值不变
                        case "Secret":
                            {
                                var secret = value?.ToString();
                                if (!secret.IsNullOrEmpty()) agentSet.Secret = secret;

                                break;
                            }
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

            return Fail(0, L.T("Configuration saved to: {0}", saved.Join(", ")));
        }
        catch (Exception ex)
        {
            return Fail(1, L.T("Error while saving configuration: {0}", ex.Message));
        }
    }
    #endregion

    #region 本机信息
    /// <summary>获取本机详细信息</summary>
    /// <returns>丰富的本机信息</returns>
    public Object Machine()
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

        try
        {
            var mi = MachineInfo.GetCurrent();
            mi.Refresh();

            // 系统概览
            var osInfo = new Dictionary<String, Object?>
            {
                ["os"] = Environment.OSVersion.ToString(),
                ["platform"] = Runtime.Windows ? "Windows" : Runtime.Linux ? "Linux" : Runtime.OSX ? "macOS" : "Other",
                ["hostName"] = Environment.MachineName,
                ["userName"] = Environment.UserName,
                ["processorCount"] = Environment.ProcessorCount,
                ["tickCount"] = Runtime.TickCount64 / 1000,
                ["hostUptime"] = FormatUptime(Runtime.TickCount64 / 1000),
#if NET45 || NET461
                ["runtime"] = Environment.Version + "",
                ["processArch"] = IntPtr.Size == 8 ? "X64" : "X86",
                ["systemArch"] = IntPtr.Size == 8 ? "X64" : "X86",
#else
                ["runtime"] = RuntimeInformation.FrameworkDescription,
                ["processArch"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["systemArch"] = RuntimeInformation.OSArchitecture.ToString(),
#endif
            };

            // CPU
            var cpuInfo = new Dictionary<String, Object?>
            {
                ["cpuName"] = mi.Processor ?? "",
                ["cpuCount"] = Environment.ProcessorCount,
                ["cpuRate"] = mi.CpuRate,
                ["cpuRatePercent"] = (mi.CpuRate * 100).ToString("F1") + "%",
            };

            // 内存信息
            var totalMemory = (Int64)mi.Memory;
            var availableMemory = (Int64)mi.AvailableMemory;
            var usedMemory = totalMemory - availableMemory;
            var memRate = totalMemory > 0 ? (Double)usedMemory / totalMemory : 0;
            var memoryInfo = new Dictionary<String, Object?>
            {
                ["totalMemory"] = FormatBytes(totalMemory),
                ["usedMemory"] = FormatBytes(usedMemory),
                ["availableMemory"] = FormatBytes(availableMemory),
                ["memoryRate"] = memRate,
                ["memoryRatePercent"] = (memRate * 100).ToString("F1") + "%",
                ["totalMB"] = totalMemory / 1024 / 1024,
                ["usedMB"] = usedMemory / 1024 / 1024,
                ["availableMB"] = availableMemory / 1024 / 1024,
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
                    return new Dictionary<String, Object?>
                    {
                        ["name"] = d.Name,
                        ["label"] = d.VolumeLabel ?? "",
                        ["format"] = d.DriveFormat,
                        ["totalSize"] = FormatBytes(total),
                        ["usedSize"] = FormatBytes(used),
                        ["freeSize"] = FormatBytes(free),
                        ["usedPercent"] = (rate * 100).ToString("F1") + "%",
                        ["totalMB"] = total / 1024 / 1024,
                        ["usedMB"] = used / 1024 / 1024,
                        ["freeMB"] = free / 1024 / 1024,
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
                    return new Dictionary<String, Object?>
                    {
                        ["name"] = n.Name,
                        ["description"] = n.Description,
                        ["ip"] = ip,
                        ["mac"] = n.GetPhysicalAddress()?.ToString() ?? "",
                        ["type"] = n.NetworkInterfaceType.ToString(),
                        ["speed"] = FormatSpeed(n.Speed),
                        ["operationalStatus"] = n.OperationalStatus.ToString(),
                        ["bytesReceived"] = FormatBytes(stats.BytesReceived),
                        ["bytesSent"] = FormatBytes(stats.BytesSent),
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
                        return ProcessItem(p);
                    }
                    catch { return null; }
                })
                .Where(p => p != null)
                .ToArray();

            return new Dictionary<String, Object?>
            {
                ["code"] = 0,
                ["data"] = new Dictionary<String, Object?>
                {
                    ["os"] = osInfo,
                    ["cpu"] = cpuInfo,
                    ["memory"] = memoryInfo,
                    ["drives"] = drives,
                    ["nics"] = nics,
                    ["processes"] = processes,
                }
            };
        }
        catch (Exception ex)
        {
            return Fail(1, L.T("Error while getting machine information: {0}", ex.Message));
        }
    }

    /// <summary>获取 Top 进程列表</summary>
    /// <param name="sort">排序方式：memory/cpu</param>
    /// <param name="count">返回数量，默认 10</param>
    /// <returns>进程列表</returns>
    public Object GetProcessList(String sort = "memory", Int32 count = 10)
    {
        if (!CheckAuth()) return Fail(401, L.T("Unauthorized"));

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
                    return ProcessItem(p);
                }
                catch { return null; }
            }).Where(p => p != null).ToArray();

            return new Dictionary<String, Object?>
            {
                ["code"] = 0,
                ["data"] = new Dictionary<String, Object?>
                {
                    ["processes"] = processes,
                    ["total"] = processes.Length
                }
            };
        }
        catch (Exception ex)
        {
            return Fail(1, L.T("Error while getting process list: {0}", ex.Message));
        }
    }

    /// <summary>构造进程信息</summary>
    private static Dictionary<String, Object?> ProcessItem(Process p) => new()
    {
        ["name"] = p.ProcessName,
        ["pid"] = p.Id,
        ["memoryMB"] = (p.WorkingSet64 / 1024 / 1024).ToString("F0"),
        ["cpuSeconds"] = p.TotalProcessorTime.TotalSeconds.ToString("F1"),
        ["threadCount"] = p.Threads.Count,
        ["handleCount"] = p.HandleCount,
        ["startTime"] = p.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
    };
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