namespace Stardust.Models;

/// <summary>平台级发送命令参数模型</summary>
public class CommandInModel
{
    /// <summary>编码。节点编码或应用编码</summary>
    public String? Code { get; set; }

    /// <summary>命令</summary>
    public String Command { get; set; } = null!;

    /// <summary>参数</summary>
    public String? Argument { get; set; }

    /// <summary>有效期。多久之后指令过期，单位秒，未指定时表示不限制</summary>
    public Int32 Expire { get; set; }

    /// <summary>超时时间。等待响应的时间，单位秒，未指定时表示不等待</summary>
    public Int32 Timeout { get; set; }
}