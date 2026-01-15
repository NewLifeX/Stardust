namespace Stardust.Models;

/// <summary>操作系统类型</summary>
public enum OSKind
{
    /// <summary>未知或通用</summary>
    Unknown = 0,

    /// <summary>Windows系统</summary>
    Windows = 1,

    /// <summary>Linux系统（glibc）</summary>
    Linux = 2,

    /// <summary>Linux系统（musl库，如Alpine）</summary>
    LinuxMusl = 3,

    /// <summary>macOS系统</summary>
    OSX = 4,

    /// <summary>Android系统</summary>
    Android = 5,

    /// <summary>iOS系统</summary>
    iOS = 6,
}
