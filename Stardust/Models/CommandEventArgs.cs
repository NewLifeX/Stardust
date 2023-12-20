namespace Stardust.Models;

/// <summary>
/// 命令事件参数
/// </summary>
public class CommandEventArgs : EventArgs
{
    /// <summary>
    /// 命令
    /// </summary>
    public CommandModel? Model { get; set; }

    /// <summary>
    /// 响应
    /// </summary>
    public CommandReplyModel? Reply { get; set; }
}