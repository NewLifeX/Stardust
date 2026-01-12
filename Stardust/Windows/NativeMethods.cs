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
    internal static extern Int32 GdipDeleteGraphics(HandleRef graphics);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern Int32 GdipGetDpiX(HandleRef graphics, Single[] dpi);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    internal static extern Int32 GdipGetDpiY(HandleRef graphics, Single[] dpi);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetDesktopWindow();

    // Windows 10 (1607)+
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern UInt32 GetDpiForWindow(IntPtr hwnd);

    // Windows 10+ (and Windows 8.1 via shcore's GetDpiForMonitor; not used here)
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern UInt32 GetDpiForSystem();

    internal struct StartupInput
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

    internal struct StartupOutput
    {
        public IntPtr hook;

        public IntPtr unhook;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern Int32 GetSystemMetrics(Int32 nIndex);

    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern Boolean EmptyWorkingSet(IntPtr hProcess);

    #region DXGI - GPU 信息获取
    /// <summary>创建 DXGI Factory</summary>
    /// <param name="riid">IDXGIFactory 的 GUID</param>
    /// <param name="ppFactory">返回的 Factory 对象</param>
    /// <returns>HRESULT</returns>
    [DllImport("dxgi.dll", ExactSpelling = true, PreserveSig = true)]
    internal static extern Int32 CreateDXGIFactory([In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IDXGIFactory ppFactory);

    /// <summary>创建 DXGI Factory（便捷重载）</summary>
    internal static Int32 CreateDXGIFactory(Guid riid, out IDXGIFactory? ppFactory)
    {
        return CreateDXGIFactory(ref riid, out ppFactory);
    }
    #endregion
}

#region DXGI COM 接口定义
/// <summary>DXGI Factory 接口</summary>
[ComImport, Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIFactory
{
    /// <summary>设置私有数据</summary>
    [PreserveSig]
    Int32 SetPrivateData([In] ref Guid Name, UInt32 DataSize, IntPtr pData);

    /// <summary>设置私有数据接口</summary>
    [PreserveSig]
    Int32 SetPrivateDataInterface([In] ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] Object pUnknown);

    /// <summary>获取私有数据</summary>
    [PreserveSig]
    Int32 GetPrivateData([In] ref Guid Name, ref UInt32 pDataSize, IntPtr pData);

    /// <summary>获取父对象</summary>
    [PreserveSig]
    Int32 GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out Object ppParent);

    /// <summary>枚举显卡适配器</summary>
    /// <param name="Adapter">适配器索引（从0开始）</param>
    /// <param name="ppAdapter">返回的适配器对象</param>
    /// <returns>HRESULT，DXGI_ERROR_NOT_FOUND (0x887A0002) 表示没有更多适配器</returns>
    [PreserveSig]
    Int32 EnumAdapters(UInt32 Adapter, [Out, MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter ppAdapter);

    /// <summary>创建软件适配器</summary>
    [PreserveSig]
    Int32 MakeWindowAssociation(IntPtr WindowHandle, UInt32 Flags);

    /// <summary>获取关联窗口</summary>
    [PreserveSig]
    Int32 GetWindowAssociation(out IntPtr pWindowHandle);

    /// <summary>创建交换链</summary>
    [PreserveSig]
    Int32 CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] Object pDevice, IntPtr pDesc, out IntPtr ppSwapChain);

    /// <summary>创建软件适配器</summary>
    [PreserveSig]
    Int32 CreateSoftwareAdapter(IntPtr Module, [Out, MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter ppAdapter);
}

/// <summary>DXGI 适配器接口</summary>
[ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIAdapter
{
    /// <summary>设置私有数据</summary>
    [PreserveSig]
    Int32 SetPrivateData([In] ref Guid Name, UInt32 DataSize, IntPtr pData);

    /// <summary>设置私有数据接口</summary>
    [PreserveSig]
    Int32 SetPrivateDataInterface([In] ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] Object pUnknown);

    /// <summary>获取私有数据</summary>
    [PreserveSig]
    Int32 GetPrivateData([In] ref Guid Name, ref UInt32 pDataSize, IntPtr pData);

    /// <summary>获取父对象</summary>
    [PreserveSig]
    Int32 GetParent([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out Object ppParent);

    /// <summary>枚举输出设备</summary>
    [PreserveSig]
    Int32 EnumOutputs(UInt32 Output, out IntPtr ppOutput);

    /// <summary>获取适配器描述信息</summary>
    /// <param name="pDesc">返回的描述结构</param>
    /// <returns>HRESULT</returns>
    [PreserveSig]
    Int32 GetDesc(out DXGI_ADAPTER_DESC pDesc);

    /// <summary>检查接口支持</summary>
    [PreserveSig]
    Int32 CheckInterfaceSupport([In] ref Guid InterfaceName, out Int64 pUMDVersion);
}

/// <summary>DXGI 适配器描述结构</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DXGI_ADAPTER_DESC
{
    /// <summary>显卡描述（最多128个字符）</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public String Description;

    /// <summary>厂商 ID</summary>
    public UInt32 VendorId;

    /// <summary>设备 ID</summary>
    public UInt32 DeviceId;

    /// <summary>子系统 ID</summary>
    public UInt32 SubSysId;

    /// <summary>修订版本</summary>
    public UInt32 Revision;

    /// <summary>专用显存大小（字节）</summary>
    public UIntPtr DedicatedVideoMemory;

    /// <summary>专用系统内存大小（字节）</summary>
    public UIntPtr DedicatedSystemMemory;

    /// <summary>共享系统内存大小（字节）</summary>
    public UIntPtr SharedSystemMemory;

    /// <summary>适配器 LUID</summary>
    public Int64 AdapterLuid;
}
#endregion