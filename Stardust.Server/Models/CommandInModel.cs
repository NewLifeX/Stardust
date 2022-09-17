using System;

namespace Stardust.Server.Models;

public class CommandInModel
{
    /// <summary>节点编码</summary>
    public String Code { get; set; }

    /// <summary>命令</summary>
    public String Command { get; set; }

    /// <summary>参数</summary>
    public String Argument { get; set; }

    /// <summary>有效期。多久之后指令过期，单位秒，未指定时表示不限制</summary>
    public Int32 Expire { get; set; }
}