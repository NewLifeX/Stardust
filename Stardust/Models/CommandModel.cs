namespace Stardust.Models;

/// <summary>命令模型</summary>
public class CommandModel
{
    /// <summary>序号</summary>
    public Int32 Id { get; set; }

    /// <summary>命令</summary>
    public String Command { get; set; } = null!;

    /// <summary>参数</summary>
    public String? Argument { get; set; }

    /// <summary>开始执行时间。用于提前下发指令后延期执行，暂时不支持取消</summary>
    public DateTime StartTime { get; set; }

    /// <summary>过期时间。未指定时表示不限制</summary>
    public DateTime Expire { get; set; }

    /// <summary>跟踪标识。传输traceParent，用于建立全局调用链，便于查找问题</summary>
    public String? TraceId { get; set; }
}