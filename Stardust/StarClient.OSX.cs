using System.Diagnostics;
using System.Net.NetworkInformation;
using NewLife;
using Stardust.Models;

namespace Stardust;

/// <summary>星星客户端 - macOS平台信息采集</summary>
public partial class StarClient
{
    #region 采样缓存
    private static readonly Object _osxCpuLock = new();
    private static DateTime _osxCpuTime;
    private static Double _osxCpuRate;

    private static readonly Object _osxWifiLock = new();
    private static DateTime _osxWifiTime;
    private static String? _osxSsid;
    private static Int32 _osxSignal;
    #endregion

    /// <summary>填充macOS专属信息</summary>
    /// <param name="di">节点信息</param>
    public static void FillOnOSX(NodeInfo di)
    {
        try
        {
            di.MaxOpenFiles = ExecuteOSX("sh", "-c \"ulimit -n\"")?.Trim().ToInt() ?? 0;
        }
        catch { }

        try
        {
            // macOS 的C运行时是 libSystem，内核版本由 uname -r 给出
            var kernel = ExecuteOSX("uname", "-r")?.Trim();
            if (!kernel.IsNullOrEmpty()) di.CLibVersion = $"libSystem(Darwin {kernel})";
        }
        catch { }

        try
        {
            di.GPU = GetGpuInfoOSX();
        }
        catch { }

        try
        {
            var total = GetOSXTotalMemory();
            if (total > 0) di.Memory = total;

            var (available, free) = GetOSXMemory();
            if (available > 0) di.AvailableMemory = available;
            if (free > 0) di.FreeMemory = free;
        }
        catch { }
    }

    /// <summary>填充macOS心跳专属信息</summary>
    /// <param name="request">心跳请求</param>
    public static void FillPingOnOSX(PingInfo request)
    {
        try
        {
            request.SystemLoad = GetLoadOSX();
        }
        catch { }

        try
        {
            var (iops, activeTime) = GetDiskStatsOSX();
            request.DiskIOPS = iops;
            request.DiskActiveTime = activeTime;
        }
        catch { }

        try
        {
            request.CpuRate = Math.Round(GetCpuRateOSX(), 3);
        }
        catch { }

        try
        {
            var total = GetOSXTotalMemory();
            var (available, free) = GetOSXMemory();
            if (total > 0) request.Memory = total;
            if (available > 0) request.AvailableMemory = available;
            if (free > 0) request.FreeMemory = free;
        }
        catch { }

        try
        {
            var battery = GetBatteryPercentOSX();
            if (battery >= 0) request.Battery = battery;
        }
        catch { }

        try
        {
            var (_, signal) = GetWifiOSX();
            if (signal != 0) request.Signal = signal;
        }
        catch { }
    }

    #region 指标采集
    /// <summary>获取系统负载。sysctl vm.loadavg 输出 { 1.23 2.34 3.45 }</summary>
    /// <returns>1分钟平均负载</returns>
    internal static Double GetLoadOSX() => ParseLoad(ExecuteOSX("sysctl", "-n vm.loadavg"));

    /// <summary>解析 sysctl vm.loadavg 输出</summary>
    /// <param name="output">vm.loadavg 输出</param>
    /// <returns>1分钟平均负载</returns>
    internal static Double ParseLoad(String? output)
    {
        if (output.IsNullOrEmpty()) return 0;

        var ss = output.Split([' ', '{', '}', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return ss.Length > 0 ? ss[0].ToDouble() : 0;
    }

    /// <summary>获取CPU占用率。top单次采样约1秒，做5秒缓存避免重复调用</summary>
    /// <returns>CPU占用率百分比</returns>
    internal static Double GetCpuRateOSX()
    {
        lock (_osxCpuLock)
        {
            var now = DateTime.Now;
            if (_osxCpuTime > DateTime.MinValue && (now - _osxCpuTime).TotalSeconds < 5) return _osxCpuRate;

            var output = ExecuteOSX("top", "-l 1 -n 0", 8_000);
            var idle = ParseTopIdle(output);
            if (idle >= 0) _osxCpuRate = Math.Max(0, 100 - idle);
            _osxCpuTime = now;

            return _osxCpuRate;
        }
    }

    /// <summary>解析 top 输出中的 idle 百分比</summary>
    /// <param name="output">top -l 1 输出</param>
    /// <returns>idle百分比，解析失败返回-1</returns>
    internal static Double ParseTopIdle(String? output)
    {
        if (output.IsNullOrEmpty()) return -1;

        // CPU usage: 3.12% user, 6.25% sys, 90.62% idle
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.IndexOf("CPU usage:", StringComparison.OrdinalIgnoreCase);
            if (p < 0) continue;

            foreach (var item in line[p..].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!item.Contains("idle", StringComparison.OrdinalIgnoreCase)) continue;

                var idx = item.IndexOf('%');
                if (idx <= 0) continue;

                var txt = item[..idx].Trim();
                var n = txt.LastIndexOf(' ');
                if (n >= 0) txt = txt[(n + 1)..];

                return txt.ToDouble();
            }
        }

        return -1;
    }

