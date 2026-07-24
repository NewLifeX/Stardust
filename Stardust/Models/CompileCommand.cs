using System.Text;
using NewLife;

namespace Stardust.Models;

/// <summary>编译命令参数</summary>
public class CompileCommand
{
    /// <summary>代码库。下载代码的位置</summary>
    public String? Repository { get; set; }

    /// <summary>仓库密钥。SSH 私钥，用于非交互式拉取私有仓库代码</summary>
    public String? DeployKey { get; set; }

    /// <summary>仓库用户名。HTTPS 克隆时的用户名</summary>
    public String? RepoUserName { get; set; }

    /// <summary>仓库密码。AES 加密后的密文（Hex 格式），下发时用节点 Secret 加密</summary>
    public String? RepoPassword { get; set; }

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

        // 脱敏 RepoUserName：仅保留首字符 + ***
        safe.RepoUserName = RepoUserName.IsNullOrEmpty() ? null : RepoUserName[0] + "***";
        // 去掉 RepoPassword 密文
        safe.RepoPassword = null;

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

    /// <summary>
    /// 构建克隆 URL。根据凭据类型自动组装合适的 URL：
    /// 1. 有 SSH 密钥 + 用户名 → SSH 格式（git@host:owner/repo.git）
    /// 2. 有 SSH 密钥但无用户名 → 原样返回
    /// 3. 有用户名 + 密码 → HTTPS 凭据 URL（http://user:pass@host/repo.git）
    /// 4. 无凭据 → 原样返回
    /// </summary>
    /// <param name="repository">原始仓库 URL</param>
    /// <param name="userName">仓库用户名</param>
    /// <param name="deployKey">SSH 私钥</param>
    /// <param name="password">明文密码</param>
    /// <returns>组装后的克隆 URL</returns>
    public static String? BuildCloneUrl(String? repository, String? userName, String? deployKey, String? password)
    {
        if (repository.IsNullOrEmpty()) return repository;

        // SSH 密钥优先：有 SSH 密钥 + 用户名 → 组装 SSH 格式
        if (!deployKey.IsNullOrEmpty() && !userName.IsNullOrEmpty())
        {
            var uri = TryParseUri(repository);
            if (uri != null)
            {
                // SSH 格式：git@host:owner/repo.git
                var host = uri.Host;
                var path = uri.AbsolutePath.TrimStart('/');
                return $"{userName}@{host}:{path}";
            }
            // 如果解析失败，尝试从 git@ 格式提取
            if (repository.Contains('@'))
            {
                var atIdx = repository.IndexOf('@');
                var colonIdx = repository.IndexOf(':', atIdx);
                if (colonIdx > atIdx)
                {
                    var hostAndPath = repository[(atIdx + 1)..];
                    return $"{userName}@{hostAndPath}";
                }
            }
        }

        // 用户名 + 密码 → HTTPS 凭据 URL
        if (!userName.IsNullOrEmpty() && !password.IsNullOrEmpty())
        {
            var idx = repository.IndexOf("://", StringComparison.Ordinal);
            if (idx > 0)
            {
                var protocol = repository[..(idx + 3)];
                var rest = repository[(idx + 3)..];
                var atIdx = rest.IndexOf('@');
                if (atIdx > 0)
                {
                    rest = rest[(atIdx + 1)..];
                }
                return $"{protocol}{userName}:{password}@{rest}";
            }
        }

        return repository;
    }

    /// <summary>
    /// 脱敏 URL 中的密码用于日志输出
    /// </summary>
    public static String RedactUrlForLog(String url)
    {
        if (url.IsNullOrEmpty()) return url;

        var idx = url.IndexOf("://", StringComparison.Ordinal);
        if (idx > 0)
        {
            var atIdx = url.IndexOf('@', idx + 3);
            if (atIdx > idx + 3)
            {
                var creds = url[(idx + 3)..atIdx];
                var colonIdx = creds.IndexOf(':');
                if (colonIdx > 0)
                {
                    var user = creds[..colonIdx];
                    return url[..(idx + 3)] + user + ":***@" + url[(atIdx + 1)..];
                }
            }
        }

        return url;
    }

    /// <summary>尝试解析 URI</summary>
    private static Uri? TryParseUri(String url)
    {
        try
        {
            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return new Uri(url);
        }
        catch { }
        return null;
    }
}
