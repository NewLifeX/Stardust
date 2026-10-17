using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using NewLife;
using StarAgent.Mobile;
using Stardust.Models;
using AndroidProcess = Android.OS.Process;

namespace StarAgent.Droid;

/// <summary>Android平台适配。采集设备信息、电量、存储与网络流量</summary>
/// <param name="context">应用上下文</param>
public class AndroidPlatform(Context context) : IMobilePlatform
{
    #region 静态
    private static readonly Object _trafficLock = new();
    private static Int64 _lastTrafficTime;
    private static Int64 _lastRx;
    private static Int64 _lastTx;
    private static Double _upSpeed;
    private static Double _downSpeed;

    private static readonly Object _cpuLock = new();
    private static Int64 _lastCpuTime;
    private static Int64 _lastCpuTick;
    private static Double _cpuRate;
    #endregion

    #region 属性
    /// <summary>应用上下文</summary>
    public Context Context { get; } = context;

    /// <summary>平台名称</summary>
    public String Name => "Android";

    /// <summary>设备唯一标识</summary>
    public String DeviceId { get; } = GetDeviceId(context);

    /// <summary>设备名称</summary>
    public String DeviceName => Build.Model ?? "Android";

    /// <summary>操作系统名称</summary>
    public String OSName => "Android";

    /// <summary>操作系统版本</summary>
    public String OSVersion => Build.VERSION.Release ?? "";

    /// <summary>处理器架构</summary>
    public String Architecture => GetArch(Build.SupportedAbis);
    #endregion

    #region 方法
    /// <summary>获取应用私有数据目录</summary>
    /// <returns></returns>
    public String GetDataPath() => context.GetExternalFilesDir(null)?.AbsolutePath ?? context.FilesDir?.AbsolutePath ?? Path.GetTempPath();

    /// <summary>填充节点信息</summary>
    /// <param name="info">节点信息</param>
    public void FillNodeInfo(NodeInfo info)
    {
        info.OSName = OSName;
        info.OSVersion = OSVersion;
        info.Architecture = Architecture;
        info.MachineName = DeviceName;
        info.Product = Build.Model;
        info.Vendor = Build.Manufacturer;
        info.Processor = Build.Hardware;

        var mem = GetMemoryInfo();
        if (mem != null)
        {
            info.Memory = (UInt64)mem.TotalMem;
            info.AvailableMemory = (UInt64)mem.AvailMem;
            info.FreeMemory = (UInt64)mem.AvailMem;
        }

        var (total, free) = GetStorage();
        info.TotalSize = total;
        info.AvailableFreeSpace = free;
        info.DriveSize = total;
    }

    /// <summary>填充心跳信息</summary>
    /// <param name="info">心跳信息</param>
    public void FillPingInfo(PingInfo info)
    {
        var mem = GetMemoryInfo();
        if (mem != null)
        {
            info.Memory = (UInt64)mem.TotalMem;
            info.AvailableMemory = (UInt64)mem.AvailMem;
            info.FreeMemory = (UInt64)mem.AvailMem;
        }

        var (total, free) = GetStorage();
        info.TotalSize = total;
        info.AvailableFreeSpace = free;

        info.CpuRate = GetCpuRate();
        info.Battery = GetBatteryPercent();
        info.Signal = GetWifiSignal();
        info.SystemLoad = GetLoad();

        var temperature = GetBatteryTemperature();
        if (temperature > 0) info.Temperature = temperature;

        var (up, down) = GetNetworkSpeed();
        info.UplinkSpeed = up;
        info.DownlinkSpeed = down;
    }

    /// <summary>重启当前应用</summary>
    public void RestartApp()
    {
        try
        {
            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
            if (intent != null)
            {
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                context.StartActivity(intent);
            }
        }
        catch { }

        AndroidProcess.KillProcess(AndroidProcess.MyPid());
    }
    #endregion

    #region 采集
    private ActivityManager.MemoryInfo? GetMemoryInfo()
    {
        try
        {
            var am = context.GetSystemService(Context.ActivityService) as ActivityManager;
            if (am == null) return null;

            var info = new ActivityManager.MemoryInfo();
            am.GetMemoryInfo(info);
            return info;
        }
        catch { return null; }
    }

