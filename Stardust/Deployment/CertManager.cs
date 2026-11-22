using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Deployment;

/// <summary>证书下发管理器。把平台下发的 SSL 证书文件落地到本机目录，并维护本地清单供 Nginx 配置引用</summary>
/// <remarks>
/// 目录约定：默认 ../certs（相对 StarAgent 安装目录，Linux 上通常是 /root/certs）。
/// 文件命名：{证书名}.pem（证书链）、{证书名}.privatekey.pem（私钥）、{证书名}.pfx（Windows）。
/// 本地清单：数据目录下的 certs.json，记录已落地证书的域名、文件路径与版本，用于跳过未变更的下载。
/// </remarks>
public class CertManager
{
    #region 属性
    /// <summary>默认证书目录。相对 StarAgent 安装目录</summary>
    public const String DefaultCertPath = "../certs";

    /// <summary>本地清单文件名</summary>
    public const String ManifestFileName = "certs.json";

    /// <summary>全局实例。StarAgent 启动时按配置调整证书目录</summary>
    public static CertManager Current { get; } = new();

    /// <summary>证书目录。支持绝对路径或相对 StarAgent 安装目录的路径，默认 ../certs</summary>
    public String CertPath { get; set; } = DefaultCertPath;

    /// <summary>数据目录。存放本地清单，默认 NewLife.Setting.DataPath</summary>
    public String? DataPath { get; set; }

    /// <summary>日志</summary>
    public ILog? Log { get; set; }

    /// <summary>同步锁。避免多条 cert/sync 指令并发改写清单</summary>
    private static readonly SemaphoreSlim _lock = new(1, 1);
    #endregion

    #region 目录与清单
    /// <summary>获取证书目录绝对路径</summary>
    /// <returns></returns>
    public String GetCertPath() => ResolvePath(CertPath.IsNullOrEmpty() ? DefaultCertPath : CertPath);

    /// <summary>获取本地清单文件绝对路径</summary>
    /// <returns></returns>
    public String GetManifestFile()
    {
        var dir = DataPath;
        if (dir.IsNullOrEmpty()) dir = NewLife.Setting.Current.DataPath;
        if (dir.IsNullOrEmpty()) dir = "Data";

        return ResolvePath(dir).CombinePath(ManifestFileName);
    }

    /// <summary>解析目录。绝对路径原样使用，相对路径基于应用程序目录</summary>
    /// <param name="path">目录</param>
    /// <returns></returns>
    public static String ResolvePath(String path)
    {
        if (path.IsNullOrEmpty()) return path;

        path = path.Trim();
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

        return path.GetBasePath();
    }

    /// <summary>加载本地证书清单</summary>
    /// <returns></returns>
    public IList<CertManifest> LoadManifest()
    {
        var file = GetManifestFile();
        if (!File.Exists(file)) return [];

        try
        {
            return File.ReadAllText(file).ToJsonEntity<List<CertManifest>>() ?? [];
        }
        catch (Exception ex)
        {
            Log?.Warn("Failed to load certificate manifest: {0}", ex.Message);
            return [];
        }
    }

    /// <summary>保存本地证书清单</summary>
    /// <param name="list">清单</param>
    public void SaveManifest(IEnumerable<CertManifest> list)
    {
        var file = GetManifestFile();
        try
        {
            file.EnsureDirectory(true);
            File.WriteAllText(file, list.ToList().ToJson());
        }
        catch (Exception ex)
        {
            Log?.Warn("Failed to save certificate manifest: {0}", ex.Message);
        }
    }

    /// <summary>查找匹配指定主机名的本地证书</summary>
    /// <param name="host">主机名，如 sso.newlifex.com</param>
    /// <returns></returns>
    public CertManifest? FindByHost(String host)
    {
        if (host.IsNullOrEmpty()) return null;

        host = host.Trim().TrimEnd('.');
        CertManifest? wildcard = null;
        foreach (var item in LoadManifest())
        {
            foreach (var domain in (item.Domain + "").Split(',', ';'))
            {
                var str = domain.Trim().TrimEnd('.');
                if (str.IsNullOrEmpty()) continue;

                if (str.EqualIgnoreCase(host)) return item;

                if (str.StartsWith("*.") && host.Length > str.Length - 1 && host.EndsWith(str.Substring(1), StringComparison.OrdinalIgnoreCase))
                    wildcard ??= item;
            }
        }

        return wildcard;
    }
    #endregion

