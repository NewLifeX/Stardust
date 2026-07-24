namespace DeployAgent;

/// <summary>编译命令参数</summary>
public class CompileCommand
{
    /// <summary>
    /// 代码库地址。下载代码的位置，支持 SSH（git@host:repo.git）和 HTTP（http://user@host/repo.git）格式。
    /// <para>Linux 机器如果使用 HTTP 格式，必须使用带用户名的 HTTP 格式（http://user@host/repo.git），否则 Git 会因无法弹出认证窗口而失败。</para>
    /// <para>Windows 机器首次使用 HTTP 格式时，需在目标机器上手动执行一次 git clone 以让 Git Credential Manager 缓存凭据，后续 DeployAgent 即可无交互拉取。</para>
    /// </summary>
    public String? Repository { get; set; }

    /// <summary>仓库密钥。SSH 私钥，用于非交互式拉取私有仓库代码</summary>
    public String? DeployKey { get; set; }

    /// <summary>仓库用户名。HTTPS 克隆时的用户名</summary>
    public String? RepoUserName { get; set; }

    /// <summary>仓库密码。AES 加密后的密文（Hex 格式），下发时用节点 Secret 加密</summary>
    public String? RepoPassword { get; set; }

    /// <summary>分支</summary>
    public String? Branch { get; set; } = "main";

    /// <summary>源代码目录。本地已有源码的路径，优先使用</summary>
    public String? SourcePath { get; set; }

    /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
    public String? ProjectPath { get; set; }

    /// <summary>项目类型。默认dotnet</summary>
    public Int32 ProjectKind { get; set; }

    /// <summary>编译参数。编译项目时所需参数</summary>
    public String? BuildArgs { get; set; }

    /// <summary>编译输出目录。默认publish</summary>
    public String? OutputPath { get; set; } = "publish";

    /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
    public String? PackageFilters { get; set; }

    /// <summary>应用部署集名称。用于上传到星尘</summary>
    public String? DeployName { get; set; }

    /// <summary>拉取代码</summary>
    public Boolean PullCode { get; set; }

    /// <summary>编译项目</summary>
    public Boolean BuildProject { get; set; }

    /// <summary>打包输出</summary>
    public Boolean PackageOutput { get; set; }

    /// <summary>上传应用包</summary>
    public Boolean UploadPackage { get; set; }
}
