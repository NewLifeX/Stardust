#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>模块初始化特性。旧目标框架没有该 BCL 类型，补齐垫片供源生成器使用</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute { }
#endif