    #region 同步
    /// <summary>同步证书文件到本地。已存在且版本一致时跳过下载</summary>
    /// <param name="certs">平台下发的证书信息</param>
    /// <param name="buildUrl">相对下载地址转绝对地址。StarAgent 传入客户端的 BuildUrl</param>
    /// <param name="replace">是否用本次清单替换本地清单。同步全部证书时使用，可清理已删除证书的残留记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步结果</returns>
    public async Task<CertSyncResult> SyncAsync(IEnumerable<CertInfo> certs, Func<String, String?>? buildUrl = null, Boolean replace = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SyncCoreAsync(certs, buildUrl, replace, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CertSyncResult> SyncCoreAsync(IEnumerable<CertInfo> certs, Func<String, String?>? buildUrl, Boolean replace, CancellationToken cancellationToken)
    {
        var result = new CertSyncResult();
        var list = LoadManifest();

        foreach (var cert in certs)
        {
            if (cert == null) continue;

            var name = GetSafeName(cert.Name.IsNullOrEmpty() ? cert.Domain : cert.Name);
            if (name.IsNullOrEmpty())
            {
                Log?.Warn("Certificate [{0}] has an invalid file name and was skipped", cert.Id);
                continue;
            }

            var dir = ResolvePath(cert.TargetPath.IsNullOrEmpty() ? GetCertPath() : cert.TargetPath);

            var item = list.FirstOrDefault(e => e.Id == cert.Id);
            if (item == null)
            {
                item = new CertManifest { Id = cert.Id };
                list.Add(item);
            }
            item.Domain = cert.Domain;
            item.Name = name;
            item.Version = cert.Version;
            item.UpdateTime = DateTime.Now;

            if (!cert.CertFile.IsNullOrEmpty())
            {
                item.CertFile = dir.CombinePath(name + ".pem");
                if (await DownloadAsync(cert.CertFile, item.CertFile, cert.CertHash, false, buildUrl, cancellationToken).ConfigureAwait(false))
                    result.Changed = true;
            }
            if (!cert.KeyFile.IsNullOrEmpty())
            {
                item.KeyFile = dir.CombinePath(name + ".privatekey.pem");
                if (await DownloadAsync(cert.KeyFile, item.KeyFile, cert.KeyHash, true, buildUrl, cancellationToken).ConfigureAwait(false))
                    result.Changed = true;
            }
            if (!cert.PfxFile.IsNullOrEmpty())
            {
                item.PfxFile = dir.CombinePath(name + ".pfx");
                if (await DownloadAsync(cert.PfxFile, item.PfxFile, cert.PfxHash, true, buildUrl, cancellationToken).ConfigureAwait(false))
                    result.Changed = true;
            }

            result.Certs.Add(item);
            Log?.Info("Certificate [{0}] {1} is ready at {2}", cert.Domain, name, dir);
        }

        // 全量同步时，清理本地清单里已经不存在的证书记录
        if (replace)
        {
            var ids = new HashSet<Int32>(result.Certs.Select(e => e.Id));
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(list[i].Id)) list.RemoveAt(i);
            }
        }

        SaveManifest(list);

        return result;
    }

    /// <summary>下载单个证书文件。已存在且哈希一致时返回 false</summary>
    private async Task<Boolean> DownloadAsync(String url, String path, String? hash, Boolean isPrivate, Func<String, String?>? buildUrl, CancellationToken cancellationToken)
    {
        var fi = path.AsFile();
        if (fi.Exists && !hash.IsNullOrEmpty() && fi.MD5().ToHex().EqualIgnoreCase(hash)) return false;
        if (fi.Exists && hash.IsNullOrEmpty() && fi.Length > 0) return false;

        if (buildUrl != null) url = buildUrl(url) ?? url;

        Log?.Info("Download certificate file: {0} => {1}", url, path);

        // 先写临时文件，校验通过后再覆盖，避免下载中断留下半截私钥
        var tmp = path + ".tmp";
        try
        {
            path.EnsureDirectory(true);

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
            using (var rs = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                rs.EnsureSuccessStatusCode();

                using var fs = File.Create(tmp);
                await rs.Content.CopyToAsync(fs).ConfigureAwait(false);
            }

            if (!hash.IsNullOrEmpty())
            {
                var md5 = tmp.AsFile().MD5().ToHex();
                if (!md5.EqualIgnoreCase(hash)) throw new InvalidDataException($"Certificate file hash mismatch: {path}");
            }

            File.Copy(tmp, path, true);
            SetPermission(path, isPrivate);

            return true;
        }
        finally
        {
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch { }
        }
    }

