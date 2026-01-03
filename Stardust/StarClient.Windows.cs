using System.Runtime.InteropServices;
using NewLife;
using Stardust.Models;

namespace Stardust;

/// <summary>星星客户端 - Windows平台信息采集</summary>
public partial class StarClient
{
    /// <summary>填充Windows专属信息</summary>
    /// <param name="di">节点信息</param>
    public static void FillOnWindows(NodeInfo di)
    {
        try
        {
            // DPI：优先使用 Win32 API（Win10+），避免编译期依赖 WindowsDesktop/WinForms
            var dpi = 0u;
            try
            {
                // GetDpiForWindow 在旧系统可能入口不存在，会抛出 EntryPointNotFoundException
                var hwnd = NativeMethods.GetDesktopWindow();
                dpi = NativeMethods.GetDpiForWindow(hwnd);
            }
            catch { }

            if (dpi <= 0)
            {
                try
                {
                    dpi = NativeMethods.GetDpiForSystem();
                }
                catch { }
            }

            if (dpi > 0) di.Dpi = $"{dpi}*{dpi}";

            // 分辨率：默认用 SystemMetrics（主屏），不需要 WinForms
            var w = NativeMethods.GetSystemMetrics(0);
            var h = NativeMethods.GetSystemMetrics(1);
            if (w > 0 && h > 0) di.Resolution = $"{w}*{h}";

            // 若运行环境存在 WinForms/Graphics（例如 .NET Framework / WindowsDesktop），仅在缺失时用反射补全
            if (di.Dpi.IsNullOrEmpty() || di.Resolution.IsNullOrEmpty()) TryFillByReflection(di);

            // 兜底：GDI+ 获取 DPI（某些环境 Win32/反射 获取失败时）
            if (di.Dpi.IsNullOrEmpty()) TryFillByGdiPlus(di);

            // GPU信息
            di.GPU = GetGpuInfo();

            // VC++运行时版本
            di.CLibVersion = GetVCRuntimeVersions();
        }
        catch { }
    }

    /// <summary>填充Windows心跳专属信息</summary>
    /// <param name="request">心跳请求</param>
    public static void FillPingOnWindows(PingInfo request)
    {
        try
        {
            // 获取处理器队列长度作为类似Load的指标
            request.SystemLoad = GetProcessorQueueLength();

            // 获取磁盘IOPS
            request.DiskIOPS = GetDiskIOPS();
        }
        catch { }
    }

