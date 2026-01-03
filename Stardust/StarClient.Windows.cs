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
        }
        catch { }
    }

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
