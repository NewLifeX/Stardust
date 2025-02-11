using NewLife.Log;
using Stardust.Models;

namespace Stardust.Monitors;

/// <summary>追踪请求模型</summary>
public class TraceModel
{
    /// <summary>应用标识</summary>
    public String? AppId { get; set; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>应用信息</summary>
    public AppInfo? Info { get; set; }

    /// <summary>追踪数据</summary>
    public ISpanBuilder[]? Builders { get; set; }
}

/// <summary>追踪响应模型</summary>
public class TraceResponse
{
    /// <summary>采样周期。默认60s</summary>
    public Int32 Period { get; set; } = 60;

    /// <summary>最大正常采样数。采样周期内，最多只记录指定数量的正常事件，用于绘制依赖关系</summary>
    public Int32 MaxSamples { get; set; } = 1;

    /// <summary>最大异常采样数。采样周期内，最多只记录指定数量的异常事件，默认10</summary>
    public Int32 MaxErrors { get; set; } = 10;

    /// <summary>超时时间。超过该时间时，当作异常来进行采样，默认5000毫秒</summary>
    public Int32 Timeout { get; set; }

    /// <summary>最大标签长度。超过该长度时将截断，默认1024字符</summary>
    public Int32 MaxTagLength { get; set; } = 1024;

    /// <summary>请求标签长度。HttpClient请求和WebApi请求响应作为数据标签的最大长度，小于0时不使用，默认1024字符</summary>
    public Int32 RequestTagLength { get; set; } = 1024;

    /// <summary>性能收集。收集应用性能信息，数量较大的客户端可以不必收集应用性能信息</summary>
    public Boolean? EnableMeter { get; set; }

    /// <summary>要排除的操作名</summary>
    public String[]? Excludes { get; set; }
}