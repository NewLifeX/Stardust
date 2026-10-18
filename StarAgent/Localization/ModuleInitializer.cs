#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1
namespace System.Runtime.CompilerServices;

/// <summary>模块初始化特性垫片</summary>
/// <remarks>低版本目标框架的 BCL 不包含该特性，NewLife.Core 程序集内版本不可见，需在本程序集内补齐。</remarks>
[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : System.Attribute { }
#endif
