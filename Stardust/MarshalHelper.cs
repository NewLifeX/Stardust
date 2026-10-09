using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Stardust;

/// <summary>Marshal 助手。收敛非托管封送的跨目标框架差异</summary>
/// <remarks>
/// 泛型入口 Marshal.SizeOf&lt;T&gt; 与 Marshal.PtrToStructure&lt;T&gt; 自 .NET Framework 4.5.1 起才有，net45 目标缺失；
/// 同时泛型入口是 NativeAOT/裁剪友好的调用方式（布局解析可静态化，不需要运行时反射）。
/// Windows 系统信息探测（磁盘/ARP/WLAN）统一走这里，兼顾老目标框架与 AOT。
/// </remarks>
internal static class MarshalHelper
{
    /// <summary>获取指定类型实例的非托管大小</summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns>非托管大小</returns>
    public static Int32 SizeOf<T>() =>
#if NET45
        Marshal.SizeOf(typeof(T));
#else
        Marshal.SizeOf<T>();
#endif

    /// <summary>把非托管内存中的数据封送为指定类型实例</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="ptr">非托管内存指针</param>
    /// <returns>封送后的实例</returns>
    public static T PtrToStructure<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(IntPtr ptr) =>
#if NET45
        (T)Marshal.PtrToStructure(ptr, typeof(T))!;
#else
        Marshal.PtrToStructure<T>(ptr)!;
#endif
}
