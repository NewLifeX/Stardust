namespace Stardust.Models;

/// <summary>编译命令参数</summary>
public class CompileCommand
{
    /// <summary>代码库。下载代码的位置</summary>
    public String? Repository { get; set; }

    /// <summary>仓库密钥。SSH 私钥，用于非交互式拉取私有仓库代码</summary>
    public String? DeployKey { get; set; }

    /// <summary>分支</summary>
    public String? Branch { get; set; }

    /// <summary>源代码目录。本地已有源码的路径，优先使用</summary>
    public String? SourcePath { get; set; }

    /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
    public String? ProjectPath { get; set; }

    /// <summary>项目类型。默认dotnet</summary>
    public ProjectKinds ProjectKind { get; set; }

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

    /// <summary>生成脱敏后的历史记录副本，去掉 DeployKey 和 Repository 中的凭据</summary>
    /// <returns>脱敏后的副本</returns>
    public CompileCommand RedactForHistory()
    {
        var safe = (CompileCommand)MemberwiseClone();

        // 去掉 DeployKey 私钥
        safe.DeployKey = null;

        // 从 Repository URL 中移除凭据（如 http://user:pass@host → http://host）
        if (safe.Repository != null)
        {
            var idx = safe.Repository.IndexOf("://", StringComparison.Ordinal);
            if (idx > 0)
            {
                var atIdx = safe.Repository.IndexOf('@', idx + 3);
                if (atIdx > idx + 3)
                {
                    safe.Repository = safe.Repository[..(idx + 3)] + safe.Repository[(atIdx + 1)..];
                }
            }
        }

        return safe;
    }
}
