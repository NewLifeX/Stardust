using System.Runtime.InteropServices;

namespace Stardust;

internal class NativeMethods
{
    private static IntPtr initToken;

    static NativeMethods() => Initialize();

    private static void Initialize()
    {
        var input = StartupInput.GetDefault();
        var num = GdiplusStartup(out initToken, ref input, out var output);
        if (num == 0)
        {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.ProcessExit += OnProcessExit;
            if (!currentDomain.IsDefaultAppDomain())
            {
                currentDomain.DomainUnload += OnProcessExit;
            }
        }
    }

    private static void OnProcessExit(Object? sender, EventArgs e)
    {
        if (initToken != IntPtr.Zero)
        {
            var namedDataSlot = Thread.GetNamedDataSlot("system.drawing.threaddata");
            Thread.SetData(namedDataSlot, null);

            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.ProcessExit -= OnProcessExit;
            if (!currentDomain.IsDefaultAppDomain())
            {
                currentDomain.DomainUnload -= OnProcessExit;
            }
        }
    }

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern Int32 GdiplusStartup(out IntPtr token, ref StartupInput input, out StartupOutput output);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern Int32 GdipCreateFromHWND(HandleRef hwnd, out IntPtr graphics);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern Int32 GdipGetDpiX(HandleRef graphics, Single[] dpi);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern Int32 GdipGetDpiY(HandleRef graphics, Single[] dpi);

    struct StartupInput
    {
        public Int32 GdiplusVersion;

        public IntPtr DebugEventCallback;

        public Boolean SuppressBackgroundThread;

        public Boolean SuppressExternalCodecs;

        public static StartupInput GetDefault()
        {
            var result = default(StartupInput);
            result.GdiplusVersion = 1;
            result.SuppressBackgroundThread = false;
            result.SuppressExternalCodecs = false;
            return result;
        }
    }

    struct StartupOutput
    {
        public IntPtr hook;

        public IntPtr unhook;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern Int32 GetSystemMetrics(Int32 nIndex);

    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern Boolean EmptyWorkingSet(IntPtr hProcess);
}