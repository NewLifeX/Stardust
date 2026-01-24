namespace Stardust.Models;

/// <summary>CPU指令集架构</summary>
public enum CpuArch
{
    /// <summary>未知或通用</summary>
    Any = 0,

    /// <summary>x86（32位）</summary>
    X86 = 1,

    /// <summary>x64（64位）</summary>
    X64 = 2,

    /// <summary>ARM（32位）</summary>
    Arm = 3,

    /// <summary>ARM64（64位）</summary>
    Arm64 = 4,

    /// <summary>龙芯LoongArch64</summary>
    LA64 = 5,

    /// <summary>RISC-V 64位</summary>
    RiscV64 = 6,

    /// <summary>MIPS 64位</summary>
    Mips64 = 7,
}
