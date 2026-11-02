namespace Stardust.Models;

/// <summary>安装引导清单。安装引导页与安装脚本的数据来源</summary>
/// <remarks>
/// 清单优先取后台维护的产品版本与发布包，缺失时回退公共镜像，保证页面开箱可用。
/// 下载地址统一指向本站 /install/download/{kind}/{id}，由服务端在点击或执行时现场签发短时令牌，
/// 因此清单里不会出现会过期的签名地址。相对地址由客户端按 <see cref="Server"/> 补全。
/// </remarks>
public class InstallManifest
{
    /// <summary>服务端地址。StarAgent 安装后注册的目标地址，也是相对下载地址的补全基址</summary>
    public String? Server { get; set; }

    /// <summary>升级通道。release/beta/develop</summary>
    public String? Channel { get; set; }

    /// <summary>清单生成时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>StarAgent 发布版本，按新到旧排列</summary>
    public IList<InstallVersion> Versions { get; set; } = [];

    /// <summary>.NET 运行时安装包</summary>
    public IList<InstallPackage> Runtimes { get; set; } = [];

    /// <summary>公共镜像中的历史安装包。服务端未维护对应包时兜底</summary>
    public IList<InstallPackage> Mirror { get; set; } = [];
}

/// <summary>安装引导清单中的一个发布版本</summary>
public class InstallVersion
{
    /// <summary>发布版本编号</summary>
    public Int32 Id { get; set; }

    /// <summary>版本号</summary>
    public String? Version { get; set; }

    /// <summary>升级通道</summary>
    public String? Channel { get; set; }

    /// <summary>是否强制升级</summary>
    public Boolean Force { get; set; }

    /// <summary>安装包集合</summary>
    public IList<InstallPackage> Packages { get; set; } = [];
}

/// <summary>安装引导清单中的一个安装包</summary>
public class InstallPackage
{
    /// <summary>包编号。公共镜像包为0</summary>
    public Int32 Id { get; set; }

    /// <summary>包类型。agent（.NET版StarAgent）/ native（原生Agent）/ runtime（.NET运行时）</summary>
    public String? Kind { get; set; }

    /// <summary>版本号。.NET运行时为运行时版本，公共镜像包为框架名</summary>
    public String? Version { get; set; }

    /// <summary>目标操作系统。Any表示通用</summary>
    public String? OS { get; set; }

    /// <summary>目标CPU架构。Any表示通用</summary>
    public String? Arch { get; set; }

    /// <summary>目标运行时。.NET为主版本号，原生Agent为go/rust/cpp，*表示万能包</summary>
    public String? Runtime { get; set; }

    /// <summary>文件名</summary>
    public String? FileName { get; set; }

    /// <summary>下载地址。本站包为相对地址，公共镜像为绝对地址</summary>
    public String? Url { get; set; }

    /// <summary>文件大小。公共镜像包未知时为0</summary>
    public Int64 Size { get; set; }

    /// <summary>文件哈希。优先SHA512，其次MD5；公共镜像包为空</summary>
    public String? FileHash { get; set; }

    /// <summary>来源。db表示后台维护，mirror表示公共镜像兜底</summary>
    public String? Origin { get; set; }
}
