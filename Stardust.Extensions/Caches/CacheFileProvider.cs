using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Model;
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

    /// <summary>超时时间。文件下载用，默认5秒</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>目录索引下载超时。目录列表是小数据，且同步阻塞请求线程，默认3秒</summary>
    public TimeSpan DirectoryTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>请求虚拟路径。如/files。拼接上游地址时必须补回，否则会请求到站点根目录</summary>
    public String? RequestPath { get; set; }

    /// <summary>失败负缓存时长。下载失败后在该时段内不再重复拉取，避免每次请求都空等超时，默认10分钟</summary>
    public TimeSpan NegativeCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>本进程监听端口。用于识别上游地址是否指向自身。为空时退化为按主机判断</summary>
    public static HashSet<Int32> LocalPorts { get; } = new();

    /// <summary>APM追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>并发下载信号量。限制过期文件同时下载数量，避免线程爆炸</summary>
    private readonly SemaphoreSlim _downloadSemaphore = new(4, 4);

    /// <summary>同步下载信号量。请求线程上的下载必须有上限，否则慢上游会打满线程池拖垮全站</summary>
    private readonly SemaphoreSlim _syncSemaphore = new(2, 2);

    /// <summary>失败负缓存。key为子路径，value为失败时间</summary>
    private readonly ConcurrentDictionary<String, DateTime> _failed = new();

    /// <summary>共享HttpClient。复用连接池，避免每次请求新建TCP连接导致TIME_WAIT堆积</summary>
    private static HttpClient? _client;
    private static readonly Object _clientLock = new();
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

        Tracer ??= ServiceProvider?.GetService<ITracer>();
        using var span = Tracer?.NewSpan(nameof(GetFileInfo), subpath);

        // 本地不存在时，从服务器下载
        var fi = fullPath.AsFile();
        if (Path.GetFileName(fullPath).Contains('.'))
        {
            var svrs = GetServers?.Invoke() ?? Servers;
            if (!fi.Exists)
            {
                // 自环或负缓存命中时不发起网络请求，直接当作不存在，快速返回404
                if (!CanDownload("file:" + subpath, svrs, span))
                {
                    fi = null;
                }
                // 同步下载必须限流，拿不到名额就快速失败，绝不让请求线程排队等待
                else if (!_syncSemaphore.Wait(0))
                {
                    span?.AppendTag("下载并发已满，跳过本次下载");
                    fi = null;
                }
                else
                {
                    try
                    {
                        fi = DownloadFile(subpath, fullPath, svrs).ConfigureAwait(false).GetAwaiter().GetResult()?.AsFile();
                    }
                    finally { _syncSemaphore.Release(); }
                }
            }
            else if (fi.LastWriteTime.AddMonths(1) < DateTime.Now && _downloadSemaphore.Wait(0))
            {
                // 后台异步下载。信号量限流（最多4个并发），避免大量过期文件同时下载造成线程爆炸
                _ = Task.Run(async () =>
                {
                    try { await DownloadFile(subpath, fullPath, svrs).ConfigureAwait(false); }
                    finally { _downloadSemaphore.Release(); }
                });
            }
        }
        if (fi == null || !fi.Exists) return new NotFoundFileInfo(subpath);

        var fileInfo = new FileInfo(fullPath);
        return IsExcluded(fileInfo, _filters) ? new NotFoundFileInfo(subpath) : new PhysicalFileInfo(fileInfo);
    }

    async Task<String?> DownloadFile(String subpath, String fullPath, String[]? svrs = null)
    {
        var span = DefaultSpan.Current;
        svrs ??= GetServers?.Invoke() ?? Servers;
        if (svrs != null && svrs.Length != 0)
        {
            foreach (var item in svrs)
            {
                // 上游指向自身时不做请求，否则会自己请求自己形成死循环
                if (IsSelf(item))
                {
                    span?.AppendTag($"跳过自环上游：{item}");
                    continue;
                }

                try
                {
                    var url = BuildUrl(item, subpath);

                    span?.AppendTag(url);
                    XTrace.WriteLine("下载文件：{0}", url);

                    // 先下载到临时目录，避免出现下载半截的情况
                    var tmp = Path.GetTempFileName();
                    {
                        using var fs = new FileStream(tmp, FileMode.OpenOrCreate);
                        using var cts = new CancellationTokenSource(Timeout);
                        using var rs = await GetClient()
                            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                            .ConfigureAwait(false);

                        // 非成功状态不得写入缓存，否则会把404页面当成文件缓存下来
                        if (!rs.IsSuccessStatusCode)
                            throw new HttpRequestException($"下载失败，状态码[{(Int32)rs.StatusCode}]，地址[{url}]");

                        using var stream = await rs.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        stream.CopyTo(fs);
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
                        target = StarSetting.Current.ServiceAddress?.Split(",").FirstOrDefault(e => e.StartsWithIgnoreCase("http"));
                        if (!target.IsNullOrEmpty()) target = target.EnsureEnd("/files/");
                    }
                    if (!target.IsNullOrEmpty() && fullPath.EndsWithIgnoreCase(".sh", ".bat"))
                    {
                        var txt = File.ReadAllText(fullPath);
                        var txt2 = txt.Replace(item.TrimSuffix("/"), target.TrimSuffix("/"));
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

        // 全部上游都不可用，写入负缓存，短期内不再重复拉取，避免每次请求都空等超时
        SetNegative("file:" + subpath);

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

            Tracer ??= ServiceProvider?.GetService<ITracer>();
            using var span = Tracer?.NewSpan(nameof(GetDirectoryContents), subpath);

            var fullPath = GetFullPath(subpath);

            // 下载信息文件
            var svrs = GetServers?.Invoke() ?? Servers;
            if (!IndexInfoFile.IsNullOrEmpty() && svrs != null && svrs.Length > 0)
            {
                var fi = fullPath.CombinePath(IndexInfoFile).GetBasePath().AsFile();
                if (!fi.Exists)
                {
                    // 自环或负缓存命中时不发起网络请求，直接返回空目录
                    if (!CanDownload("dir:" + subpath, svrs, span))
                    {
                        // 跳过下载
                    }
                    // 同步下载必须限流，拿不到名额就快速失败，绝不让请求线程排队等待
                    else if (!_syncSemaphore.Wait(0))
                    {
                        span?.AppendTag("下载并发已满，跳过本次下载");
                    }
                    else
                    {
                        try
                        {
                            fi = DownloadDirectory(subpath, fi.FullName, svrs).ConfigureAwait(false).GetAwaiter().GetResult()?.AsFile();
                        }
                        finally { _syncSemaphore.Release(); }
                    }
                }
                else if (fi.LastWriteTime.AddDays(1) < DateTime.Now && _downloadSemaphore.Wait(0))
                {
                    // 后台异步下载，信号量限流
                    _ = Task.Run(async () =>
                    {
                        try { await DownloadDirectory(subpath, fi.FullName, svrs).ConfigureAwait(false); }
                        finally { _downloadSemaphore.Release(); }
                    });
                }
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
        subpath = subpath.TrimSuffix("/");
        var span = DefaultSpan.Current;
        foreach (var item in svrs)
        {
            // 上游指向自身时不做请求，否则会自己请求自己形成死循环
            if (IsSelf(item))
            {
                span?.AppendTag($"跳过自环上游：{item}");
                continue;
            }

            try
            {
                var url = BuildUrl(item, subpath);

                span?.AppendTag(url);
                XTrace.WriteLine("下载目录：{0}", url);

                using var cts = new CancellationTokenSource(DirectoryTimeout);
                using var rs = await GetClient()
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .ConfigureAwait(false);

                // 非成功状态视为失败，避免把404页面当成目录索引解析
                if (!rs.IsSuccessStatusCode)
                    throw new HttpRequestException($"下载目录失败，状态码[{(Int32)rs.StatusCode}]，地址[{url}]");

                var html = await rs.Content.ReadAsStringAsync().ConfigureAwait(false);

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

        // 全部上游都不可用，写入负缓存，短期内不再重复拉取，避免每次请求都空等超时
        SetNegative("dir:" + subpath);

        return null;
    }

    #region 下载治理
    /// <summary>拼接上游下载地址。必须补回请求虚拟路径（如/files），否则会请求到站点根目录</summary>
    /// <param name="server">上游地址，支持包含{0}占位符</param>
    /// <param name="subpath">相对于请求路径的子路径</param>
    /// <returns></returns>
    String BuildUrl(String server, String subpath)
    {
        var url = subpath.Replace("\\", "/").TrimStart('/');
        if (server.Contains("{0}")) return server.Replace("{0}", url);

        var baseUrl = server.TrimSuffix("/");

        // 上游地址未带请求路径时补上，兼容已配置的完整地址，避免重复拼接成 /files/files
        var rp = RequestPath?.Trim('/');
        if (!rp.IsNullOrEmpty() && !baseUrl.EndsWithIgnoreCase("/" + rp)) baseUrl += "/" + rp;

        return baseUrl + "/" + url;
    }

    /// <summary>判断上游地址是否指向自身。指向自身时拉取文件会形成自己请求自己的死循环</summary>
    /// <param name="server"></param>
    /// <returns></returns>
    internal static Boolean IsSelf(String server)
    {
        if (server.IsNullOrEmpty()) return false;
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)) return false;

        // 与自身部署访问地址（NewLife.Setting.Current.ServiceAddress）比对，scheme+主机+端口一致即为自身。
        // 覆盖域名、公网IP等无法通过本机网卡识别的场景；每次实时读取，配置热更新也能生效
        var origin = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        var addrs = NewLife.Setting.Current.ServiceAddress?.Split(",") ?? [];
        foreach (var item in addrs)
        {
            if (Uri.TryCreate(item.Trim(), UriKind.Absolute, out var svc) &&
                svc.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).EqualIgnoreCase(origin))
                return true;
        }

        var host = uri.Host;
        var isLocal = host.EqualIgnoreCase("localhost", "127.0.0.1", "::1") || LocalHosts.Contains(host);
        if (!isLocal) return false;

        // 已知本进程监听端口时比对端口，避免误伤同机不同端口的其它服务
        if (LocalPorts.Count > 0) return LocalPorts.Contains(uri.Port);

        return true;
    }

    /// <summary>本机主机名与IP地址集合，用于识别自环上游。延迟初始化，避免每次请求都做DNS解析</summary>
    private static readonly Lazy<HashSet<String>> _localHosts = new(() =>
    {
        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        try
        {
            set.Add(System.Net.Dns.GetHostName());
            foreach (var item in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                set.Add(item.ToString());
        }
        catch { }

        return set;
    });
    private static HashSet<String> LocalHosts => _localHosts.Value;

    /// <summary>共享HttpClient。复用连接池，避免每次下载都新建TCP连接造成TIME_WAIT堆积</summary>
    /// <returns></returns>
    static HttpClient GetClient()
    {
        if (_client != null) return _client;

        lock (_clientLock)
        {
            if (_client != null) return _client;

            var handler = new SocketsHttpHandler
            {
                // 连接池复用，定期重连以应对DNS变化
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 8,
                UseProxy = false,
            };
#if NET5_0_OR_GREATER
            handler.ConnectTimeout = TimeSpan.FromSeconds(3);
#endif
            // 单次超时由调用方通过CancellationToken控制，这里不设全局超时
            _client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }

        return _client;
    }

    /// <summary>是否可以发起下载。自环上游与负缓存命中时直接拒绝，避免无意义的等待</summary>
    /// <param name="subpath"></param>
    /// <param name="svrs"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    Boolean CanDownload(String subpath, String[]? svrs, ISpan? span)
    {
        if (svrs == null || svrs.Length == 0) return false;

        if (IsNegative(subpath))
        {
            span?.AppendTag("负缓存命中，跳过下载");
            return false;
        }

        // 所有上游都指向自身，拉取必然是死循环，直接按文件不存在处理
        if (svrs.All(IsSelf))
        {
            span?.AppendTag("上游指向自身，跳过下载");
            XTrace.WriteLine("文件缓存上游[{0}]指向自身，跳过下载：{1}", svrs.Join(), subpath);
            return false;
        }

        return true;
    }

    /// <summary>是否处于失败负缓存期内</summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    Boolean IsNegative(String subpath)
    {
        if (NegativeCacheDuration <= TimeSpan.Zero) return false;
        if (!_failed.TryGetValue(subpath, out var time)) return false;

        // 已过期则移除，让下一次请求重新尝试
        if (time.Add(NegativeCacheDuration) < DateTime.Now)
        {
            _failed.TryRemove(subpath, out _);
            return false;
        }

        return true;
    }

    /// <summary>记录下载失败，进入负缓存</summary>
    /// <param name="subpath"></param>
    void SetNegative(String subpath)
    {
        if (NegativeCacheDuration <= TimeSpan.Zero) return;

        // 防止恶意请求大量不存在的路径撑爆内存
        if (_failed.Count > 10_000) CleanNegative();

        _failed[subpath] = DateTime.Now;
    }

    /// <summary>清理已过期的负缓存</summary>
    void CleanNegative()
    {
        var now = DateTime.Now;
        foreach (var item in _failed)
        {
            if (item.Value.Add(NegativeCacheDuration) < now) _failed.TryRemove(item.Key, out _);
        }
    }
    #endregion

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