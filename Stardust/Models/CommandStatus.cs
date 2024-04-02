namespace Stardust.Models;

/// <summary>命令状态</summary>
public enum CommandStatus
{
    /// <summary>就绪</summary>
    就绪 = 0,

    /// <summary>处理中</summary>
    处理中 = 1,

    /// <summary>已完成</summary>
    已完成 = 2,

    /// <summary>取消</summary>
    取消 = 3,

    /// <summary>错误</summary>
    错误 = 4,
}