using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Web;

namespace Stardust.Extensions.Caches;

/// <summary>文件缓存提供者。本地文件不存在时，从上级拉取</summary>
class CacheFileProvider : IFileProvider
{
    #region 属性
    private static readonly Char[] _pathSeparators = new Char[2]
    {
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    };

    private readonly ExclusionFilters _filters;

    /// <summary>根目录</summary>
    public String? Root { get; }

    /// <summary>服务端地址。本地文件不存在时，将从这里下载</summary>
    public String[]? Servers { get; set; }

    /// <summary>目标服务器地址。下载文件带有sh脚本时，把其中的源服务器地址替换为目标服务器地址</summary>
    public String? TargetServer { get; set; }

    /// <summary>获取服务器地址的委托。方便实时更新</summary>
    public Func<String[]>? GetServers { get; set; }

    /// <summary>索引信息文件。列出扩展显示的文件内容</summary>
    public String? IndexInfoFile { get; set; }

    /// <summary>超时时间</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>APM追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    /// <summary>
    /// 实例化
    /// </summary>
    /// <param name="root"></param>
    /// <param name="server"></param>
    /// <param name="filters"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public CacheFileProvider(String root, String? server, ExclusionFilters filters = ExclusionFilters.Sensitive)
    {
        if (!Path.IsPathRooted(root)) throw new ArgumentException("The path must be absolute.", nameof(root));

        Root = Path.GetFullPath(root).EnsureEnd(Path.DirectorySeparatorChar + "");
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);