    /// <summary>获取物理内存大小</summary>
    /// <returns>字节数</returns>
    internal static UInt64 GetOSXTotalMemory()
    {
        var str = ExecuteOSX("sysctl", "-n hw.memsize");
        return str.IsNullOrEmpty() ? 0 : (UInt64)Math.Max(0, str.Trim().ToLong());
    }

    /// <summary>获取可用内存与空闲内存</summary>
    /// <returns>可用内存与空闲内存</returns>
    internal static (UInt64 Available, UInt64 Free) GetOSXMemory() => ParseVmStat(ExecuteOSX("vm_stat"));

    /// <summary>解析 vm_stat 输出。页数乘以页大小得到字节数</summary>
    /// <param name="output">vm_stat 输出</param>
    /// <returns>可用内存与空闲内存</returns>
    internal static (UInt64 Available, UInt64 Free) ParseVmStat(String? output)
    {
        if (output.IsNullOrEmpty()) return (0, 0);

        var pageSize = 4096L;
        var free = 0L;
        var inactive = 0L;
        var speculative = 0L;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            // 首行包含页大小：(page size of 16384 bytes)
            if (line.StartsWith("Mach Virtual Memory Statistics", StringComparison.OrdinalIgnoreCase))
            {
                var p = line.IndexOf("page size of", StringComparison.OrdinalIgnoreCase);
                if (p >= 0)
                {
                    var ss = line[(p + "page size of".Length)..].Split([' ', ')'], StringSplitOptions.RemoveEmptyEntries);
                    if (ss.Length > 0 && ss[0].ToLong() > 0) pageSize = ss[0].ToLong();
                }
                continue;
            }

            var idx = line.IndexOf(':');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim().TrimEnd('.').Trim().ToLong();

            if (key.EqualIgnoreCase("Pages free"))
                free = value;
            else if (key.EqualIgnoreCase("Pages inactive"))
                inactive = value;
            else if (key.EqualIgnoreCase("Pages speculative"))
                speculative = value;
        }

