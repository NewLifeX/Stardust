using System.Runtime.InteropServices;
using Foundation;
using NewLife;
using StarAgent.Mobile;
using Stardust.Models;
using UIKit;

namespace StarAgent.Ios;

/// <summary>iOS平台适配。采集设备信息、电量与存储，受系统限制仅能提供应用级指标</summary>
public class IosPlatform : IMobilePlatform
{
    #region 静态
    private static readonly Object _cpuLock = new();
    private static Int64 _lastCpuTime;
    private static Int64 _lastCpuTick;
    private static Double _cpuRate;
    #endregion

    #region 属性
    /// <summary>平台名称</summary>
    public String Name => "iOS";

    /// <summary>设备唯一标识</summary>
    public String DeviceId { get; }

    /// <summary>设备名称</summary>
    public String DeviceName => UIDevice.CurrentDevice.Model;

    /// <summary>操作系统名称</summary>
    public String OSName => UIDevice.CurrentDevice.SystemName;

    /// <summary>操作系统版本</summary>
    public String OSVersion => UIDevice.CurrentDevice.SystemVersion;

    /// <summary>处理器架构</summary>
    public String Architecture => RuntimeInformation.ProcessArchitecture.ToString().ToLower();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public IosPlatform()
    {
        try { UIDevice.CurrentDevice.BatteryMonitoringEnabled = true; } catch { }

        DeviceId = GetDeviceId();
    }
    #endregion

    #region 方法
    /// <summary>获取应用私有数据目录</summary>
    /// <returns></returns>
    public String GetDataPath()
    {
        var dir = NSSearchPath.GetDirectories(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User).FirstOrDefault();
        if (dir.IsNullOrEmpty()) dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!dir.IsNullOrEmpty() && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        return dir ?? Path.GetTempPath();
    }

    /// <summary>填充节点信息</summary>
    /// <param name="info">节点信息</param>
    public void FillNodeInfo(NodeInfo info)
    {
        info.OSName = OSName;
        info.OSVersion = OSVersion;
        info.Architecture = Architecture;
        info.MachineName = DeviceName;
        info.Product = UIDevice.CurrentDevice.Model;
        info.Vendor = "Apple";
        info.ProcessorCount = Environment.ProcessorCount;
        info.Memory = (UInt64)NSProcessInfo.ProcessInfo.PhysicalMemory;

        var (total, free) = GetStorage();
        info.TotalSize = total;
        info.AvailableFreeSpace = free;
        info.DriveSize = total;
    }

    /// <summary>填充心跳信息</summary>
    /// <param name="info">心跳信息</param>
    public void FillPingInfo(PingInfo info)
    {
        var total = (UInt64)NSProcessInfo.ProcessInfo.PhysicalMemory;
        info.Memory = total;

        // iOS 不开放系统级可用内存，这里用"物理内存 - 当前进程工作集"作为近似值
        try
        {
            var used = (UInt64)System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            info.AvailableMemory = used < total ? total - used : 0;
            info.FreeMemory = info.AvailableMemory;
        }
        catch { }

        var (size, free) = GetStorage();
        info.TotalSize = size;
        info.AvailableFreeSpace = free;

        var level = UIDevice.CurrentDevice.BatteryLevel;
        if (level >= 0) info.Battery = Math.Round(level * 100, 1);

        info.CpuRate = GetCpuRate();
    }

    /// <summary>重启当前应用。iOS 不允许应用拉起自身，退出后由用户再次打开</summary>
    public void RestartApp() => Environment.Exit(0);
    #endregion

    #region 采集
    private static (UInt64 Total, UInt64 Free) GetStorage()
    {
        try
        {
            var di = new DriveInfo("/");
            return ((UInt64)di.TotalSize, (UInt64)di.AvailableFreeSpace);
        }
        catch { return (0, 0); }
    }

    private static Double GetCpuRate()
    {
        lock (_cpuLock)
        {
            var now = NewLife.Runtime.TickCount64;
            var tick = GetProcessCpuTicks();

            if (_lastCpuTick > 0 && now > _lastCpuTime && tick >= _lastCpuTick)
            {
                var elapsed = now - _lastCpuTime;
                var used = tick - _lastCpuTick;
                var cores = Math.Max(1, Environment.ProcessorCount);
                _cpuRate = Math.Round(used * 100.0 / elapsed / cores, 3);
            }

            _lastCpuTime = now;
            _lastCpuTick = tick;

            return _cpuRate;
        }
    }

    private static Int64 GetProcessCpuTicks()
    {
        try { return System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.Ticks / 10_000; }
        catch { return 0; }
    }

    private static String GetDeviceId()
    {
        try
        {
            var defaults = NSUserDefaults.StandardUserDefaults;
            var id = defaults.StringForKey("StarAgentDeviceId");
            if (id.IsNullOrEmpty())
            {
                id = UIDevice.CurrentDevice.IdentifierForVendor?.ToString() ?? Guid.NewGuid().ToString("N");
                defaults.SetString(id, "StarAgentDeviceId");
                defaults.Synchronize();
            }

            return id!;
        }
        catch { }

        return Guid.NewGuid().ToString("N");
    }
    #endregion
}