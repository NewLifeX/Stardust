using System.ComponentModel;

namespace Stardust.Models;

/// <summary>
/// 表示应用程序中任务或操作的优先级级别。
/// 以 Normal (0) 为基准，其他优先级相对其高低分布。
/// </summary>
public enum ProcessPriority
{
    /// <summary>
    /// 最低优先级，用于可延迟或非关键任务。
    /// </summary>
    [Description("空闲")]
    Idle = -2,

    /// <summary>
    /// 较低优先级，用于次要任务。
    /// </summary>
    [Description("较低")]
    BelowNormal = -1,

    /// <summary>
    /// 正常优先级，用于标准任务处理。
    /// </summary>
    [Description("正常")]
    Normal = 0,

    /// <summary>
    /// 较高优先级，用于需要尽快处理的重要任务。
    /// </summary>
    [Description("较高")]
    AboveNormal = 1,

    /// <summary>
    /// 高优先级，用于关键业务操作。
    /// </summary>
    [Description("高")]
    High = 2,

    /// <summary>
    /// 紧急优先级，用于必须立即处理的实时或关键任务。
    /// </summary>
    [Description("实时")]
    RealTime = 3
}
