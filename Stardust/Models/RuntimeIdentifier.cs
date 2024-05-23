namespace Stardust.Models;

/// <summary>运行时标识符。定义应用包可运行的操作系统与处理器架构</summary>
public enum RuntimeIdentifier
{
    /// <summary>任意</summary>
    Any = 0,

    /// <summary>任意Windows系统</summary>
    Win = 10,
    /// <summary>Windows系统（32位）</summary>
    WinX86 = 11,
    /// <summary>Windows系统（64位）</summary>
    WinX64 = 12,
    /// <summary>Windows系统（32位Arm）</summary>
    WinArm = 13,
    /// <summary>Windows系统（64位Arm）</summary>
    WinArm64 = 14,

    /// <summary>任意Linux系统</summary>
    Linux = 20,
    ///// <summary>Linux系统（32位）</summary>
    //LinuxX86 = 21,
    /// <summary>Linux系统（64位）</summary>
    LinuxX64 = 22,
    /// <summary>Linux系统（32位Arm）</summary>
    LinuxArm = 23,
    /// <summary>Linux系统（64位Arm）</summary>
    LinuxArm64 = 24,
    /// <summary>Linux系统（64位MIPS）</summary>
    LinuxMips64 = 26,
    /// <summary>Linux系统（龙芯）</summary>
    LinuxLA64 = 27,
    /// <summary>Linux系统（RISC）</summary>
    LinuxRiscV64 = 28,

    /// <summary>任意Linux系统（musl库）</summary>
    LinuxMusl = 30,
    /// <summary>Linux系统（musl库64位）</summary>
    LinuxMuslX64 = 32,
    /// <summary>Linux系统（musl库32位Arm）</summary>
    LinuxMuslArm = 33,
    /// <summary>Linux系统（musl库64位Arm）</summary>
    LinuxMuslArm64 = 34,

    /// <summary>任意OSX系统</summary>
    Osx = 40,
    ///// <summary>OSX系统（32位）</summary>
    //OsxX86 = 41,
    /// <summary>OSX系统（64位）</summary>
    OsxX64 = 42,
    ///// <summary>OSX系统（32位Arm）</summary>
    //OsxArm = 43,
    /// <summary>OSX系统（64位Arm）</summary>
    OsxArm64 = 44,
}
