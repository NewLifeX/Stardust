using System.ComponentModel;
using NewLife.Configuration;

namespace AgentExpansion;

/// <summary>代理扩展设置</summary>
[Config("AgentExpansion")]
public class AgentExpansionSetting : Config<AgentExpansionSetting>
{
    #region 属性
    /// <summary>启用</summary>
    [Description("启用")]
    public Boolean Enable { get; set; } = true;

    /// <summary>扫描周期。单位秒，默认3600</summary>
    [Description("扫描周期。单位秒，默认3600")]
    public Int32 Period { get; set; } = 3600;

    /// <summary>目标网段。支持192.168.1.0/24或192.168.1.*，为空表示本地网段</summary>
    [Description("目标网段。支持192.168.1.0/24或192.168.1.*，为空表示本地网段")]
    public String? Networks { get; set; }

    /// <summary>账号</summary>
    [Description("账号")]
    public String? UserName { get; set; }

    /// <summary>密码</summary>
    [Description("密码")]
    public String? Password { get; set; }

    /// <summary>SSH端口。默认22</summary>
    [Description("SSH端口。默认22")]
    public Int32 SshPort { get; set; } = 22;

    /// <summary>Telnet端口。默认23</summary>
    [Description("Telnet端口。默认23")]
    public Int32 TelnetPort { get; set; } = 23;

    /// <summary>连接超时。默认3000ms</summary>
    [Description("连接超时。默认3000ms")]
    public Int32 Timeout { get; set; } = 3000;

    /// <summary>最大并发。默认64</summary>
    [Description("最大并发。默认64")]
    public Int32 MaxConcurrent { get; set; } = 64;

    /// <summary>最大扫描主机数。默认4096</summary>
    [Description("最大扫描主机数。默认4096")]
    public Int32 MaxHosts { get; set; } = 4096;

    /// <summary>安装包地址。留空则使用PluginServer</summary>
    [Description("安装包地址。留空则使用PluginServer")]
    public String? PackageUrl { get; set; }

    /// <summary>安装目录。留空使用默认路径</summary>
    [Description("安装目录。留空使用默认路径")]
    public String? TargetPath { get; set; }
    #endregion
}