    private (UInt64 Total, UInt64 Free) GetStorage()
    {
        try
        {
            var dir = context.FilesDir?.AbsolutePath;
            if (dir.IsNullOrEmpty()) return (0, 0);

            var stat = new StatFs(dir);
            return ((UInt64)stat.TotalBytes, (UInt64)stat.AvailableBytes);
        }
        catch { return (0, 0); }
    }

    private Double GetBatteryPercent()
    {
        try
        {
            var bm = context.GetSystemService(Context.BatteryService) as BatteryManager;
            var value = bm?.GetIntProperty((Int32)BatteryProperty.Capacity) ?? 0;
            return value > 0 ? value : 0;
        }
        catch { return 0; }
    }

    private Int32 GetWifiSignal()
    {
        try
        {
            var wm = context.GetSystemService(Context.WifiService) as WifiManager;
            return wm?.ConnectionInfo?.Rssi ?? 0;
        }
        catch { return 0; }
    }

    private Double GetBatteryTemperature()
    {
        try
        {
            using var filter = new IntentFilter(Intent.ActionBatteryChanged);
            using var battery = context.RegisterReceiver(null, filter);
            var temp = battery?.GetIntExtra(BatteryManager.ExtraTemperature, Int32.MinValue) ?? Int32.MinValue;

            return temp == Int32.MinValue ? 0 : temp / 10.0;
        }
        catch { return 0; }
    }

    private static Double GetLoad()
    {
        try
        {
            // Android 部分版本限制 /proc 读取，失败时填0
            var text = File.ReadAllText("/proc/loadavg");
            var first = text.Split([' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            return Double.TryParse(first, out var value) ? value : 0;
        }
        catch { return 0; }
    }

    private static String GetArch(IList<String>? abis)
    {
        var abi = abis?.FirstOrDefault() ?? "";
        return abi switch
        {
            "arm64-v8a" => "Arm64",
            "armeabi-v7a" or "armeabi" => "Arm",
            "x86_64" => "X64",
            "x86" => "X86",
            _ => abi,
        };
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
                var cores = Math.Max(1, System.Environment.ProcessorCount);
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

    private (UInt64 Up, UInt64 Down) GetNetworkSpeed()
    {
        try
        {
            var now = NewLife.Runtime.TickCount64;
            var rx = global::Android.Net.TrafficStats.TotalRxBytes;
            var tx = global::Android.Net.TrafficStats.TotalTxBytes;

            lock (_trafficLock)
            {
                if (_lastTrafficTime > 0 && now > _lastTrafficTime && rx >= 0 && tx >= 0)
                {
                    var seconds = (now - _lastTrafficTime) / 1000.0;
                    var dRx = rx - _lastRx;
                    var dTx = tx - _lastTx;
                    if (dRx >= 0 && dTx >= 0 && seconds > 0)
                    {
                        _downSpeed = Math.Round(dRx / seconds);
                        _upSpeed = Math.Round(dTx / seconds);
                    }
                }

                _lastTrafficTime = now;
                _lastRx = rx;
                _lastTx = tx;

                return ((UInt64)Math.Max(0, _upSpeed), (UInt64)Math.Max(0, _downSpeed));
            }
        }
        catch { return (0, 0); }
    }

    private static String GetDeviceId(Context context)
    {
        try
        {
            var id = global::Android.Provider.Settings.Secure.GetString(context.ContentResolver, global::Android.Provider.Settings.Secure.AndroidId);
            if (!id.IsNullOrEmpty()) return id!;
        }
        catch { }

        // 兜底：本地生成并持久化，保证同一安装内稳定
        try
        {
            var sp = context.GetSharedPreferences("staragent", FileCreationMode.Private);
            var id = sp?.GetString("device_id", null);
            if (id.IsNullOrEmpty())
            {
                id = Guid.NewGuid().ToString("N");
                sp?.Edit()?.PutString("device_id", id)?.Apply();
            }
            return id!;
        }
        catch { }

        return Guid.NewGuid().ToString("N");
    }
    #endregion
}