        Servers = server?.Split(",");
        _filters = filters;
    }

    private String? GetFullPath(String path)
    {
        if (Root.IsNullOrEmpty()) return null;
        if (PathNavigatesAboveRoot(path)) return null;

        String fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(Root, path));
        }
        catch
        {
            return null;
        }

        return !fullPath.StartsWithIgnoreCase(Root) ? null : fullPath;
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IFileInfo GetFileInfo(String subpath)
    {
        if (String.IsNullOrEmpty(subpath) || HasInvalidPathChars(subpath)) return new NotFoundFileInfo(subpath);

        subpath = subpath.TrimStart(_pathSeparators);
        if (Path.IsPathRooted(subpath)) return new NotFoundFileInfo(subpath);

        var fullPath = GetFullPath(subpath);
        if (fullPath == null) return new NotFoundFileInfo(subpath);

        using var span = Tracer?.NewSpan(nameof(GetFileInfo), subpath);

        // 本地不存在时，从服务器下载
        var fi = fullPath.AsFile();
        if (Path.GetFileName(fullPath).Contains('.'))
        {
            if (!fi.Exists)
                fi = DownloadFile(subpath, fullPath).ConfigureAwait(false).GetAwaiter().GetResult()?.AsFile();
            else if (fi.LastWriteTime.AddMonths(1) < DateTime.Now)
                _ = Task.Run(() => DownloadFile(subpath, fullPath));
        }
        if (fi == null || !fi.Exists) return new NotFoundFileInfo(subpath);

        var fileInfo = new FileInfo(fullPath);
        return IsExcluded(fileInfo, _filters) ? new NotFoundFileInfo(subpath) : new PhysicalFileInfo(fileInfo);
    }

    async Task<String?> DownloadFile(String subpath, String fullPath)
    {
        var span = DefaultSpan.Current;
        var svrs = GetServers?.Invoke() ?? Servers;
        if (svrs != null && svrs.Length != 0)
        {
            foreach (var item in svrs)
            {
                try
                {
                    var url = subpath.Replace("\\", "/");
                    url = item.Contains("{0}") ? item.Replace("{0}", url) : item.TrimEnd("/") + url.EnsureStart("/");

                    span?.AppendTag(url);
                    XTrace.WriteLine("下载文件：{0}", url);

                    // 先下载到临时目录，避免出现下载半截的情况
                    var tmp = Path.GetTempFileName();
                    {
                        using var fs = new FileStream(tmp, FileMode.OpenOrCreate);
                        using var client = new HttpClient { Timeout = Timeout };
                        using var rs = await client.GetStreamAsync(url).ConfigureAwait(false);
                        rs.CopyTo(fs);
                        fs.Flush();
                        fs.SetLength(fs.Position);
                    }

                    // 移动临时文件到最终目录
                    fullPath.EnsureDirectory(true);
                    File.Move(tmp, fullPath, true);

                    // 修改脚本文件中的Url
                    var target = TargetServer;
                    if (target.IsNullOrEmpty())
                    {
                        target = NewLife.Setting.Current.ServiceAddress?.Split(",").FirstOrDefault();
                        if (!target.IsNullOrEmpty()) target = target.EnsureEnd("/files/");
                    }
                    if (!target.IsNullOrEmpty() && fullPath.EndsWithIgnoreCase(".sh", ".bat"))
                    {
                        var txt = File.ReadAllText(fullPath);
                        var txt2 = txt.Replace(item.EnsureEnd("/"), target.EnsureEnd("/"));
                        if (txt != txt2) File.WriteAllText(fullPath, txt2);
                    }

                    XTrace.WriteLine("下载文件完成：{0}", fullPath);
                    //fi = fullPath.AsFile();
                    return fullPath;
                }
                catch (Exception ex)
                {
                    span?.SetError(ex, null);
                    XTrace.WriteLine(ex.Message);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="filters"></param>
    /// <returns></returns>
    public static Boolean IsExcluded(FileSystemInfo fileInfo, ExclusionFilters filters)
    {
        if (filters == ExclusionFilters.None) return false;

        return fileInfo.Name.StartsWith(".", StringComparison.Ordinal) && (filters & ExclusionFilters.DotPrefixed) != 0 ||
            fileInfo.Exists && (

                (fileInfo.Attributes & FileAttributes.Hidden) != 0 && (filters & ExclusionFilters.Hidden) != 0 ||
                (fileInfo.Attributes & FileAttributes.System) != 0 && (filters & ExclusionFilters.System) != 0
            );
    }

    /// <summary>
    /// 获取文件内容
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IDirectoryContents GetDirectoryContents(String subpath)
    {
        try
        {
            if (subpath == null || HasInvalidPathChars(subpath)) return NotFoundDirectoryContents.Singleton;

            subpath = subpath.TrimStart(_pathSeparators);
            if (Path.IsPathRooted(subpath)) return NotFoundDirectoryContents.Singleton;

            using var span = Tracer?.NewSpan(nameof(GetDirectoryContents), subpath);

            var fullPath = GetFullPath(subpath);

            // 下载信息文件
            var svrs = GetServers?.Invoke() ?? Servers;
            if (!IndexInfoFile.IsNullOrEmpty() && svrs != null && svrs.Length > 0)
            {
                var fi = fullPath.CombinePath(IndexInfoFile).GetBasePath().AsFile();
                if (!fi.Exists)
                    fi = DownloadDirectory(subpath, fi.FullName, svrs).ConfigureAwait(false).GetAwaiter().GetResult()?.AsFile();
                else if (fi.LastWriteTime.AddDays(1) < DateTime.Now)
                    _ = Task.Run(() => DownloadDirectory(subpath, fi.FullName, svrs));
            }

            return fullPath == null || !Directory.Exists(fullPath)
                      ? NotFoundDirectoryContents.Singleton
                      : new CacheDirectoryContents(fullPath, _filters) { IndexInfoFile = IndexInfoFile };
        }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }

        return NotFoundDirectoryContents.Singleton;
    }

    async Task<String?> DownloadDirectory(String subpath, String fullPath, String[] svrs)
    {
        subpath = subpath.TrimEnd('/');
        var span = DefaultSpan.Current;
        foreach (var item in svrs)
        {
            try
            {
                var url = subpath.Replace("\\", "/");
                url = item.Contains("{0}") ? item.Replace("{0}", url) : item.TrimEnd("/") + url.EnsureStart("/");

                span?.AppendTag(url);
                XTrace.WriteLine("下载目录：{0}", url);

                using var client = new HttpClient { Timeout = Timeout };
                var html = await client.GetStringAsync(url).ConfigureAwait(false);

                var links = Link.Parse(html, url);
                var list = links.Select(e => new FileInfoModel
                {
                    Name = HttpUtility.UrlDecode(e.FullName + ""),
                    LastModified = e.Time.Year > 2000 ? e.Time : DateTime.Now,
                    Exists = true,
                    IsDirectory = e.Hash.IsNullOrEmpty(),
                }).ToList();

                var csv = new CsvDb<FileInfoModel> { FileName = fullPath };
                csv.Write(list, false);

                XTrace.WriteLine("下载目录完成：{0}", fullPath);
                return fullPath;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                XTrace.WriteLine("下载目录出错：{0}", ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// 监控文件改变
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    public IChangeToken Watch(String filter) => NullChangeToken.Singleton;

    internal static Boolean HasInvalidPathChars(String path) => path.IndexOfAny(_invalidFileNameChars) != -1;

    private static readonly Char[] _invalidFileNameChars = (from c in Path.GetInvalidFileNameChars()
                                                            where c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar
                                                            select c).ToArray();

    internal static Boolean PathNavigatesAboveRoot(String path)
    {
        var stringTokenizer = new StringTokenizer(path, _pathSeparators);
        var num = 0;
        foreach (var item in stringTokenizer)
        {
            if (item.Equals(".") || item.Equals("")) continue;
            if (item.Equals(".."))
            {
                num--;
                if (num == -1) return true;
            }
            else
                num++;
        }
        return false;
    }
}