        var freeBytes = (UInt64)Math.Max(0, free * pageSize);
        var availableBytes = (UInt64)Math.Max(0, (free + inactive + speculative) * pageSize);
        return (availableBytes, freeBytes);
    }

    /// <summary>获取电池电量百分比</summary>
    /// <returns>电量百分比，无电池时返回-1</returns>
    internal static Double GetBatteryPercentOSX() => ParseBatteryPercent(ExecuteOSX("pmset", "-g batt"));

    /// <summary>解析 pmset -g batt 输出</summary>
    /// <param name="output">pmset 输出</param>
    /// <returns>电量百分比，无电池时返回-1</returns>
    internal static Double ParseBatteryPercent(String? output)
    {
        if (output.IsNullOrEmpty()) return -1;

        // 台式机没有 InternalBattery 条目
        if (!output.Contains("InternalBattery", StringComparison.OrdinalIgnoreCase)) return -1;

        var idx = output.IndexOf('%');
        if (idx <= 0) return -1;

        var start = idx - 1;
        while (start > 0 && Char.IsDigit(output[start - 1])) start--;

        return output[start..idx].ToDouble();
    }

    /// <summary>获取WiFi名称与信号强度。system_profiler较慢，做5分钟缓存</summary>
    /// <returns>SSID与信号dBm</returns>
    internal static (String? Ssid, Int32 Signal) GetWifiOSX()
    {
        lock (_osxWifiLock)
        {
            var now = DateTime.Now;
            if (_osxWifiTime > DateTime.MinValue && (now - _osxWifiTime).TotalMinutes < 5) return (_osxSsid, _osxSignal);

            String? ssid = null;
            var signal = 0;

            try
            {
                var wifi = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(e => e.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && e.OperationalStatus == OperationalStatus.Up);
                if (wifi != null)
                {
                    var str = ExecuteOSX("networksetup", $"-getairportnetwork {wifi.Name}", 3_000);
                    ssid = ParseAirportNetwork(str);

                    signal = ParseWifiSignal(ExecuteOSX("system_profiler", "SPAirPortDataType", 8_000));
                }
            }
            catch { }

            _osxSsid = ssid;
            _osxSignal = signal;
            _osxWifiTime = now;

            return (ssid, signal);
        }
    }

    /// <summary>解析 networksetup 输出的WiFi名称</summary>
    /// <param name="output">networksetup -getairportnetwork 输出</param>
    /// <returns>SSID</returns>
    internal static String? ParseAirportNetwork(String? output)
    {
        if (output.IsNullOrEmpty()) return null;

        var p = output.IndexOf(':');
        return p > 0 ? output[(p + 1)..].Trim() : null;
    }

    /// <summary>解析 system_profiler 输出中的信号强度</summary>
    /// <param name="output">system_profiler SPAirPortDataType 输出</param>
    /// <returns>信号dBm，未找到返回0</returns>
    internal static Int32 ParseWifiSignal(String? output)
    {
        if (output.IsNullOrEmpty()) return 0;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf("Signal / Noise:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            // Signal / Noise: -55 dBm / -90 dBm
            var ss = line[(idx + "Signal / Noise:".Length)..].Split([' '], StringSplitOptions.RemoveEmptyEntries);
            return ss.Length > 0 ? ss[0].ToInt() : 0;
        }

        return 0;
    }

    /// <summary>获取磁盘IOPS。macOS iostat 不提供活动时间，活动时间固定为0</summary>
    /// <returns>IOPS与活动时间百分比</returns>
    internal static (Int32 IOPS, Double ActiveTime) GetDiskStatsOSX()
    {
        var output = ExecuteOSX("iostat", "-d -c 2 -w 1", 5_000);
        return (ParseIostatTps(output), 0);
    }

    /// <summary>解析 iostat 输出的最后一次采样，返回各磁盘 tps 之和</summary>
    /// <param name="output">iostat -d -c 2 输出</param>
    /// <returns>每秒IO操作次数</returns>
    internal static Int32 ParseIostatTps(String? output)
    {
        if (output.IsNullOrEmpty()) return 0;

        // 每3列为一组：KB/t tps MB/s
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var ss = lines[i].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (ss.Length < 3) continue;

            var total = 0;
            var count = 0;
            for (var k = 1; k < ss.Length; k += 3)
            {
                if (Int32.TryParse(ss[k], out var tps))
                {
                    total += tps;
                    count++;
                }
            }
            if (count > 0) return total;
        }

        return 0;
    }

    /// <summary>获取GPU信息</summary>
    /// <returns>GPU名称，多个逗号分隔</returns>
    internal static String? GetGpuInfoOSX() => ParseGpuInfo(ExecuteOSX("system_profiler", "SPDisplaysDataType", 8_000));

    /// <summary>解析 system_profiler 显卡输出中的 Chipset Model</summary>
    /// <param name="output">system_profiler SPDisplaysDataType 输出</param>
    /// <returns>GPU名称，多个逗号分隔</returns>
    internal static String? ParseGpuInfo(String? output)
    {
        if (output.IsNullOrEmpty()) return null;

        var list = new List<String>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var str = line.Trim();
            if (!str.StartsWith("Chipset Model:", StringComparison.OrdinalIgnoreCase)) continue;

            var value = str["Chipset Model:".Length..].Trim();
            if (!value.IsNullOrEmpty() && !list.Contains(value)) list.Add(value);
        }

        return list.Count > 0 ? list.Join(",") : null;
    }
    #endregion

    #region 辅助
    private static String? ExecuteOSX(String cmd, String? arguments = null, Int32 msWait = 3_000)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, arguments ?? String.Empty)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi);
            if (process == null) return null;

            // 先异步读取 stdout/stderr，避免输出较多时管道缓冲区写满导致进程阻塞
            var stdout = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(msWait))
            {
                try { process.Kill(); } catch { }
                return null;
            }

            return stdout.GetAwaiter().GetResult();
        }
        catch { return null; }
    }
    #endregion
}