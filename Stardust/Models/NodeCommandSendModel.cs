#nullable enable
using System.ComponentModel;

namespace Stardust.Models;

/// <summary>节点命令下发请求模型</summary>
public class NodeCommandSendModel
{
    /// <summary>节点编号</summary>
    [DisplayName("节点编号")]
    public Int32 NodeId { get; set; }

    /// <summary>命令内容。可空：空串/null 放过模型绑定，由控制器统一校验并返回 400 信封（隐式 [Required] 会把空命令拦成 -2）</summary>
    [DisplayName("命令")]
    public String? Command { get; set; }
}
