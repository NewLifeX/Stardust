namespace Stardust.Models;

/// <summary>
/// 命令响应模型
/// </summary>
public class CommandReplyModel
{
    /// <summary>服务编号</summary>
    public Int32 Id { get; set; }

    /// <summary>状态</summary>
    public CommandStatus Status { get; set; }

    /// <summary>返回数据</summary>
    public String? Data { get; set; }
}