    /// <summary>设置私钥文件权限，仅允许属主读写</summary>
    private static void SetPermission(String path, Boolean isPrivate)
    {
        if (!isPrivate || Runtime.Windows) return;

#if NET7_0_OR_GREATER
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { }
#else
        try
        {
            "chmod".Run($"600 \"{path}\"", 3_000);
        }
        catch { }
#endif
    }

    /// <summary>过滤非法文件名，只保留字母数字与点横线下划线</summary>
    /// <param name="name">原始名称</param>
    /// <returns></returns>
    public static String? GetSafeName(String? name)
    {
        if (name.IsNullOrEmpty()) return null;

        name = name.Trim();
        var chars = name.Where(e => e is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_').ToArray();
        var rs = new String(chars).Trim('.', '-', '_');

        // 拒绝空名称与路径穿越
        if (rs.IsNullOrEmpty() || rs == "." || rs == "..") return null;

        return rs;
    }
    #endregion

    #region Nginx 配置
    /// <summary>解析 Nginx 站点配置中的证书引用。替换占位符，并在需要 HTTPS 却缺少证书指令时自动补全</summary>
    /// <param name="text">站点配置文本</param>
    /// <returns>处理后的配置文本</returns>
    public String ResolveConfig(String text)
    {
        if (text.IsNullOrEmpty()) return text;

        var certDir = GetCertPath();
        var nginx = new NginxFile();
        var parsed = nginx.Parse(text);

        var host = (nginx.ServerName + "").Split(',')[0].Trim();
        var item = FindByHost(host);
        var name = item?.Name;
        if (name.IsNullOrEmpty()) name = host;

        text = text.Replace(NginxFile.CertDirToken, certDir);
        text = text.Replace(NginxFile.CertNameToken, name);

        // 已显式配置证书，或未监听 HTTPS 端口，无需补全
        if (!parsed || !nginx.SslCertificate.IsNullOrEmpty()) return text;
        if (!nginx.Ports.Any(e => e % 1000 == 443)) return text;
        if (name.IsNullOrEmpty() || name == "*") return text;

        var certFile = certDir.CombinePath(name + ".pem");
        var keyFile = certDir.CombinePath(name + ".privatekey.pem");
        var add = $"{Environment.NewLine}{Environment.NewLine}\tssl_certificate\t{certFile};{Environment.NewLine}\tssl_certificate_key\t{keyFile};";

        // 插到 server_name 行之后，保持配置可读
        var match = Regex.Match(text, @"(?im)^[ \t]*server_name[ \t]+[^;]+;[ \t]*$");
        if (match.Success) return text.Insert(match.Index + match.Length, add);

        var index = text.IndexOf('{');
        if (index < 0) return text;

        return text.Insert(index + 1, add);
    }
    #endregion
}

/// <summary>本地证书清单项</summary>
public class CertManifest
{
    /// <summary>证书编号</summary>
    public Int32 Id { get; set; }

    /// <summary>证书域名。支持通配符</summary>
    public String? Domain { get; set; }

    /// <summary>证书名。下发文件前缀</summary>
    public String? Name { get; set; }

    /// <summary>证书链文件绝对路径</summary>
    public String? CertFile { get; set; }

    /// <summary>私钥文件绝对路径</summary>
    public String? KeyFile { get; set; }

    /// <summary>PFX 文件绝对路径</summary>
    public String? PfxFile { get; set; }

    /// <summary>版本。证书指纹或更新时间</summary>
    public String? Version { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>证书同步结果</summary>
public class CertSyncResult
{
    /// <summary>是否有文件发生变化</summary>
    public Boolean Changed { get; set; }

    /// <summary>本次涉及的证书清单</summary>
    public IList<CertManifest> Certs { get; } = [];
}
