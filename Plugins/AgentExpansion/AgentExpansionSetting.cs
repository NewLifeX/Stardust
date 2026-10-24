using System.ComponentModel;
using NewLife.Configuration;

namespace AgentExpansion;

/// <summary>代理扩展设置</summary>
[Config("AgentExpansion")]
public class AgentExpansionSetting : Config<AgentExpansionSetting>
{
    #region 属性
    /// <summary>启用</summary>
    [Description("Enable")]
    public Boolean Enable { get; set; } = true;

    /// <summary>扫描周期。单位秒，默认3600</summary>
    [Description("Scan period. Unit: seconds, default 3600.")]
    public Int32 Period { get; set; } = 3600;

    /// <summary>目标网段。支持192.168.1.0/24或192.168.1.*，为空表示本地网段。仅支持局域网网段</summary>
    /// <remarks>
    /// 只接受 RFC1918 私有网段（10.0.0.0/8、172.16.0.0/12、192.168.0.0/16），
    /// 链路本地 169.254.0.0/16、回环、多播、保留以及公网地址会被忽略，避免误配导致全网段扫描。
    /// </remarks>
    [Description("Target subnet. Supports 192.168.1.0/24 or 192.168.1.*; empty means the local subnet. Only LAN subnets are supported.")]
    public String? Networks { get; set; }

    /// <summary>账号。可使用env:NAME引用环境变量</summary>
    [Description("Account. Use env:NAME to reference an environment variable")]
    public String? UserName { get; set; }

    /// <summary>密码。可使用env:NAME引用环境变量</summary>
    [Description("Password. Can reference an environment variable with env:NAME.")]
    public String? Password { get; set; }

    /// <summary>SSH端口。默认22</summary>
    [Description("SSH port. Default 22")]
    public Int32 SshPort { get; set; } = 22;

    /// <summary>Telnet端口。默认23</summary>
    [Description("Telnet port. Default 23")]
    public Int32 TelnetPort { get; set; } = 23;

    /// <summary>连接超时。默认3000ms</summary>
    [Description("Connection timeout. Default 3000ms")]
    public Int32 Timeout { get; set; } = 3000;

    /// <summary>最大并发。默认64</summary>
    [Description("Max concurrency. Default 64")]
    public Int32 MaxConcurrent { get; set; } = 64;

    /// <summary>最大扫描主机数。默认4096</summary>
    [Description("Max scan hosts. Default 4096")]
    public Int32 MaxHosts { get; set; } = 4096;

    /// <summary>安装包地址。留空则使用PluginServer</summary>
    [Description("Installation package URL. Leave blank to use PluginServer.")]
    public String? PackageUrl { get; set; }

    /// <summary>安装包哈希。SHA512字符串，用于校验包完整性</summary>
    [Description("Installation package hash. SHA512 string used to verify package integrity.")]
    public String? PackageSha512 { get; set; }

    /// <summary>SSH主机指纹。用于校验SSH连接</summary>
    [Description("SSH host fingerprint. Used to verify SSH connections")]
    public String? SshHostKey { get; set; }

    /// <summary>安装目录。留空使用默认路径</summary>
    [Description("Installation directory. Leave blank to use the default path.")]
    public String? TargetPath { get; set; }
    #endregion
}
