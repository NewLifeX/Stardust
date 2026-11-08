using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace StarAgent;

/// <summary>NativeAOT 元数据保留根</summary>
/// <remarks>
/// 自定义命令处理器由 NewLife.Agent 反射扫描服务程序集发现；裁剪后类型元数据可能被移除，导致命令行静默丢命令。
/// 这里显式保留构造与公开方法，保证反射发现（未静态注册的构建形态）在 AOT 下同样可用。
/// </remarks>
internal static class AotRoots
{
#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandHandler.ListServices))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandHandler.StartService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandHandler.StopService))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandHandler.RestartService))]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Ensure() { }
}
