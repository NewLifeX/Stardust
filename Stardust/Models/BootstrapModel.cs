namespace Stardust.Models;

/// <summary>引导安装模型。原生 Agent 用它安装 .NET 运行时与完整版 StarAgent</summary>
/// <remarks>
/// 原生 Agent（Go/Rust/C++）只做采集与指令响应，需要完整能力时应引导安装 .NET 版 StarAgent。
/// 服务端按节点操作系统与 CPU 架构匹配运行时包与完整版安装包，客户端负责下载、校验与执行安装。
/// </remarks>
public class BootstrapModel
{
    /// <summary>目标版本。完整版 StarAgent 的版本号</summary>
    public String? Version { get; set; }

    /// <summary>服务端地址。安装完整版后用于注册的服务器地址</summary>
    public String? Server { get; set; }

    /// <summary>节点编码。完整版安装后复用同一节点身份</summary>
    public String? Code { get; set; }

    /// <summary>运行时安装包。为空表示该平台无需安装运行时</summary>
    public BootstrapPackage? Runtime { get; set; }

    /// <summary>完整版 StarAgent 安装包</summary>
    public BootstrapPackage? Agent { get; set; }
}

/// <summary>引导安装包</summary>
public class BootstrapPackage
{
    /// <summary>版本号</summary>
    public String? Version { get; set; }

    /// <summary>安装类型。runtime/aspnet/desktop/host，完整版安装包为空</summary>
    public String? Kind { get; set; }

    /// <summary>文件名</summary>
    public String? FileName { get; set; }

    /// <summary>下载地址。相对路径表示服务端静态目录或附件</summary>
    public String? Source { get; set; }

    /// <summary>文件大小</summary>
    public Int64 Size { get; set; }

    /// <summary>文件哈希。优先SHA512，其次MD5</summary>
    public String? FileHash { get; set; }
}