    private static Double GetProcessorQueueLength()
    {
        try
        {
            // 通过 wmic 获取处理器队列长度
            var rs = Execute("wmic", "path Win32_PerfFormattedData_PerfOS_System get ProcessorQueueLength /value");
            if (!rs.IsNullOrEmpty())
            {
                var lines = rs.Split(new[] { '\r', '\n', '=' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var value = line.Trim().ToDouble();
                    if (value >= 0) return value;
                }
            }
        }
        catch { }
        return 0;
    }

    private static Int32 GetDiskIOPS()
    {
        try
        {
            // 通过 wmic 获取磁盘读写次数
            var rs = Execute("wmic", "path Win32_PerfFormattedData_PerfDisk_PhysicalDisk where Name='_Total' get DiskReadsPersec,DiskWritesPersec /value");
            if (!rs.IsNullOrEmpty())
            {
                var reads = 0;
                var writes = 0;

                var lines = rs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("DiskReadsPersec="))
                        reads = line.Substring("DiskReadsPersec=".Length).Trim().ToInt();
                    else if (line.StartsWith("DiskWritesPersec="))
                        writes = line.Substring("DiskWritesPersec=".Length).Trim().ToInt();
                }

                return reads + writes;
            }
        }
        catch { }
        return 0;
    }

    private static String? GetGpuInfo()
    {
        try
        {
            // 通过WMI获取GPU信息
            var gpuList = new List<String>();
            var rs = Execute("wmic", "path win32_VideoController get name");
            if (!rs.IsNullOrEmpty())
            {
                var lines = rs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var name = line.Trim();
                    if (!name.IsNullOrEmpty() && !name.EqualIgnoreCase("Name") && !gpuList.Contains(name))
                        gpuList.Add(name);
                }
            }
            return gpuList.Count > 0 ? gpuList.Join(",") : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>获取已安装的VC++运行时版本</summary>
    /// <returns>VC++运行时版本列表，如"VC2015-2022 14.42,VC2013 12.0"</returns>
    private static String? GetVCRuntimeVersions()
    {
#if NET45_OR_GREATER || NET6_0_OR_GREATER
        try
        {
            var versions = new List<String>();

            // 检测路径列表，涵盖不同架构
            var basePaths = new[]
            {
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes",
                @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes",
            };

            using var hklm = Microsoft.Win32.Registry.LocalMachine;

            foreach (var basePath in basePaths)
            {
                using var baseKey = hklm.OpenSubKey(basePath);
                if (baseKey == null) continue;

                foreach (var archName in baseKey.GetSubKeyNames())
                {
                    using var archKey = baseKey.OpenSubKey(archName);
                    if (archKey == null) continue;

                    var major = archKey.GetValue("Major");
                    var minor = archKey.GetValue("Minor");
                    var bld = archKey.GetValue("Bld");
                    if (major != null)
                    {
                        // 构建版本字符串，如 "14.42.34433"
                        var ver = $"{major}";
                        if (minor != null) ver += $".{minor}";

                        // 根据主版本号确定 VC++ 年份
                        var vcYear = GetVCYear(major.ToString().ToInt());
                        var verStr = $"{vcYear} {ver}";

                        // 添加架构信息以区分32/64位
                        if (!archName.EqualIgnoreCase("x64", "x86"))
                            verStr += $"({archName})";

                        if (!versions.Any(v => v.StartsWith(vcYear)))
                            versions.Add(verStr);
                    }
                }
            }

            // 检测旧版本 VC++ 运行时（2008-2013）
            var oldVersions = GetOldVCRuntimeVersions();
            if (oldVersions != null)
                versions.AddRange(oldVersions);

            // 按版本号倒序排列
            return versions.Count > 0 ? versions.OrderByDescending(e => e).Join(",") : null;
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

#if NET45_OR_GREATER || NET6_0_OR_GREATER
    /// <summary>根据主版本号获取VC++年份标识</summary>
    /// <param name="major">主版本号</param>
    /// <returns>年份标识，如"VC2015-2022"</returns>
    private static String GetVCYear(Int32 major) => major switch
    {
        >= 14 => "VC2015-2022",
        12 => "VC2013",
        11 => "VC2012",
        10 => "VC2010",
        9 => "VC2008",
        8 => "VC2005",
        _ => $"VC{major}"
    };

    /// <summary>获取旧版本VC++运行时（2005-2013）</summary>
    private static IList<String>? GetOldVCRuntimeVersions()
    {
        var versions = new List<String>();

        try
        {
            using var hklm = Microsoft.Win32.Registry.LocalMachine;

            // 检测 VC++ 2013
            CheckOldVCRuntime(hklm, @"SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes", "VC2013", versions);
            CheckOldVCRuntime(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\12.0\VC\Runtimes", "VC2013", versions);

            // 检测 VC++ 2012
            CheckOldVCRuntime(hklm, @"SOFTWARE\Microsoft\VisualStudio\11.0\VC\Runtimes", "VC2012", versions);
            CheckOldVCRuntime(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\11.0\VC\Runtimes", "VC2012", versions);

            // 检测 VC++ 2010 (通过不同的注册表路径)
            if (CheckVCRuntimeInstalled(hklm, @"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x64", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist\x86", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\10.0\VC\VCRedist\x64", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\10.0\VC\VCRedist\x86", "Installed"))
            {
                if (!versions.Any(v => v.StartsWith("VC2010")))
                    versions.Add("VC2010 10.0");
            }

            // 检测 VC++ 2008
            if (CheckVCRuntimeInstalled(hklm, @"SOFTWARE\Microsoft\VisualStudio\9.0\VC\VCRedist\x64", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\Microsoft\VisualStudio\9.0\VC\VCRedist\x86", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\9.0\VC\VCRedist\x64", "Installed") ||
                CheckVCRuntimeInstalled(hklm, @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\9.0\VC\VCRedist\x86", "Installed"))
            {
                if (!versions.Any(v => v.StartsWith("VC2008")))
                    versions.Add("VC2008 9.0");
            }
        }
        catch { }

        return versions.Count > 0 ? versions : null;
    }

    /// <summary>检测旧版VC++运行时</summary>
    /// <param name="hklm">HKLM注册表</param>
    /// <param name="path">注册表路径</param>
    /// <param name="vcYear">VC++年份标识</param>
    /// <param name="versions">版本列表</param>
    private static void CheckOldVCRuntime(Microsoft.Win32.RegistryKey hklm, String path, String vcYear, IList<String> versions)
    {
        try
        {
            using var baseKey = hklm.OpenSubKey(path);
            if (baseKey == null) return;

            foreach (var archName in baseKey.GetSubKeyNames())
            {
                using var archKey = baseKey.OpenSubKey(archName);
                if (archKey == null) continue;

                var major = archKey.GetValue("Major");
                var minor = archKey.GetValue("Minor");
                if (major != null)
                {
                    var ver = $"{major}";
                    if (minor != null) ver += $".{minor}";

                    if (!versions.Any(v => v.StartsWith(vcYear)))
                        versions.Add($"{vcYear} {ver}");
                }
            }
        }
        catch { }
    }

    /// <summary>检查VC++运行时是否已安装</summary>
    /// <param name="hklm">HKLM注册表</param>
    /// <param name="path">注册表路径</param>
    /// <param name="valueName">值名称</param>
    private static Boolean CheckVCRuntimeInstalled(Microsoft.Win32.RegistryKey hklm, String path, String valueName)
    {
        try
        {
            using var key = hklm.OpenSubKey(path);
            if (key == null) return false;

            var value = key.GetValue(valueName);
            return value != null && value.ToString().ToInt() == 1;
        }
        catch
        {
            return false;
        }
    }
#endif

    private static void TryFillByGdiPlus(NodeInfo di)
    {
        try
        {
            var graphics = IntPtr.Zero;
            var num = NativeMethods.GdipCreateFromHWND(new HandleRef(null, IntPtr.Zero), out graphics);
            if (num == 0)
            {
                var xx = new Single[1];
                var numx = NativeMethods.GdipGetDpiX(new HandleRef(di, graphics), xx);

                var yy = new Single[1];
                var numy = NativeMethods.GdipGetDpiY(new HandleRef(di, graphics), yy);

                if (numx == 0 && numy == 0) di.Dpi = $"{xx[0]}*{yy[0]}";
            }
        }
        catch { }
    }

    private static void TryFillByReflection(NodeInfo di)
    {
        try
        {
            // System.Windows.Forms.Screen.PrimaryScreen.Bounds
            if (di.Resolution.IsNullOrEmpty())
            {
                var screenType = Type.GetType("System.Windows.Forms.Screen, System.Windows.Forms", false);
                if (screenType != null)
                {
                    var primaryScreen = screenType.GetProperty("PrimaryScreen")?.GetValue(null);
                    var bounds = primaryScreen?.GetType().GetProperty("Bounds")?.GetValue(primaryScreen);
                    var width = bounds?.GetType().GetProperty("Width")?.GetValue(bounds);
                    var height = bounds?.GetType().GetProperty("Height")?.GetValue(bounds);

                    if (width is Int32 w && height is Int32 h && w > 0 && h > 0)
                        di.Resolution = $"{w}*{h}";
                }
            }

            // System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiX/Y
            if (di.Dpi.IsNullOrEmpty())
            {
                var graphicsType = Type.GetType("System.Drawing.Graphics, System.Drawing", false);
                if (graphicsType != null)
                {
                    var mi = graphicsType.GetMethod("FromHwnd", new[] { typeof(IntPtr) });
                    var g = mi?.Invoke(null, new Object[] { IntPtr.Zero });
                    if (g != null)
                    {
                        var dpix = g.GetType().GetProperty("DpiX")?.GetValue(g);
                        var dpiy = g.GetType().GetProperty("DpiY")?.GetValue(g);

                        if (dpix is Single sx && dpiy is Single sy && sx > 0 && sy > 0)
                            di.Dpi = $"{sx}*{sy}";

                        // Graphics 实现 IDisposable
                        (g as IDisposable)?.Dispose();
                    }
                }
            }
        }
        catch { }
    }
}
