namespace Stardust.Models;

/// <summary>服务工作模式</summary>
public enum ServiceModes
{
    /// <summary>默认。单例运行exe或zip</summary>
    Default = 0,

    /// <summary>解压缩。仅解压缩文件，由外部主机托管，如IIS</summary>
    Extract = 1,

    /// <summary>解压并运行。</summary>
    ExtractAndRun = 2,

    /// <summary>仅运行一次。运行后自动进入禁用状态</summary>
    RunOnce = 3,

    /// <summary>多实例。直接运行exe或zip，支持多实例</summary>
    Multiple = 4,
}