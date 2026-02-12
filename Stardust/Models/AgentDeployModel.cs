#nullable enable
using System.ComponentModel;

namespace Stardust.Models;

/// <summary>StarAgent远程部署请求模型</summary>
public class AgentDeployModel
{
    /// <summary>目标主机列表，支持IP或网段（CIDR格式，如192.168.1.0/24）</summary>
    [DisplayName("目标主机")]
    public String Hosts { get; set; } = null!;

    /// <summary>SSH端口，默认22</summary>
    [DisplayName("SSH端口")]
    public Int32 Port { get; set; } = 22;

    /// <summary>SSH用户名</summary>
    [DisplayName("用户名")]
    public String UserName { get; set; } = null!;

    /// <summary>SSH密码</summary>
    [DisplayName("密码")]
    public String Password { get; set; } = null!;

    /// <summary>操作系统类型。Linux/Windows</summary>
    [DisplayName("操作系统")]
    public String OSType { get; set; } = "Linux";

    /// <summary>StarServer地址，StarAgent安装后将指向此地址</summary>
    [DisplayName("服务器地址")]
    public String? ServerUrl { get; set; }

    /// <summary>下载地址前缀，默认http://x.newlifex.com/star/</summary>
    [DisplayName("下载地址")]
    public String? DownloadUrl { get; set; }

    /// <summary>.NET版本，用于选择对应的StarAgent包（8/9/10）</summary>
    [DisplayName("dotnet版本")]
    public Int32 DotnetVersion { get; set; } = 9;
}

/// <summary>部署结果模型</summary>
public class AgentDeployResult
{
    /// <summary>目标主机</summary>
    public String Host { get; set; } = null!;

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>消息</summary>
    public String? Message { get; set; }

    /// <summary>输出日志</summary>
    public String? Output { get; set; }
}
